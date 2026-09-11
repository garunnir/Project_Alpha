using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    // ============================================================
    // TileMapModel — 런타임 타일 저장·조회·오클루전의 단일 창구
    // ============================================================
    public class TileMapModel : IMapModel
    {
        public event Action<Vector3Int, IReadOnlyList<TileData>> OnRuntimeDataChanged;
        public event Action<IReadOnlyCollection<Vector3Int>> OnRuntimeBatchChanged;
        public event Action<TileData> OnRuntimeTileAdded;
        public event Action<TileData> OnRuntimeTileRemoved;
        public event Action<TileOcclusionPresentationDelta> OnTileOcclusionPresentationDelta;
        internal Dictionary<Vector3Int, List<TileData>> tiles = new Dictionary<Vector3Int, List<TileData>>();

        /// <summary>파생 인덱스: <see cref="tiles"/>·faceBinder와 동기화된 Guid 조회용.</summary>
        private readonly Dictionary<Guid, TileData> _tilesById = new Dictionary<Guid, TileData>();
        private readonly TileFaceBinder _faceBinder = new TileFaceBinder();

        private List<TileData> _cachedList = new List<TileData>();
        private bool _isDirty;
        private WallOcclusionFinder _occlusionFinder;
        private readonly HashSet<Guid> _hiddenWallTileIds = new HashSet<Guid>();
        private readonly Dictionary<Guid, TileData> _hiddenWallTileCache = new Dictionary<Guid, TileData>();
        /// <summary>BFS delta 산출용 내부 캐시. 화면 SSOT는 applier entry store.</summary>
        private readonly Dictionary<Guid, float> _lastAppliedOcclusion = new Dictionary<Guid, float>();
        private readonly List<(Guid tileId, float occlusion01)> _occlusionDeltaApply = new List<(Guid, float)>();
        private readonly List<Guid> _occlusionDeltaClear = new List<Guid>();
        private readonly List<OcclusionWallEntry> _occlusionWallEntries = new List<OcclusionWallEntry>();
        private readonly HashSet<Vector3Int> _changedCellsBuffer = new HashSet<Vector3Int>();
        private readonly List<TileData> _forEachRuntimeScratch = new List<TileData>();
        private readonly List<Vector3Int> _playerOccupiedCellsBuffer = new List<Vector3Int>();

        private bool _hasLastOcclusionPlayerCell;
        private Vector3Int _lastOcclusionPlayerCell;
        private Func<TileData, bool> _occlusionTileVisible;
        private TileMapCacheHub _mapCacheHub;
        private BuildingGroupBuilder _buildingGroupBuilder;

        public ITileFaceBinderReadOnly FaceBinder => _faceBinder;

        public bool CellHasWalkableFloor(int x, int cellY, int z)
        {
            if (_mapCacheHub != null)
                return _mapCacheHub.Topology.CellHasFloor(x, cellY, z);

            return _faceBinder.TryGetFloorFace(
                FloorFaceKey.ForWalkableCell(new Vector3Int(x, cellY, z)),
                out var face) &&
                TileCollisionFlagsUtil.Has(face.identity.collisionFlags, TileCollisionFlags.ProvidesLogicalFloor);
        }

        public bool TryGetFloorFaceForWalkableCell(int x, int cellY, int z, out TileData face)
        {
            if (_mapCacheHub != null)
                return _mapCacheHub.Topology.TryGetFloorFaceForWalkableCell(x, cellY, z, out face);

            return _faceBinder.TryGetFloorFace(
                FloorFaceKey.ForWalkableCell(new Vector3Int(x, cellY, z)),
                out face);
        }

        public void SetMapCacheHub(TileMapCacheHub hub) => _mapCacheHub = hub;

        public void SetBuildingGroupBuilder(BuildingGroupBuilder builder) => _buildingGroupBuilder = builder;

        public bool TryGetEdgeWallIdsForRoom(RoomKey roomKey, out IReadOnlyCollection<Guid> edgeIds)
        {
            if (_mapCacheHub != null)
                return _mapCacheHub.Buildings.TryGetEdgeWalls(roomKey, out edgeIds);

            edgeIds = Array.Empty<Guid>();
            return false;
        }

        internal void MarkTilesDirty() => _isDirty = true;

        /// <summary>
        /// 변경된 점유/면 셀만 뷰 refresh (SyncTileParent·UpdateTile).
        /// 전맵 batch 금지 — dig 하이라이트·presentation을 깨뜨림.
        /// </summary>
        internal void NotifyCellsChanged(IReadOnlyCollection<Vector3Int> cells)
        {
            if (cells == null || cells.Count == 0)
                return;

            var set = new HashSet<Vector3Int>();
            foreach (Vector3Int cell in cells)
                set.Add(cell);

            if (set.Count == 0)
                return;

            MarkTilesDirty();
            OnRuntimeBatchChanged?.Invoke(set);
        }

        public void GatherRenderableTiles(Vector3Int cellPos, List<TileData> buffer)
        {
            buffer.Clear();

            if (tiles.TryGetValue(cellPos, out var list))
                buffer.AddRange(list);

            _faceBinder.AppendFacesAtCell(cellPos, buffer);
        }

        private void NotifyCell(Vector3Int cell)
        {
            var snapshot = new List<TileData>();
            GatherRenderableTiles(cell, snapshot);
            OnRuntimeDataChanged?.Invoke(cell, snapshot);
        }

        public void AddTile(Vector3Int pos, TileData tile) => SetTile(tile);

        public IReadOnlyList<TileData> TilesSnapshot
        {
            get
            {
                if (_isDirty)
                {
                    CopyRuntimeTilesTo(_cachedList);
                    _isDirty = false;
                }

                return _cachedList;
            }
        }

        public void SetTile(TileData tileData)
        {
            if (TryFindTileById(tileData.tileDefId, out var existing) &&
                IdentityEquals(existing.identity, tileData.identity))
                return;

            if (TryFindTileById(tileData.tileDefId, out existing))
                RemoveFromStoreOnly(existing);

            var changedCells = new HashSet<Vector3Int>();

            switch (TileIdentityUtil.GetPlacementSlot(tileData.identity))
            {
                case TilePlacementSlot.VerticalFace:
                    SetWallFaceTile(tileData);
                    break;
                case TilePlacementSlot.HorizontalFace:
                    SetFloorFaceTile(tileData);
                    break;
                default:
                    SetCellTile(tileData);
                    break;
            }

            TileIdentityUtil.CollectAffectedCells(tileData.identity, changedCells);

            InvalidateOcclusionPlayerTracking();
            NotifyBuildingTopologyChanged(changedCells);
        }

        public void RemoveTile(TileData tileData)
        {
            var changedCells = new HashSet<Vector3Int>();
            bool removed = false;

            if (TryFindTileById(tileData.tileDefId, out var existingTile))
            {
                switch (TileIdentityUtil.GetPlacementSlot(existingTile.identity))
                {
                    case TilePlacementSlot.VerticalFace:
                        if (_faceBinder.TryRemoveWall(
                                WallEdgeKey.FromWallTileIdentity(existingTile.identity),
                                out var removedWall))
                        {
                            removed = true;
                            tileData = removedWall;
                            TileIdentityUtil.CollectAffectedCells(tileData.identity, changedCells);
                        }
                        break;
                    case TilePlacementSlot.HorizontalFace:
                        if (_faceBinder.TryRemoveFloor(
                                FloorFaceKey.FromFloorTileIdentity(existingTile.identity),
                                out var removedFloor))
                        {
                            removed = true;
                            tileData = removedFloor;
                            TileIdentityUtil.CollectAffectedCells(tileData.identity, changedCells);
                        }
                        break;
                    default:
                        RemoveOccupiedCellTile(ref tileData, ref removed, changedCells);
                        break;
                }
            }
            else
            {
                if (_faceBinder.TryRemove(tileData.tileDefId, out var removedFace))
                {
                    removed = true;
                    tileData = removedFace;
                    TileIdentityUtil.CollectAffectedCells(tileData.identity, changedCells);
                }
                else
                {
                    RemoveOccupiedCellTile(ref tileData, ref removed, changedCells);
                }
            }

            if (!removed)
                return;

            _tilesById.Remove(tileData.tileDefId);
            _isDirty = true;
            InvalidateOcclusionPlayerTracking();
            NotifyBuildingTopologyChanged(changedCells, isRemoval: true, removedTile: tileData);
            OnRuntimeTileRemoved?.Invoke(tileData);

            foreach (var cell in changedCells)
                NotifyCell(cell);
        }

        void RemoveFromStoreOnly(in TileData tile)
        {
            switch (TileIdentityUtil.GetPlacementSlot(tile.identity))
            {
                case TilePlacementSlot.VerticalFace:
                    _faceBinder.TryRemoveWall(WallEdgeKey.FromWallTileIdentity(tile.identity), out _);
                    break;
                case TilePlacementSlot.HorizontalFace:
                    _faceBinder.TryRemoveFloor(FloorFaceKey.FromFloorTileIdentity(tile.identity), out _);
                    break;
                default:
                {
                    Vector3Int pos = tile.identity.GridPos;
                    if (!tiles.TryGetValue(pos, out var list))
                        break;

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i].tileDefId != tile.tileDefId)
                            continue;
                        list.RemoveAt(i);
                        break;
                    }

                    if (list.Count == 0)
                        tiles.Remove(pos);
                    break;
                }
            }

            _tilesById.Remove(tile.tileDefId);
            _isDirty = true;
        }

        void RemoveOccupiedCellTile(ref TileData tileData, ref bool removed, HashSet<Vector3Int> changedCells)
        {
            Vector3Int pos = tileData.identity.GridPos;
            if (!tiles.TryGetValue(pos, out var list))
                return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].tileDefId != tileData.tileDefId)
                    continue;

                tileData = list[i];
                list.RemoveAt(i);
                removed = true;
                changedCells.Add(pos);
                break;
            }

            if (list.Count == 0)
                tiles.Remove(pos);
        }

        public bool TryGetTileById(Guid tileId, out TileData tileData) => TryFindTileById(tileId, out tileData);

        public bool TryGetCellTiles(int x, int z, int cellY, out IReadOnlyList<TileData> tileList)
        {
            if (_mapCacheHub != null &&
                _mapCacheHub.TryGetCellTiles(x, z, cellY, out var hubList))
            {
                tileList = hubList;
                return true;
            }

            return TryGetTiles(new Vector3Int(x, cellY, z), out tileList);
        }

        public IEnumerable<(int x, int z, int y)> EnumerateOccupiedCells()
        {
            if (_mapCacheHub != null)
                return _mapCacheHub.Topology.EnumerateOccupiedCells();

            return EnumerateOccupiedCellsFromAnchorDict();
        }

        IEnumerable<(int x, int z, int y)> EnumerateOccupiedCellsFromAnchorDict()
        {
            foreach (var pos in tiles.Keys)
                yield return (pos.x, pos.z, pos.y);
        }

        public void ForEachRuntimeTileMutating(Action<TileData> visit)
        {
            if (visit == null)
                return;

            CopyRuntimeTilesTo(_forEachRuntimeScratch);
            for (int i = 0; i < _forEachRuntimeScratch.Count; i++)
                visit(_forEachRuntimeScratch[i]);
        }

        void CopyRuntimeTilesTo(List<TileData> dst)
        {
            dst.Clear();
            foreach (var list in tiles.Values)
                dst.AddRange(list);

            foreach (var kv in _faceBinder.WallFaceIndex)
                dst.Add(kv.Value);

            foreach (var kv in _faceBinder.FloorFaceIndex)
                dst.Add(kv.Value);
        }

        public void PatchTileIdentity(Guid tileDefId, int buildingId, int roomId)
        {
            if (!TryFindTileById(tileDefId, out var tile))
                return;

            var updated = new TileData
            {
                tileDefId = tile.tileDefId,
                state = tile.state,
                identity = CopyIdentity(tile.identity, buildingId, roomId),
            };

            if (TileIdentityUtil.IsFaceSlot(tile.identity))
            {
                if (_faceBinder.TryReplaceTileData(updated))
                {
                    IndexTile(updated);
                    MarkTilesDirty();
                }
                return;
            }

            Vector3Int pos = tile.identity.GridPos;
            if (!tiles.TryGetValue(pos, out var list))
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].tileDefId != tileDefId)
                    continue;

                list[i] = updated;
                IndexTile(updated);
                MarkTilesDirty();
                return;
            }
        }

        /// <summary>런타임 원본(tiles·edge)에서 ID 인덱스를 전부 다시 채웁니다.</summary>
        internal void ReindexTilesByIdFromRuntime()
        {
            _tilesById.Clear();
            CopyRuntimeTilesTo(_forEachRuntimeScratch);
            for (int i = 0; i < _forEachRuntimeScratch.Count; i++)
                IndexTile(_forEachRuntimeScratch[i]);
        }

        private static TileData WithIdentity(in TileData tile, in TileIdentity identity) =>
            new TileData
            {
                tileDefId = tile.tileDefId,
                state = tile.state,
                identity = identity,
            };

        private static TileIdentity CopyIdentity(in TileIdentity id, int buildingId, int roomId) =>
            new TileIdentity
            {
                PrefabId = id.PrefabId,
                GridPos = id.GridPos,
                sizeUnit = id.sizeUnit,
                placementSlot = id.placementSlot,
                wallFace = id.wallFace,
                floorFace = id.floorFace,
                buildingId = buildingId,
                roomId = roomId,
                collisionFlags = id.collisionFlags,
            };

        private void SetFloorFaceTile(TileData tileData)
        {
            if (!TileIdentityUtil.IsValidHorizontalFaceIdentity(tileData.identity))
            {
                Debug.LogError(
                    $"[TileMapModel] HorizontalFace tile '{tileData.identity.PrefabId}' requires floorFace=PosY and walkable GridPos. Skipped.");
                return;
            }

            if (_faceBinder.TryGetFloorFace(FloorFaceKey.FromFloorTileIdentity(tileData.identity), out var previous))
            {
                OnRuntimeTileRemoved?.Invoke(previous);
                _tilesById.Remove(previous.tileDefId);
            }

            _faceBinder.Register(tileData);
            IndexTile(tileData);
            _isDirty = true;
            OnRuntimeTileAdded?.Invoke(tileData);

            _changedCellsBuffer.Clear();
            TileIdentityUtil.CollectAffectedCells(tileData.identity, _changedCellsBuffer);
            foreach (var cell in _changedCellsBuffer)
                NotifyCell(cell);
        }

        private void SetWallFaceTile(TileData tileData)
        {
            if (_faceBinder.TryGetWallFace(WallEdgeKey.FromWallTileIdentity(tileData.identity), out var previous))
            {
                OnRuntimeTileRemoved?.Invoke(previous);
                _tilesById.Remove(previous.tileDefId);
            }

            _faceBinder.Register(tileData);
            IndexTile(tileData);
            _isDirty = true;
            OnRuntimeTileAdded?.Invoke(tileData);

            _changedCellsBuffer.Clear();
            TileIdentityUtil.CollectAffectedCells(tileData.identity, _changedCellsBuffer);
            foreach (var cell in _changedCellsBuffer)
                NotifyCell(cell);
        }

        private void SetCellTile(TileData tileData)
        {
            Vector3Int pos = tileData.identity.GridPos;
            if (!tiles.TryGetValue(pos, out var list))
            {
                tiles[pos] = new List<TileData> { tileData };
                IndexTile(tileData);
                _isDirty = true;
                OnRuntimeTileAdded?.Invoke(tileData);
                NotifyCell(pos);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].tileDefId != tileData.tileDefId)
                    continue;

                list[i] = tileData;
                IndexTile(tileData);
                _isDirty = true;
                NotifyCell(pos);
                return;
            }

            list.Add(tileData);
            IndexTile(tileData);
            _isDirty = true;
            OnRuntimeTileAdded?.Invoke(tileData);
            NotifyCell(pos);
        }

        public void Initialize(MapModelDTO prepared)
        {
            tiles.Clear();
            _faceBinder.Clear();
            _tilesById.Clear();

            foreach (var kv in prepared.TilesData)
            {
                switch (TileIdentityUtil.GetPlacementSlot(kv.identity))
                {
                    case TilePlacementSlot.VerticalFace:
                        _faceBinder.Register(kv);
                        IndexTile(kv);
                        break;
                    case TilePlacementSlot.HorizontalFace:
                    {
                        if (!TileIdentityUtil.IsValidHorizontalFaceIdentity(kv.identity))
                        {
                            Debug.LogError(
                                $"[TileMapModel] Invalid HorizontalFace '{kv.identity.PrefabId}' in prepared data. Skipped.");
                            break;
                        }

                        _faceBinder.Register(kv);
                        IndexTile(kv);
                        break;
                    }
                    default:
                        if (!tiles.ContainsKey(kv.identity.GridPos))
                            tiles[kv.identity.GridPos] = new List<TileData>();

                        tiles[kv.identity.GridPos].Add(kv);
                        IndexTile(kv);
                        break;
                }
            }

            _isDirty = true;
            _mapCacheHub?.Topology.RebuildOccupancy();
            _occlusionFinder = new WallOcclusionFinder(tiles, _faceBinder.WallFaceIndex, _mapCacheHub?.Topology, this);
            _hiddenWallTileIds.Clear();
            _hiddenWallTileCache.Clear();
            _occlusionWallEntries.Clear();
            _lastAppliedOcclusion.Clear();
            _hasLastOcclusionPlayerCell = false;
            if (_buildingGroupBuilder == null)
                _mapCacheHub?.InvalidateAll();
            else
                _buildingGroupBuilder.AssignAll();
        }

        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            _occlusionFinder ??= new WallOcclusionFinder(tiles, _faceBinder.WallFaceIndex, _mapCacheHub?.Topology, this);
            return _occlusionFinder.Find(playerCellPos);
        }

        /// <summary>BFS 결과 집합만 갱신하고 거리 occlusion을 채운 뒌 API(호환용). 월드 기반 갱신은 <see cref="UpdateOcclusionFromPlayerWorld"/>를 쓰세요.</summary>
        public void HideOcclusionTileWall(Vector3Int playerCellPos)
        {
            var settings = OcclusionProximitySettings.DefaultUnity;
            Vector3 world = TileHelper.ConvertGridToWorldPos(playerCellPos, settings.CellSize);
            UpdateOcclusionFromPlayerWorld(world, settings);
        }

        /// <summary>적용 중인 벽 캐릭터 오클루전을 모두 해제하고 presentation delta를 emit합니다.</summary>
        public void ClearWallCharacterOcclusion()
        {
            if (_hiddenWallTileIds.Count == 0 && _lastAppliedOcclusion.Count == 0)
                return;

            _occlusionDeltaApply.Clear();
            _occlusionDeltaClear.Clear();

            foreach (Guid hiddenId in _hiddenWallTileIds)
                _occlusionDeltaClear.Add(hiddenId);

            _hiddenWallTileIds.Clear();
            _hiddenWallTileCache.Clear();
            _occlusionWallEntries.Clear();
            _lastAppliedOcclusion.Clear();
            _hasLastOcclusionPlayerCell = false;
            RaiseOcclusionPresentationDelta();
        }

        /// <summary>호환: visibility world만 있을 때 evaluate 셀을 hub에서 해석합니다.</summary>
        public void UpdateOcclusionFromPlayerWorld(Vector3 visibilityWorld, OcclusionProximitySettings settings)
        {
            float cs = Mathf.Max(1e-4f, settings.CellSize);
            Vector3Int evaluationCell = _mapCacheHub != null
                ? OccupiedCellCoord.ResolveFromWorld(_mapCacheHub, visibilityWorld, cs)
                : TileHelper.ConvertWorldToGrid(visibilityWorld, cs);
            UpdateOcclusionFromPlayerWorld(visibilityWorld, evaluationCell, settings);
        }

        /// <inheritdoc cref="UpdateOcclusionFromPlayerWorld(Vector3, OcclusionProximitySettings)"/>
        public void UpdateOcclusionFromPlayerWorld(
            Vector3 visibilityWorld,
            int playerFloorCellY,
            OcclusionProximitySettings settings)
        {
            UpdateOcclusionFromPlayerWorld(
                visibilityWorld, playerFloorCellY, settings, occlusionTileVisible: null);
        }

        /// <summary>호환: floor Y + policy filter. evaluate 셀은 hub에서 해석합니다.</summary>
        public void UpdateOcclusionFromPlayerWorld(
            Vector3 visibilityWorld,
            int playerFloorCellY,
            OcclusionProximitySettings settings,
            Func<TileData, bool> occlusionTileVisible,
            Vector3Int playerFootprint = default)
        {
            float cs = Mathf.Max(1e-4f, settings.CellSize);
            Vector3Int evaluationCell = _mapCacheHub != null
                ? OccupiedCellCoord.ResolveFromWorld(
                    _mapCacheHub, visibilityWorld, cs, playerFloorCellY, 0f)
                : new Vector3Int(
                    TileHelper.ConvertWorldToGrid(visibilityWorld, cs).x,
                    playerFloorCellY,
                    TileHelper.ConvertWorldToGrid(visibilityWorld, cs).z);
            UpdateOcclusionFromPlayerWorld(
                visibilityWorld, evaluationCell, settings, occlusionTileVisible, playerFootprint);
        }

        /// <summary>
        /// <paramref name="evaluationCell"/>는 <see cref="PlayerVisibilityWorldResolve"/> SSOT.
        /// room·야외·BFS는 이 셀만 사용합니다 (<c>GetVisitedForWorld</c> / 판정용 <c>ConvertWorldToGrid</c> 금지).
        /// </summary>
        public void UpdateOcclusionFromPlayerWorld(
            Vector3 visibilityWorld,
            Vector3Int evaluationCell,
            OcclusionProximitySettings settings,
            Func<TileData, bool> occlusionTileVisible = null,
            Vector3Int playerFootprint = default)
        {
            _occlusionTileVisible = occlusionTileVisible;
            NormalizeProximity(ref settings);

            Vector3Int playerCell = evaluationCell;
            int playerFloorCellY = evaluationCell.y;

            if (_mapCacheHub != null)
            {
                if (_mapCacheHub.IsOutdoorEvaluation(
                        evaluationCell.y, evaluationCell.x, evaluationCell.z))
                {
                    if (_hiddenWallTileIds.Count > 0 || _lastAppliedOcclusion.Count > 0)
                        ClearWallCharacterOcclusion();
                    _hasLastOcclusionPlayerCell = false;
                    return;
                }
            }

            bool needRebuild = !_hasLastOcclusionPlayerCell || playerCell != _lastOcclusionPlayerCell;
            if (!needRebuild && _occlusionTileVisible != null)
            {
                foreach (Guid hiddenId in _hiddenWallTileIds)
                {
                    if (!_tilesById.TryGetValue(hiddenId, out TileData hiddenTile))
                        continue;

                    if (!_occlusionTileVisible(hiddenTile))
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                RebuildOcclusionMembership(playerCell, visibilityWorld, playerFloorCellY, settings, playerFootprint);
                _hasLastOcclusionPlayerCell = true;
                _lastOcclusionPlayerCell = playerCell;
            }

            RefreshOcclusionProximity(visibilityWorld, settings);
        }

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tileList)
        {
            if (tiles.TryGetValue(pos, out var list))
            {
                tileList = list;
                return true;
            }

            tileList = null;
            return false;
        }

        private void NotifyBuildingTopologyChanged(
            HashSet<Vector3Int> changedCells,
            bool isRemoval = false,
            TileData removedTile = default,
            bool immediateTopologyBake = false)
        {
            if (_mapCacheHub != null)
            {
                _mapCacheHub.NotifyTopologyChanged(
                    changedCells,
                    _buildingGroupBuilder,
                    isRemoval,
                    removedTile,
                    immediateTopologyBake);
                return;
            }

            if (_buildingGroupBuilder != null)
            {
                if (isRemoval)
                    _buildingGroupBuilder.HandleRemoveTile(removedTile, changedCells);
                else
                    _buildingGroupBuilder.HandleSetOrApply(changedCells);
            }
        }

        private bool TryFindTileById(Guid tileId, out TileData tileData) =>
            _tilesById.TryGetValue(tileId, out tileData);

        private static void IndexTile(in TileData tile, Dictionary<Guid, TileData> index) =>
            index[tile.tileDefId] = tile;

        private void IndexTile(in TileData tile) => IndexTile(tile, _tilesById);

        /// <inheritdoc cref="IMapModel.ApplyTileStates"/>
        /// <remarks>프레젠테이션(오클루전·고스트·선택)은 <see cref="TileViewPresentationApplier"/> 경로만 사용합니다.</remarks>
        public void ApplyTileStates(IReadOnlyList<TileData> tileList)
        {
        }

        /// <inheritdoc cref="IMapModel.ApplyTiles"/>
        public void ApplyTiles(IReadOnlyList<TileData> tileList)
        {
            if (tileList == null || tileList.Count == 0)
                return;

            if (!MergeTilesIntoRuntime(tileList, _changedCellsBuffer))
                return;

            _isDirty = true;
            NotifyBuildingTopologyChanged(_changedCellsBuffer, immediateTopologyBake: true);
            OnRuntimeBatchChanged?.Invoke(_changedCellsBuffer);
        }

        /// <summary>런타임 딕셔너리에 타일을 반영하고 변경된 셀을 수집합니다.</summary>
        private bool MergeTilesIntoRuntime(IReadOnlyList<TileData> tileList, HashSet<Vector3Int> changedCells)
        {
            changedCells.Clear();

            for (int t = 0; t < tileList.Count; t++)
            {
                TileData tile = tileList[t];

                if (TileIdentityUtil.IsFaceSlot(tile.identity))
                {
                    if (TileIdentityUtil.IsHorizontalFace(tile.identity) &&
                        !TileIdentityUtil.IsValidHorizontalFaceIdentity(tile.identity))
                    {
                        Debug.LogError(
                            $"[TileMapModel] Invalid HorizontalFace '{tile.identity.PrefabId}' during replace. Skipped.");
                        continue;
                    }

                    if (!_faceBinder.TryReplaceTileData(tile))
                        continue;

                    IndexTile(tile);
                    TileIdentityUtil.CollectAffectedCells(tile.identity, changedCells);
                    continue;
                }

                Vector3Int pos = tile.identity.GridPos;

                if (!tiles.TryGetValue(pos, out var existingList))
                    continue;

                for (int i = 0; i < existingList.Count; i++)
                {
                    if (existingList[i].tileDefId != tile.tileDefId)
                        continue;

                    existingList[i] = tile;
                    IndexTile(tile);
                    break;
                }

                changedCells.Add(pos);
            }

            return changedCells.Count > 0;
        }

        private static bool IdentityEquals(in TileIdentity a, in TileIdentity b) =>
            a.PrefabId == b.PrefabId &&
            a.GridPos == b.GridPos &&
            a.sizeUnit == b.sizeUnit &&
            a.placementSlot == b.placementSlot &&
            a.wallFace == b.wallFace &&
            a.floorFace == b.floorFace &&
            a.buildingId == b.buildingId &&
            a.roomId == b.roomId &&
            a.collisionFlags == b.collisionFlags;

        private static void NormalizeProximity(ref OcclusionProximitySettings s)
        {
            if (s.OcclusionFullWithinDistance > s.OcclusionNoneBeyondDistance)
            {
                (s.OcclusionFullWithinDistance, s.OcclusionNoneBeyondDistance) =
                    (s.OcclusionNoneBeyondDistance, s.OcclusionFullWithinDistance);
            }

            float minSpan = 1e-3f;
            if (Mathf.Abs(s.OcclusionNoneBeyondDistance - s.OcclusionFullWithinDistance) < minSpan)
                s.OcclusionNoneBeyondDistance = s.OcclusionFullWithinDistance + minSpan;

            if (s.ApplyEpsilon < 0f)
                s.ApplyEpsilon = 0f;
        }

        private void InvalidateOcclusionPlayerTracking() =>
            _hasLastOcclusionPlayerCell = false;

        private void RebuildOcclusionMembership(
            Vector3Int playerCellPos,
            Vector3 playerWorld,
            int playerFloorCellY,
            OcclusionProximitySettings settings,
            Vector3Int playerFootprint)
        {
            float cs = Mathf.Max(1e-4f, settings.CellSize);
            HashSet<(int x, int z)> roomVisited = null;
            if (_mapCacheHub != null)
            {
                roomVisited = _mapCacheHub.GetVisitedForCell(
                    playerFloorCellY, playerCellPos.x, playerCellPos.z, FloorRoomBfsProfile.Occlusion);
            }

            _occlusionFinder ??= new WallOcclusionFinder(tiles, _faceBinder.WallFaceIndex, _mapCacheHub?.Topology, this);
            _occlusionFinder.MaskOptions = _occlusionFinder.MaskOptions.WithEnabled(settings.PlayerProximityMaskEnabled);

            _playerOccupiedCellsBuffer.Clear();
            CharacterOccupiedCellUtil.AppendOccupiedCells(
                playerCellPos,
                playerFootprint == Vector3Int.zero
                    ? CharacterGridFootprintDefaults.Default
                    : playerFootprint,
                _playerOccupiedCellsBuffer);

            OcclusionSelection batch = _occlusionFinder.FindOcclusion(
                playerCellPos,
                roomVisited,
                _playerOccupiedCellsBuffer);
            var currentHiddenIds = new HashSet<Guid>();
            _occlusionDeltaApply.Clear();
            _occlusionDeltaClear.Clear();

            var list = batch.FinalOccluding;

            for (int i = 0; i < list.Count; i++)
            {
                TileData wall = list[i];
                if (!IsOcclusionTileVisible(wall))
                    continue;

                currentHiddenIds.Add(wall.tileDefId);

                float occ = ComputeOcclusionStrength(playerWorld, wall.identity, cs, settings);

                _occlusionDeltaApply.Add((wall.tileDefId, occ));
                _hiddenWallTileCache[wall.tileDefId] = wall;
                _lastAppliedOcclusion[wall.tileDefId] = occ;
            }

            foreach (Guid hiddenId in _hiddenWallTileIds)
            {
                if (currentHiddenIds.Contains(hiddenId))
                    continue;

                _occlusionDeltaClear.Add(hiddenId);
                _hiddenWallTileCache.Remove(hiddenId);
                _lastAppliedOcclusion.Remove(hiddenId);
            }

            _hiddenWallTileIds.Clear();
            foreach (Guid id in currentHiddenIds)
                _hiddenWallTileIds.Add(id);

            RebuildOcclusionWallEntries(cs);
            RaiseOcclusionPresentationDelta();
        }

        private void RebuildOcclusionWallEntries(float cellSize)
        {
            _occlusionWallEntries.Clear();
            cellSize = Mathf.Max(1e-4f, cellSize);

            foreach (Guid id in _hiddenWallTileIds)
            {
                if (!_hiddenWallTileCache.TryGetValue(id, out TileData wall) &&
                    !_tilesById.TryGetValue(id, out wall))
                    continue;

                Vector3 wallPoint = OcclusionWallWorldPoint(wall.identity, cellSize);
                _occlusionWallEntries.Add(new OcclusionWallEntry(id, wallPoint.x, wallPoint.z));
            }
        }

        private void RefreshOcclusionProximity(Vector3 playerWorld, OcclusionProximitySettings settings)
        {
            if (_occlusionWallEntries.Count == 0)
                return;

            float cs = Mathf.Max(1e-4f, settings.CellSize);
            float eps = settings.ApplyEpsilon;
            _occlusionDeltaApply.Clear();
            _occlusionDeltaClear.Clear();

            for (int i = 0; i < _occlusionWallEntries.Count; i++)
            {
                OcclusionWallEntry entry = _occlusionWallEntries[i];
                Guid id = entry.TileId;

                if (!_hiddenWallTileCache.TryGetValue(id, out TileData wall))
                {
                    if (!_tilesById.TryGetValue(id, out wall))
                        continue;

                    _hiddenWallTileCache[id] = wall;
                }

                if (!IsOcclusionTileVisible(wall))
                {
                    if (_lastAppliedOcclusion.ContainsKey(id))
                        _occlusionDeltaClear.Add(id);
                    _lastAppliedOcclusion.Remove(id);
                    _hiddenWallTileCache.Remove(id);
                    continue;
                }

                float d = Mathf.Sqrt(OcclusionDistSqXZ(playerWorld, entry.WallWorldX, entry.WallWorldZ));
                float occ = OcclusionCurve(d, settings);
                if (_lastAppliedOcclusion.TryGetValue(id, out float prev) && Mathf.Abs(occ - prev) <= eps)
                    continue;

                _occlusionDeltaApply.Add((id, occ));
                _lastAppliedOcclusion[id] = occ;
            }

            RaiseOcclusionPresentationDelta();
        }

        private void RaiseOcclusionPresentationDelta()
        {
            if (_occlusionDeltaApply.Count == 0 && _occlusionDeltaClear.Count == 0)
                return;

            var delta = new TileOcclusionPresentationDelta(_occlusionDeltaApply, _occlusionDeltaClear);
            OnTileOcclusionPresentationDelta?.Invoke(delta);
        }

        private static float ComputeOcclusionStrength(
            Vector3 playerWorld,
            TileIdentity identity,
            float cellSize,
            OcclusionProximitySettings settings)
        {
            Vector3 wallPoint = OcclusionWallWorldPoint(identity, cellSize);
            float d = Mathf.Sqrt(OcclusionDistSqXZ(playerWorld, wallPoint));
            return OcclusionCurve(d, settings);
        }

        private static float OcclusionDistSqXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float OcclusionDistSqXZ(Vector3 a, float wallX, float wallZ)
        {
            float dx = a.x - wallX;
            float dz = a.z - wallZ;
            return dx * dx + dz * dz;
        }

        private static Vector3 OcclusionWallWorldPoint(TileIdentity identity, float cellSize)
        {
            if (TileIdentityUtil.IsVerticalFace(identity))
            {
                WallEdgeKey key = WallEdgeKey.FromWallTileIdentity(identity);
                WallEdgeKey.GetWorldPose(key, cellSize, out Vector3 pose, out _);
                return pose;
            }

            Vector3 sizeF = (Vector3)identity.sizeUnit;
            Vector3 centroidOffset = (sizeF - Vector3.one) * 0.5f;
            Vector3 gridCenter = (Vector3)identity.GridPos + centroidOffset;
            return TileHelper.ConvertGridToWorldPos(gridCenter, cellSize);
        }

        private static float OcclusionCurve(float distance, OcclusionProximitySettings s) =>
            OcclusionBlendMath.DistanceToOcclusion01(
                distance,
                s.OcclusionFullWithinDistance,
                s.OcclusionNoneBeyondDistance);

        bool IsOcclusionTileVisible(in TileData tile) =>
            _occlusionTileVisible == null || _occlusionTileVisible(tile);

        private readonly struct OcclusionWallEntry
        {
            public readonly Guid TileId;
            public readonly float WallWorldX;
            public readonly float WallWorldZ;

            public OcclusionWallEntry(Guid tileId, float wallWorldX, float wallWorldZ)
            {
                TileId = tileId;
                WallWorldX = wallWorldX;
                WallWorldZ = wallWorldZ;
            }
        }
    }
}
