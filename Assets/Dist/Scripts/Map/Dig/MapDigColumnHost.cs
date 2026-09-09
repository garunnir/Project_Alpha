// ============================================================
// MapDigColumnHost — 굴착·구조물 HP·지층·pit 가시성 무효화
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    [DisallowMultipleComponent]
    public sealed class MapDigColumnHost : MonoBehaviour
    {
        public static MapDigColumnHost Runtime { get; private set; }

        readonly Dictionary<Vector2Int, int> _columnDepthByXz = new();
        readonly Dictionary<DurabilityKey, int> _remainingHpByKey = new();

        TileMapCacheHub _hub;
        TileMapController _controller;
        TilePrefabDB _prefabDb;
        float _cellSize = 1f;
        int _stratumSeed;
        Action _onPitTopologyChanged;

        [SerializeField] StratumProfile _stratumProfile;

        public int StratumSeed => _stratumSeed;
        public float CellSize => _cellSize;

        readonly struct DurabilityKey : IEquatable<DurabilityKey>
        {
            public readonly Vector3Int Cell;
            public readonly byte Kind;

            public DurabilityKey(Vector3Int cell, byte kind)
            {
                Cell = cell;
                Kind = kind;
            }

            public bool Equals(DurabilityKey other) =>
                Cell == other.Cell && Kind == other.Kind;

            public override bool Equals(object obj) =>
                obj is DurabilityKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(Cell, Kind);
        }

        void Awake()
        {
            Runtime = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(Runtime, this))
                Runtime = null;
        }

        public void BindMapContext(
            TileMapCacheHub hub,
            float cellSize,
            TilePrefabDB prefabDb,
            TileMapController controller = null,
            StratumProfile stratumProfile = null)
        {
            _hub = hub;
            _cellSize = Mathf.Max(1e-4f, cellSize);
            _prefabDb = prefabDb;
            _controller = controller;
            if (stratumProfile != null)
                _stratumProfile = stratumProfile;
        }

        /// <summary>dig-break 후 floor visibility lastCtx 무효화 등.</summary>
        public void SetPitTopologyChangedHandler(Action handler) =>
            _onPitTopologyChanged = handler;

        public void LoadFromDto(MapSaveJsonDto dto)
        {
            _columnDepthByXz.Clear();
            _remainingHpByKey.Clear();
            _stratumSeed = dto != null ? dto.stratumSeed : 0;
            if (_stratumSeed == 0)
                _stratumSeed = UnityEngine.Random.Range(1, int.MaxValue);

            if (dto?.columnDepths != null)
            {
                for (int i = 0; i < dto.columnDepths.Count; i++)
                {
                    ColumnDepthSaveData entry = dto.columnDepths[i];
                    if (entry == null || entry.depth <= 0)
                        continue;
                    _columnDepthByXz[new Vector2Int(entry.x, entry.z)] = entry.depth;
                }
            }

            if (dto?.tileDurabilities != null)
            {
                for (int i = 0; i < dto.tileDurabilities.Count; i++)
                {
                    TileDurabilitySaveData entry = dto.tileDurabilities[i];
                    if (entry == null || entry.remainingHp <= 0)
                        continue;
                    var key = new DurabilityKey(
                        new Vector3Int(entry.x, entry.y, entry.z),
                        entry.kind);
                    _remainingHpByKey[key] = entry.remainingHp;
                }
            }
        }

        public void WriteToDto(MapSaveJsonDto dto)
        {
            if (dto == null)
                return;

            dto.stratumSeed = _stratumSeed;
            dto.schemaVersion = Mathf.Max(dto.schemaVersion, MapSaveSchema.TileDurabilityV4);

            dto.columnDepths ??= new List<ColumnDepthSaveData>();
            dto.columnDepths.Clear();
            foreach (KeyValuePair<Vector2Int, int> pair in _columnDepthByXz)
            {
                if (pair.Value <= 0)
                    continue;
                dto.columnDepths.Add(new ColumnDepthSaveData
                {
                    x = pair.Key.x,
                    z = pair.Key.y,
                    depth = pair.Value,
                });
            }

            dto.tileDurabilities ??= new List<TileDurabilitySaveData>();
            dto.tileDurabilities.Clear();
            foreach (KeyValuePair<DurabilityKey, int> pair in _remainingHpByKey)
            {
                if (pair.Value <= 0)
                    continue;
                dto.tileDurabilities.Add(new TileDurabilitySaveData
                {
                    x = pair.Key.Cell.x,
                    y = pair.Key.Cell.y,
                    z = pair.Key.Cell.z,
                    kind = pair.Key.Kind,
                    remainingHp = pair.Value,
                });
            }
        }

        /// <summary>FloorFace dig HP. maxHp는 TileDefinition breakDurability.</summary>
        public int GetOrInitFloorFaceHp(Vector3Int walkableCell, int maxHp)
        {
            maxHp = Mathf.Max(1, maxHp);
            var key = new DurabilityKey(walkableCell, TileDurabilityKind.FloorFace);
            if (_remainingHpByKey.TryGetValue(key, out int remaining))
                return Mathf.Clamp(remaining, 0, maxHp);

            _remainingHpByKey[key] = maxHp;
            return maxHp;
        }

        public int GetOrInitOccupiedHp(Vector3Int cell, int maxHp)
        {
            maxHp = Mathf.Max(1, maxHp);
            var key = new DurabilityKey(cell, TileDurabilityKind.Occupied);
            if (_remainingHpByKey.TryGetValue(key, out int remaining))
                return Mathf.Clamp(remaining, 0, maxHp);

            _remainingHpByKey[key] = maxHp;
            return maxHp;
        }

        /// <summary>저장된 FloorFace remaining만. 없으면 false (풀 HP·미개시).</summary>
        public bool TryGetFloorFaceRemaining(Vector3Int walkableCell, out int remaining) =>
            _remainingHpByKey.TryGetValue(
                new DurabilityKey(walkableCell, TileDurabilityKind.FloorFace),
                out remaining);

        /// <summary>저장된 Occupied remaining만. 없으면 false.</summary>
        public bool TryGetOccupiedRemaining(Vector3Int cell, out int remaining) =>
            _remainingHpByKey.TryGetValue(
                new DurabilityKey(cell, TileDurabilityKind.Occupied),
                out remaining);

        /// <summary>피해 적용. remaining ≤0이면 true (파괴 후보).</summary>
        public bool ApplyFloorFaceDamage(Vector3Int walkableCell, int damage, out int remaining)
        {
            remaining = 0;
            if (damage <= 0)
                return false;

            var key = new DurabilityKey(walkableCell, TileDurabilityKind.FloorFace);
            if (!_remainingHpByKey.TryGetValue(key, out remaining))
                return false;

            remaining -= damage;
            if (remaining > 0)
            {
                _remainingHpByKey[key] = remaining;
                TileDamagePresentation.RefreshFloorFace(walkableCell);
                return false;
            }

            _remainingHpByKey.Remove(key);
            remaining = 0;
            TileDamagePresentation.RefreshFloorFace(walkableCell);
            return true;
        }

        public bool ApplyOccupiedDamage(Vector3Int cell, int damage, out int remaining)
        {
            remaining = 0;
            if (damage <= 0)
                return false;

            var key = new DurabilityKey(cell, TileDurabilityKind.Occupied);
            if (!_remainingHpByKey.TryGetValue(key, out remaining))
                return false;

            remaining -= damage;
            if (remaining > 0)
            {
                _remainingHpByKey[key] = remaining;
                TileDamagePresentation.RefreshOccupied(cell);
                return false;
            }

            _remainingHpByKey.Remove(key);
            remaining = 0;
            TileDamagePresentation.RefreshOccupied(cell);
            return true;
        }

        public void ClearFloorFaceHp(Vector3Int walkableCell)
        {
            _remainingHpByKey.Remove(new DurabilityKey(walkableCell, TileDurabilityKind.FloorFace));
            TileDamagePresentation.RefreshFloorFace(walkableCell);
        }

        public void ClearOccupiedHp(Vector3Int cell)
        {
            _remainingHpByKey.Remove(new DurabilityKey(cell, TileDurabilityKind.Occupied));
            TileDamagePresentation.RefreshOccupied(cell);
        }
        public bool TryBreakDigTarget(DigTileTarget target, out string brokenPrefabId)
        {
            brokenPrefabId = null;
            if (_controller == null || _hub == null)
                return false;

            return target.BreakKind switch
            {
                DigBreakKind.HorizontalFace => TryBreakHorizontalFace(target.WalkableCell, out brokenPrefabId),
                DigBreakKind.WalkableStratumBlock => TryBreakStratumBlock(
                    target.WalkableCell,
                    target.BlockAnchorCell,
                    out brokenPrefabId),
                _ => false,
            };
        }

        public bool TryBreakFloor(Vector3Int walkableCell, out string brokenPrefabId) =>
            TryBreakHorizontalFace(walkableCell, out brokenPrefabId);

        bool TryBreakHorizontalFace(Vector3Int walkableCell, out string brokenPrefabId)
        {
            brokenPrefabId = null;
            if (_controller == null || _hub == null)
                return false;

            Vector3Int cellBelow = walkableCell + Vector3Int.down;
            if (!_hub.TryGetHorizontalFaceBetween(cellBelow, walkableCell, out TileData faceTile))
                return false;

            TileDefinition definition = ResolveDefinition(faceTile.identity.PrefabId);
            if (!TileFlags.IsDiggableTarget(definition))
                return false;

            brokenPrefabId = faceTile.identity.PrefabId;
            _controller.RemoveAndFlush(faceTile);
            ClearFloorFaceHp(walkableCell);

            TrySpawnStratumBelow(walkableCell);
            _onPitTopologyChanged?.Invoke();
            return true;
        }

        bool TryBreakStratumBlock(
            Vector3Int walkableCell,
            Vector3Int blockAnchor,
            out string brokenPrefabId)
        {
            brokenPrefabId = null;
            if (_controller == null || _hub == null)
                return false;

            if (!MapDigTerrainUtil.TryGetSupportBlockAtAnchor(
                    _hub,
                    blockAnchor,
                    out TileData blockTile,
                    out TileDefinition definition))
            {
                return false;
            }

            if (!TileFlags.IsDiggableTarget(definition))
                return false;

            brokenPrefabId = blockTile.identity.PrefabId;
            _controller.RemoveAndFlush(blockTile);
            ClearOccupiedHp(blockAnchor);

            TrySpawnStratumBelow(walkableCell);
            _onPitTopologyChanged?.Invoke();
            return true;
        }

        void TrySpawnStratumBelow(Vector3Int walkableCell)
        {
            Vector3Int belowWalkable = walkableCell + Vector3Int.down;
            if (HasWalkableSupport(belowWalkable))
                return;

            int depth = IncrementColumnDepth(walkableCell.x, walkableCell.z);
            string generatedPrefabId = StratumGenerator.PickStratumPrefabId(
                _stratumSeed,
                walkableCell.x,
                walkableCell.z,
                depth,
                _stratumProfile);

            if (TryGetDefinition(generatedPrefabId, out TileDefinition blockDef))
                _controller.TryPlaceStratumBlock(belowWalkable, blockDef);
        }

        bool HasWalkableSupport(Vector3Int walkableCell) =>
            _hub != null &&
            _hub.CellHasFloor(walkableCell.x, walkableCell.y, walkableCell.z);

        /// <summary>원거리 Obstructed 등 — Occupied solid wall 타일 제거.</summary>
        public bool TryBreakOccupiedTile(Vector3Int cell, out string brokenPrefabId)
        {
            brokenPrefabId = null;
            if (_controller == null || _hub == null)
                return false;

            if (!_hub.TryGetCellTiles(cell.x, cell.z, cell.y, out List<TileData> tiles) ||
                tiles == null ||
                tiles.Count == 0)
            {
                return false;
            }

            TileData wall = default;
            bool found = false;
            for (int i = 0; i < tiles.Count; i++)
            {
                TileData tile = tiles[i];
                TileDefinition def = ResolveDefinition(tile.identity.PrefabId);
                if (def == null || !def.occupied.blocksOccupiedCells)
                    continue;
                wall = tile;
                found = true;
                break;
            }

            if (!found)
                return false;

            brokenPrefabId = wall.identity.PrefabId;
            _controller.RemoveAndFlush(wall);
            ClearOccupiedHp(cell);
            _onPitTopologyChanged?.Invoke();
            return true;
        }

        public Vector3 CellWorld(Vector3Int walkableCell) =>
            TileHelper.ConvertGridToWorldPos(walkableCell, _cellSize);

        int IncrementColumnDepth(int x, int z)
        {
            var key = new Vector2Int(x, z);
            if (!_columnDepthByXz.TryGetValue(key, out int depth))
                depth = 0;

            depth++;
            _columnDepthByXz[key] = depth;
            return depth;
        }

        TileDefinition ResolveDefinition(string prefabId)
        {
            if (TryGetDefinition(prefabId, out TileDefinition def))
                return def;
            return null;
        }

        bool TryGetDefinition(string prefabId, out TileDefinition def)
        {
            def = null;
            if (_prefabDb != null && _prefabDb.TryGetDefinition(prefabId, out def))
                return def != null;

            return TilePrefabDB.TryResolveDefinition(prefabId, out def);
        }
    }
}
