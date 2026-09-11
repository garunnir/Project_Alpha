// ============================================================
// TileMapDtoMapper — MapSaveJsonDto ↔ MapModelDTO (schema V5 outdoor/buildings)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

namespace IsoTilemap
{
    public class TileMapDtoMapper : IMapMapper
    {
        /// <summary>
        /// buildingId ≤ 0 · 부모 파싱 실패 타일을 임시로 묶는 음수 키 시작.
        /// 양수 id·서로 다른 부모와 한 blob(buildingId:0)으로 몰아넣지 않음.
        /// </summary>
        const int OrphanBuildingGroupKeyStart = -1;

        public MapModelDTO ToPrepared(MapSaveJsonDto tileMapData)
        {
            if (tileMapData == null || tileMapData.tiles == null)
            {
                Debug.LogWarning("TileMapData or its tiles are null.");
                return null;
            }

            List<TileData> prepareData = new List<TileData>();
            bool legacyTiles = tileMapData.schemaVersion < MapSaveSchema.PlacementSlotV1;
            int schemaVersion = tileMapData.schemaVersion;
            bool schemaV5 = schemaVersion >= MapSaveSchema.OutdoorStructureLayersV5;

            AppendRootOccupiedAndWalls(tileMapData, prepareData, legacyTiles);

            List<FloorFaceSaveData> outdoorFaces;
            List<FloorFaceSaveData> flatStructureFaces;
            if (schemaV5)
            {
                outdoorFaces = tileMapData.outdoorFloorFaces;
                flatStructureFaces = null;
            }
            else
                ClassifyLegacyFloorFaces(tileMapData.floorFaces, out outdoorFaces, out flatStructureFaces);

            AppendFloorFacesWorld(outdoorFaces, schemaVersion, prepareData, replaceExistingFloor: false, asOutdoor: true);
            AppendFloorFacesWorld(flatStructureFaces, schemaVersion, prepareData, replaceExistingFloor: true, asOutdoor: false);

            if (schemaV5)
            {
                AppendWallEdgesWorld(tileMapData.outdoorWallEdges, prepareData, asOutdoor: true);
                AppendOccupiedWorld(tileMapData.outdoorTiles, prepareData, legacyTiles, asOutdoor: true);
            }

            if (schemaV5 && tileMapData.buildings != null)
            {
                for (int i = 0; i < tileMapData.buildings.Count; i++)
                    AppendBuildingWorld(
                        tileMapData.buildings[i],
                        schemaVersion,
                        prepareData,
                        legacyTiles,
                        buildingIndexFallback: i);
            }

            MapFishTrapSaveBuffer.QueueLoadRecords(tileMapData.tiles);

            return new MapModelDTO(prepareData);
        }

        public MapSaveJsonDto FromPrepared(MapModelDTO prepared)
        {
            IReadOnlyList<TileData> tiles = prepared.TilesData;
            var dto = new MapSaveJsonDto { schemaVersion = MapSaveSchema.Current };

            // 물은 타일 모델에 없으므로 여기서 복원할 수 없다. null = "저작 레이어 미지정" —
            // 씬 마커를 읽은 호출부(MapFileSaver)가 채우고, 아니면 MapSaveLayerCarryOver가 디스크를 계승한다.
            dto.liquidAuthoringFaces = null;

            // V5: flat floorFaces/wallEdges는 비움. tiles는 fish-trap only용으로 유지.
            dto.floorFaces = new List<FloorFaceSaveData>();
            dto.wallEdges = new List<WallEdgeSaveData>();
            dto.outdoorFloorFaces = new List<FloorFaceSaveData>();
            dto.outdoorWallEdges = new List<WallEdgeSaveData>();
            dto.outdoorTiles = new List<TileSaveData>();
            dto.buildings = new List<BuildingSaveData>();

            var groups = new Dictionary<int, BuildingGroupScratch>();

            foreach (var ti in tiles)
            {
                if (ti.identity.buildingId == TileIdentity.BuildingIdOutdoor)
                {
                    AppendTileToOutdoorDto(dto, ti);
                    continue;
                }

                switch (TileIdentityUtil.GetPlacementSlot(ti.identity))
                {
                    case TilePlacementSlot.VerticalFace:
                        AddWallToGroup(groups, ResolveBuildingGroupKey(ti.identity.buildingId), ti);
                        break;
                    case TilePlacementSlot.HorizontalFace:
                        AddFloorToGroup(groups, ResolveBuildingGroupKey(ti.identity.buildingId), ti);
                        break;
                    default:
                        AddOccupiedToGroup(groups, ResolveBuildingGroupKey(ti.identity.buildingId), ti);
                        break;
                }
            }

            int nextOrphanSaveId = MaxPositiveBuildingKey(groups) + 1;
            foreach (var kv in groups)
            {
                int saveId = kv.Key > 0 ? kv.Key : nextOrphanSaveId++;
                dto.buildings.Add(FinalizeBuildingSave(saveId, kv.Value));
            }

            MapFishTrapSaveBuffer.AppendSaveRecords(dto.tiles);

            return dto;
        }

        /// <summary>
        /// 뷰 하이어라키 스냅샷 저장 (remesh 없음).
        /// <c>Outdoor/</c> → outdoor*; <c>Building_{id}</c> → buildings[].
        /// 합침은 bake가 하이어라키를 맞춘 뒤에만 반영된다.
        /// </summary>
        public MapSaveJsonDto FromHierarchyViews(IEnumerable<TileView> views)
        {
            var dto = new MapSaveJsonDto { schemaVersion = MapSaveSchema.Current };
            dto.liquidAuthoringFaces = null;
            dto.floorFaces = new List<FloorFaceSaveData>();
            dto.wallEdges = new List<WallEdgeSaveData>();
            dto.outdoorFloorFaces = new List<FloorFaceSaveData>();
            dto.outdoorWallEdges = new List<WallEdgeSaveData>();
            dto.outdoorTiles = new List<TileSaveData>();
            dto.buildings = new List<BuildingSaveData>();
            dto.tiles = new List<TileSaveData>();

            if (views == null)
                return dto;

            var byBuilding = new Dictionary<int, List<TileData>>();

            foreach (TileView view in views)
            {
                if (view == null)
                    continue;

                List<TileData> one = TileViewSceneGather.BuildTileDataSnapshot(new[] { view });
                if (one == null || one.Count == 0)
                    continue;

                TileData data = one[0];
                Transform parent = view.transform.parent;

                if (BuildingViewHierarchy.IsUnderOutdoorRoot(parent))
                {
                    AppendTileToOutdoorDto(dto, data);
                    continue;
                }

                int buildingKey = ResolveHierarchyBuildingGroupKey(parent, data.identity.buildingId);
                if (!byBuilding.TryGetValue(buildingKey, out List<TileData> list))
                {
                    list = new List<TileData>();
                    byBuilding[buildingKey] = list;
                }

                list.Add(data);
            }

            var groups = new Dictionary<int, BuildingGroupScratch>();
            foreach (var kv in byBuilding)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    TileData ti = kv.Value[i];
                    switch (TileIdentityUtil.GetPlacementSlot(ti.identity))
                    {
                        case TilePlacementSlot.VerticalFace:
                            AddWallToGroup(groups, kv.Key, ti);
                            break;
                        case TilePlacementSlot.HorizontalFace:
                            AddFloorToGroup(groups, kv.Key, ti);
                            break;
                        default:
                            AddOccupiedToGroup(groups, kv.Key, ti);
                            break;
                    }
                }
            }

            int nextOrphanSaveId = MaxPositiveBuildingKey(groups) + 1;
            foreach (var kv in groups)
            {
                int saveId = kv.Key > 0 ? kv.Key : nextOrphanSaveId++;
                dto.buildings.Add(FinalizeBuildingSave(saveId, kv.Value));
            }

            MapFishTrapSaveBuffer.AppendSaveRecords(dto.tiles);
            return dto;
        }

        static void AppendTileToOutdoorDto(MapSaveJsonDto dto, in TileData ti)
        {
            Vector3Int p = ti.identity.GridPos;
            switch (TileIdentityUtil.GetPlacementSlot(ti.identity))
            {
                case TilePlacementSlot.VerticalFace:
                    dto.outdoorWallEdges.Add(new WallEdgeSaveData
                    {
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        face = ti.identity.wallFace,
                        prefabId = ti.identity.PrefabId,
                    });
                    break;
                case TilePlacementSlot.HorizontalFace:
                    dto.outdoorFloorFaces.Add(new FloorFaceSaveData
                    {
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        face = ti.identity.floorFace,
                        prefabId = ti.identity.PrefabId,
                    });
                    break;
                default:
                    dto.outdoorTiles.Add(new TileSaveData
                    {
                        sizeX = ti.identity.sizeUnit.x,
                        sizeY = ti.identity.sizeUnit.y,
                        sizeZ = ti.identity.sizeUnit.z,
                        x = p.x,
                        y = p.y,
                        z = p.z,
                        prefabId = ti.identity.PrefabId,
                        seedItemId = ti.plant.HasSeed ? ti.plant.seedItemId : null,
                        plantedWorldMinute = ti.plant.plantedWorldMinute,
                        fertilized = ti.plant.fertilized,
                        lastFruitHarvestWorldMinute = ti.plant.lastFruitHarvestWorldMinute,
                    });
                    break;
            }
        }

        static void AppendRootOccupiedAndWalls(
            MapSaveJsonDto tileMapData,
            List<TileData> prepareData,
            bool legacyTiles)
        {
            foreach (var td in tileMapData.tiles)
            {
                if (MapFishTrapSaveBuffer.IsTrapOnlyRecord(td))
                    continue;

                TilePlacementSlot slot = ResolveOccupiedTileSlot(td, legacyTiles);

                if (slot == TilePlacementSlot.HorizontalFace)
                {
                    Debug.LogWarning(
                        $"[TileMapDtoMapper] Floor '{td.prefabId}' in tiles[] is no longer loaded. Use floorFaces[] / outdoorFloorFaces / buildings[].");
                    continue;
                }

                if (slot == TilePlacementSlot.VerticalFace)
                {
                    byte wallFace = (byte)Mathf.Clamp((int)td.face, 0, 1);
                    if (TryMakeWallFaceIdentity(
                            td.prefabId,
                            new Vector3Int(td.x, td.y, td.z),
                            wallFace,
                            out var identity))
                        prepareData.Add(MakeTile(identity));
                    continue;
                }

                if (TryMakeOccupiedIdentity(
                        td.prefabId,
                        new Vector3Int(td.x, td.y, td.z),
                        out var occupied))
                {
                    var plant = new PlantTileInstance
                    {
                        seedItemId = td.seedItemId,
                        plantedWorldMinute = td.plantedWorldMinute,
                        fertilized = td.fertilized,
                        lastFruitHarvestWorldMinute = td.lastFruitHarvestWorldMinute > PlantGrowth.NoFruitHarvestMinute
                            ? td.lastFruitHarvestWorldMinute
                            : MapPlantConsts.NoFruitHarvestMinute,
                    };
                    prepareData.Add(MakeTile(occupied, plant));
                }
            }

            if (tileMapData.wallEdges == null)
                return;

            foreach (var we in tileMapData.wallEdges)
            {
                if (TryMakeWallFaceIdentity(
                        we.prefabId,
                        new Vector3Int(we.x, we.y, we.z),
                        (byte)Mathf.Clamp((int)we.face, 0, 1),
                        out var identity))
                    prepareData.Add(MakeTile(identity));
            }
        }

        static void ClassifyLegacyFloorFaces(
            List<FloorFaceSaveData> floorFaces,
            out List<FloorFaceSaveData> outdoor,
            out List<FloorFaceSaveData> structure)
        {
            outdoor = new List<FloorFaceSaveData>();
            structure = new List<FloorFaceSaveData>();
            if (floorFaces == null)
                return;

            for (int i = 0; i < floorFaces.Count; i++)
            {
                FloorFaceSaveData ff = floorFaces[i];
                if (ff == null)
                    continue;

                if (IsOutdoorMigrationGrassFloor(ff.prefabId))
                    outdoor.Add(ff);
                else
                    structure.Add(ff);
            }
        }

        static void AppendFloorFacesWorld(
            List<FloorFaceSaveData> faces,
            int schemaVersion,
            List<TileData> prepareData,
            bool replaceExistingFloor,
            bool asOutdoor)
        {
            if (faces == null)
                return;

            for (int i = 0; i < faces.Count; i++)
            {
                FloorFaceSaveData ff = faces[i];
                if (ff == null)
                    continue;

                // 물은 타일이 아니다. 정상 경로에서는 read 시 저작 면으로 승격되어 여기 오지 않는다.
                if (MapLiquidAuthoringBake.IsLiquidAuthoringPrefab(ff.prefabId))
                {
                    Debug.LogWarning(
                        $"[TileMapDtoMapper] 물 face '{ff.prefabId}'가 floorFaces/outdoorFloorFaces에 남아 있어 무시합니다. " +
                        "liquidAuthoringFaces로 승격되었는지 확인하세요.");
                    continue;
                }

                Vector3Int walkable = ff.ResolveWalkableFromFloorFaceSave(schemaVersion);
                if (!TryMakeHorizontalFaceIdentity(ff.prefabId, walkable, out var identity))
                    continue;

                if (asOutdoor)
                {
                    identity = new TileIdentity
                    {
                        PrefabId = identity.PrefabId,
                        GridPos = identity.GridPos,
                        sizeUnit = identity.sizeUnit,
                        placementSlot = identity.placementSlot,
                        wallFace = identity.wallFace,
                        floorFace = identity.floorFace,
                        collisionFlags = identity.collisionFlags,
                        buildingId = TileIdentity.BuildingIdOutdoor,
                        roomId = 0,
                    };
                }

                if (replaceExistingFloor)
                    RemoveHorizontalFaceAt(prepareData, walkable);
                prepareData.Add(MakeTile(identity));
            }
        }

        static void AppendWallEdgesWorld(
            List<WallEdgeSaveData> edges,
            List<TileData> prepareData,
            bool asOutdoor)
        {
            if (edges == null)
                return;

            for (int i = 0; i < edges.Count; i++)
            {
                WallEdgeSaveData we = edges[i];
                if (we == null)
                    continue;

                if (!TryMakeWallFaceIdentity(
                        we.prefabId,
                        new Vector3Int(we.x, we.y, we.z),
                        (byte)Mathf.Clamp((int)we.face, 0, 1),
                        out var identity))
                    continue;

                if (asOutdoor)
                    identity = WithOutdoorBuildingId(identity);

                prepareData.Add(MakeTile(identity));
            }
        }

        static void AppendOccupiedWorld(
            List<TileSaveData> tiles,
            List<TileData> prepareData,
            bool legacyTiles,
            bool asOutdoor)
        {
            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
            {
                TileSaveData td = tiles[i];
                if (td == null || MapFishTrapSaveBuffer.IsTrapOnlyRecord(td))
                    continue;

                TilePlacementSlot slot = ResolveOccupiedTileSlot(td, legacyTiles);
                if (slot == TilePlacementSlot.HorizontalFace || slot == TilePlacementSlot.VerticalFace)
                {
                    Debug.LogWarning(
                        $"[TileMapDtoMapper] outdoorTiles entry '{td.prefabId}' has face slot — use outdoorFloorFaces / outdoorWallEdges.");
                    continue;
                }

                if (!TryMakeOccupiedIdentity(td.prefabId, new Vector3Int(td.x, td.y, td.z), out var occupied))
                    continue;

                if (asOutdoor)
                    occupied = WithOutdoorBuildingId(occupied);

                var plant = new PlantTileInstance
                {
                    seedItemId = td.seedItemId,
                    plantedWorldMinute = td.plantedWorldMinute,
                    fertilized = td.fertilized,
                    lastFruitHarvestWorldMinute = td.lastFruitHarvestWorldMinute > PlantGrowth.NoFruitHarvestMinute
                        ? td.lastFruitHarvestWorldMinute
                        : MapPlantConsts.NoFruitHarvestMinute,
                };
                prepareData.Add(MakeTile(occupied, plant));
            }
        }

        static TileIdentity WithOutdoorBuildingId(in TileIdentity identity) =>
            WithBuildingId(identity, TileIdentity.BuildingIdOutdoor);

        static TileIdentity WithBuildingId(in TileIdentity identity, int buildingId) =>
            new TileIdentity
            {
                PrefabId = identity.PrefabId,
                GridPos = identity.GridPos,
                sizeUnit = identity.sizeUnit,
                placementSlot = identity.placementSlot,
                wallFace = identity.wallFace,
                floorFace = identity.floorFace,
                collisionFlags = identity.collisionFlags,
                buildingId = buildingId,
                roomId = 0,
            };

        static void AppendBuildingWorld(
            BuildingSaveData building,
            int schemaVersion,
            List<TileData> prepareData,
            bool legacyTiles,
            int buildingIndexFallback)
        {
            if (building == null)
                return;

            int stampedBuildingId = building.buildingId > 0
                ? building.buildingId
                : buildingIndexFallback + 1;

            Vector3Int pivot = new Vector3Int(building.pivotX, building.pivotY, building.pivotZ);

            if (building.floorFaces != null)
            {
                for (int i = 0; i < building.floorFaces.Count; i++)
                {
                    FloorFaceSaveData local = building.floorFaces[i];
                    if (local == null)
                        continue;

                    if (MapLiquidAuthoringBake.IsLiquidAuthoringPrefab(local.prefabId))
                        continue;

                    Vector3Int localWalkable = local.ResolveWalkableFromFloorFaceSave(schemaVersion);
                    Vector3Int world = localWalkable + pivot;
                    if (!TryMakeHorizontalFaceIdentity(local.prefabId, world, out var identity))
                        continue;

                    // 구조물 바닥이 야외와 겹치면 구조물 우선.
                    RemoveHorizontalFaceAt(prepareData, world);
                    prepareData.Add(MakeTile(WithBuildingId(identity, stampedBuildingId)));
                }
            }

            if (building.wallEdges != null)
            {
                for (int i = 0; i < building.wallEdges.Count; i++)
                {
                    WallEdgeSaveData local = building.wallEdges[i];
                    if (local == null)
                        continue;

                    Vector3Int world = new Vector3Int(local.x, local.y, local.z) + pivot;
                    if (TryMakeWallFaceIdentity(
                            local.prefabId,
                            world,
                            (byte)Mathf.Clamp((int)local.face, 0, 1),
                            out var identity))
                        prepareData.Add(MakeTile(WithBuildingId(identity, stampedBuildingId)));
                }
            }

            if (building.tiles == null)
                return;

            for (int i = 0; i < building.tiles.Count; i++)
            {
                TileSaveData local = building.tiles[i];
                if (local == null || MapFishTrapSaveBuffer.IsTrapOnlyRecord(local))
                    continue;

                TilePlacementSlot slot = ResolveOccupiedTileSlot(local, legacyTiles);
                Vector3Int world = new Vector3Int(local.x, local.y, local.z) + pivot;

                if (slot == TilePlacementSlot.HorizontalFace)
                {
                    Debug.LogWarning(
                        $"[TileMapDtoMapper] Floor '{local.prefabId}' in buildings[].tiles is skipped. Use buildings[].floorFaces.");
                    continue;
                }

                if (slot == TilePlacementSlot.VerticalFace)
                {
                    byte wallFace = (byte)Mathf.Clamp((int)local.face, 0, 1);
                    if (TryMakeWallFaceIdentity(local.prefabId, world, wallFace, out var wallId))
                        prepareData.Add(MakeTile(WithBuildingId(wallId, stampedBuildingId)));
                    continue;
                }

                if (TryMakeOccupiedIdentity(local.prefabId, world, out var occupied))
                {
                    var plant = new PlantTileInstance
                    {
                        seedItemId = local.seedItemId,
                        plantedWorldMinute = local.plantedWorldMinute,
                        fertilized = local.fertilized,
                        lastFruitHarvestWorldMinute = local.lastFruitHarvestWorldMinute > PlantGrowth.NoFruitHarvestMinute
                            ? local.lastFruitHarvestWorldMinute
                            : MapPlantConsts.NoFruitHarvestMinute,
                    };
                    prepareData.Add(MakeTile(WithBuildingId(occupied, stampedBuildingId), plant));
                }
            }
        }

        static void RemoveHorizontalFaceAt(List<TileData> prepareData, Vector3Int walkable)
        {
            for (int i = prepareData.Count - 1; i >= 0; i--)
            {
                TileIdentity id = prepareData[i].identity;
                if (TileIdentityUtil.GetPlacementSlot(id) != TilePlacementSlot.HorizontalFace)
                    continue;
                if (id.GridPos != walkable)
                    continue;
                prepareData.RemoveAt(i);
                return;
            }
        }

        static bool IsOutdoorMigrationGrassFloor(string prefabId) =>
            string.Equals(prefabId, MapSaveSchema.OutdoorMigrationGrassFloorPrefabId, StringComparison.Ordinal);

        static int ResolveBuildingGroupKey(int buildingId) =>
            buildingId > 0 ? buildingId : OrphanBuildingGroupKeyStart;

        /// <summary>
        /// bake 후 하이어라키 스냅샷용. Building_* 부모 우선(씬 gather는 buildingId=0).
        /// </summary>
        static int ResolveHierarchyBuildingGroupKey(Transform parent, int identityBuildingId)
        {
            if (BuildingViewHierarchy.TryParseBuildingIdFromParent(parent, out int parsedId) &&
                parsedId > 0)
                return parsedId;

            if (identityBuildingId > 0)
                return identityBuildingId;

            if (parent != null)
            {
                int instanceKey = parent.GetInstanceID();
                return instanceKey < 0 ? instanceKey : -instanceKey - 1;
            }

            return OrphanBuildingGroupKeyStart;
        }

        static int MaxPositiveBuildingKey(Dictionary<int, BuildingGroupScratch> groups)
        {
            int max = 0;
            foreach (var kv in groups)
            {
                if (kv.Key > max)
                    max = kv.Key;
            }

            return max;
        }

        static void AddFloorToGroup(Dictionary<int, BuildingGroupScratch> groups, int key, in TileData ti)
        {
            BuildingGroupScratch g = GetOrCreateGroup(groups, key);
            Vector3Int p = ti.identity.GridPos;
            g.IncludeWorld(p);
            g.Floors.Add(new FloorFaceSaveData
            {
                x = p.x,
                y = p.y,
                z = p.z,
                face = ti.identity.floorFace,
                prefabId = ti.identity.PrefabId,
            });
        }

        static void AddWallToGroup(Dictionary<int, BuildingGroupScratch> groups, int key, in TileData ti)
        {
            BuildingGroupScratch g = GetOrCreateGroup(groups, key);
            Vector3Int p = ti.identity.GridPos;
            g.IncludeWorld(p);
            g.Walls.Add(new WallEdgeSaveData
            {
                x = p.x,
                y = p.y,
                z = p.z,
                face = ti.identity.wallFace,
                prefabId = ti.identity.PrefabId,
            });
        }

        static void AddOccupiedToGroup(Dictionary<int, BuildingGroupScratch> groups, int key, in TileData ti)
        {
            BuildingGroupScratch g = GetOrCreateGroup(groups, key);
            Vector3Int p = ti.identity.GridPos;
            g.IncludeWorld(p);
            g.Tiles.Add(new TileSaveData
            {
                sizeX = ti.identity.sizeUnit.x,
                sizeY = ti.identity.sizeUnit.y,
                sizeZ = ti.identity.sizeUnit.z,
                x = p.x,
                y = p.y,
                z = p.z,
                prefabId = ti.identity.PrefabId,
                seedItemId = ti.plant.HasSeed ? ti.plant.seedItemId : null,
                plantedWorldMinute = ti.plant.plantedWorldMinute,
                fertilized = ti.plant.fertilized,
                lastFruitHarvestWorldMinute = ti.plant.lastFruitHarvestWorldMinute,
            });
        }

        static BuildingGroupScratch GetOrCreateGroup(Dictionary<int, BuildingGroupScratch> groups, int key)
        {
            if (!groups.TryGetValue(key, out BuildingGroupScratch g))
            {
                g = new BuildingGroupScratch();
                groups[key] = g;
            }

            return g;
        }

        static BuildingSaveData FinalizeBuildingSave(int buildingId, BuildingGroupScratch scratch)
        {
            Vector3Int pivot = scratch.PivotMin;
            var building = new BuildingSaveData
            {
                buildingId = buildingId > 0 ? buildingId : 0,
                pivotX = pivot.x,
                pivotY = pivot.y,
                pivotZ = pivot.z,
            };

            for (int i = 0; i < scratch.Floors.Count; i++)
            {
                FloorFaceSaveData w = scratch.Floors[i];
                building.floorFaces.Add(new FloorFaceSaveData
                {
                    x = w.x - pivot.x,
                    y = w.y - pivot.y,
                    z = w.z - pivot.z,
                    face = w.face,
                    prefabId = w.prefabId,
                });
            }

            for (int i = 0; i < scratch.Walls.Count; i++)
            {
                WallEdgeSaveData w = scratch.Walls[i];
                building.wallEdges.Add(new WallEdgeSaveData
                {
                    x = w.x - pivot.x,
                    y = w.y - pivot.y,
                    z = w.z - pivot.z,
                    face = w.face,
                    prefabId = w.prefabId,
                });
            }

            for (int i = 0; i < scratch.Tiles.Count; i++)
            {
                TileSaveData w = scratch.Tiles[i];
                building.tiles.Add(new TileSaveData
                {
                    sizeX = w.sizeX,
                    sizeY = w.sizeY,
                    sizeZ = w.sizeZ,
                    x = w.x - pivot.x,
                    y = w.y - pivot.y,
                    z = w.z - pivot.z,
                    prefabId = w.prefabId,
                    seedItemId = w.seedItemId,
                    plantedWorldMinute = w.plantedWorldMinute,
                    fertilized = w.fertilized,
                    lastFruitHarvestWorldMinute = w.lastFruitHarvestWorldMinute,
                });
            }

            return building;
        }

        sealed class BuildingGroupScratch
        {
            public readonly List<FloorFaceSaveData> Floors = new List<FloorFaceSaveData>();
            public readonly List<WallEdgeSaveData> Walls = new List<WallEdgeSaveData>();
            public readonly List<TileSaveData> Tiles = new List<TileSaveData>();

            bool _hasPivot;
            Vector3Int _pivotMin;

            public Vector3Int PivotMin => _hasPivot ? _pivotMin : Vector3Int.zero;

            public void IncludeWorld(Vector3Int world)
            {
                if (!_hasPivot)
                {
                    _pivotMin = world;
                    _hasPivot = true;
                    return;
                }

                if (world.x < _pivotMin.x) _pivotMin.x = world.x;
                if (world.y < _pivotMin.y) _pivotMin.y = world.y;
                if (world.z < _pivotMin.z) _pivotMin.z = world.z;
            }
        }

        static TilePlacementSlot ResolveOccupiedTileSlot(TileSaveData td, bool legacyTiles)
        {
            if (legacyTiles)
            {
                var legacySlot = TileIdentityUtil.InferSlotFromLegacyTileType(
                    NormalizeLegacyTileType(td.tileType));
                if (legacySlot != TilePlacementSlot.None)
                    return legacySlot;
            }

            TilePrefabDB.TryResolveDefinition(td.prefabId, out var def);
            return TileIdentityUtil.ResolvePlacementSlot(def, td.prefabId);
        }

        static TileData MakeTile(in TileIdentity identity) =>
            MakeTile(identity, default);

        static TileData MakeTile(in TileIdentity identity, in PlantTileInstance plant) =>
            new TileData
            {
                tileDefId = Guid.NewGuid(),
                state = new TileState(),
                identity = identity,
                plant = plant,
            };

        static bool TryMakeHorizontalFaceIdentity(string prefabId, Vector3Int walkableGridPos, out TileIdentity identity) =>
            TryMakeIdentity(
                prefabId,
                TilePlacementSlot.HorizontalFace,
                walkableGridPos,
                wallFace: 0,
                floorFace: (byte)FloorFace.PosY,
                out identity);

        static bool TryMakeWallFaceIdentity(
            string prefabId,
            Vector3Int anchor,
            byte wallFace,
            out TileIdentity identity) =>
            TryMakeIdentity(
                prefabId,
                TilePlacementSlot.VerticalFace,
                anchor,
                wallFace,
                floorFace: 0,
                out identity);

        static bool TryMakeOccupiedIdentity(string prefabId, Vector3Int grid, out TileIdentity identity) =>
            TryMakeIdentity(
                prefabId,
                TilePlacementSlot.OccupiedCell,
                grid,
                wallFace: 0,
                floorFace: 0,
                out identity);

        static bool TryMakeIdentity(
            string prefabId,
            TilePlacementSlot slot,
            Vector3Int gridPos,
            byte wallFace,
            byte floorFace,
            out TileIdentity identity)
        {
            identity = default;
            if (!TilePrefabDB.TryResolveDefinition(prefabId, out var def) || def == null)
            {
                Debug.LogError($"[TileMapDtoMapper] Definition not found for prefabId='{prefabId}'. Tile skipped.");
                return false;
            }

            identity = new TileIdentity
            {
                PrefabId = prefabId,
                GridPos = gridPos,
                sizeUnit = new Vector3Int(
                    Mathf.Max(1, def.size.x),
                    Mathf.Max(1, def.size.y),
                    Mathf.Max(1, def.size.z)),
                placementSlot = (byte)slot,
                wallFace = wallFace,
                floorFace = slot == TilePlacementSlot.HorizontalFace ? (byte)FloorFace.PosY : floorFace,
                collisionFlags = TileCollisionProfile.FromDefinitionForSlot(slot, def),
            };

            if (slot == TilePlacementSlot.VerticalFace &&
                !TileCollisionFlagsUtil.Has(identity.collisionFlags, TileCollisionFlags.OccludesEdge))
            {
                Debug.LogWarning(
                    $"[TileMapDtoMapper] VerticalFace '{prefabId}' has no OccludesEdge flag. BFS character occlusion will skip this edge.");
            }

            return true;
        }

        static byte NormalizeLegacyTileType(byte raw)
        {
            if (raw == 3)
                return (byte)TileView.TileType.Wall;
            return raw;
        }
    }
}
