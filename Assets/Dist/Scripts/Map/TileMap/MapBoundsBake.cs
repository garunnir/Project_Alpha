// ============================================================
// MapBoundsBake — 저장 DTO 전 레이어 footprint → mapBounds SSOT
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public static class MapBoundsBake
    {
        /// <summary>산출 결과를 dto flat 필드에 기록한다.</summary>
        public static void ApplyToDto(MapSaveJsonDto dto)
        {
            if (dto == null)
                return;

            if (!TryComputeFromDto(dto, out MapBoundsSaveData bounds))
            {
                dto.hasMapBounds = false;
                return;
            }

            dto.hasMapBounds = bounds.hasMapBounds;
            dto.mapBoundsMinX = bounds.mapBoundsMinX;
            dto.mapBoundsMaxX = bounds.mapBoundsMaxX;
            dto.mapBoundsMinZ = bounds.mapBoundsMinZ;
            dto.mapBoundsMaxZ = bounds.mapBoundsMaxZ;
            dto.mapBoundsMinY = bounds.mapBoundsMinY;
        }

        /// <summary>저장 DTO 전 레이어 union. empty → false.</summary>
        public static bool TryComputeFromDto(MapSaveJsonDto dto, out MapBoundsSaveData bounds)
        {
            bounds = default;
            if (dto == null)
                return false;

            var cells = new HashSet<Vector3Int>();
            AccumulateTiles(dto.tiles, cells);
            AccumulateWallEdges(dto.wallEdges, cells);
            AccumulateFloorFaces(dto.floorFaces, cells, dto.schemaVersion, forFloorTiles: true);
            AccumulateFloorFaces(dto.outdoorFloorFaces, cells, dto.schemaVersion, forFloorTiles: true);
            AccumulateWallEdges(dto.outdoorWallEdges, cells);
            AccumulateTiles(dto.outdoorTiles, cells);
            AccumulateFloorFaces(dto.liquidAuthoringFaces, cells, dto.schemaVersion, forFloorTiles: false);
            AccumulateLiquidCells(dto.liquidCells, cells);
            AccumulateBuildings(dto.buildings, cells, dto.schemaVersion);

            if (cells.Count == 0)
                return false;

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;
            int minY = int.MaxValue;

            foreach (Vector3Int cell in cells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.z < minZ) minZ = cell.z;
                if (cell.z > maxZ) maxZ = cell.z;
                if (cell.y < minY) minY = cell.y;
            }

            bounds = new MapBoundsSaveData
            {
                hasMapBounds = true,
                mapBoundsMinX = minX,
                mapBoundsMaxX = maxX,
                mapBoundsMinZ = minZ,
                mapBoundsMaxZ = maxZ,
                mapBoundsMinY = minY,
            };
            return true;
        }

        /// <summary>런타임: 저장 bounds 우선, 없으면 동일 규칙으로 1회 산출.</summary>
        public static MapBounds ResolveForRuntime(MapSaveJsonDto dto)
        {
            if (dto != null && dto.hasMapBounds)
            {
                return new MapBounds(
                    dto.mapBoundsMinX,
                    dto.mapBoundsMaxX,
                    dto.mapBoundsMinZ,
                    dto.mapBoundsMaxZ,
                    dto.mapBoundsMinY);
            }

            if (TryComputeFromDto(dto, out MapBoundsSaveData computed) && computed.hasMapBounds)
            {
                return new MapBounds(
                    computed.mapBoundsMinX,
                    computed.mapBoundsMaxX,
                    computed.mapBoundsMinZ,
                    computed.mapBoundsMaxZ,
                    computed.mapBoundsMinY);
            }

            return MapBounds.Unbounded;
        }

        static void AccumulateTiles(List<TileSaveData> tiles, HashSet<Vector3Int> cells)
        {
            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
            {
                TileSaveData t = tiles[i];
                if (t == null)
                    continue;

                int sx = Mathf.Max(1, t.sizeX);
                int sy = Mathf.Max(1, t.sizeY);
                int sz = Mathf.Max(1, t.sizeZ);
                var basePos = new Vector3Int(t.x, t.y, t.z);
                TileIdentityUtil.AppendOccupiedCellBox(basePos, new Vector3Int(sx, sy, sz), cells);
            }
        }

        static void AccumulateWallEdges(List<WallEdgeSaveData> edges, HashSet<Vector3Int> cells)
        {
            if (edges == null)
                return;

            for (int i = 0; i < edges.Count; i++)
            {
                WallEdgeSaveData edge = edges[i];
                if (edge == null || string.IsNullOrEmpty(edge.prefabId))
                    continue;

                int sizeY = ResolveSizeY(edge.prefabId);
                var key = new WallEdgeKey(
                    new Vector3Int(edge.x, edge.y, edge.z),
                    (WallFace)edge.face);
                TileIdentityUtil.AppendWallIncidentCells(key, sizeY, cells);
            }
        }

        static void AccumulateFloorFaces(
            List<FloorFaceSaveData> faces,
            HashSet<Vector3Int> cells,
            int schemaVersion,
            bool forFloorTiles)
        {
            if (faces == null)
                return;

            for (int i = 0; i < faces.Count; i++)
            {
                FloorFaceSaveData face = faces[i];
                if (face == null || string.IsNullOrEmpty(face.prefabId))
                    continue;

                int sizeY = ResolveSizeY(face.prefabId);
                FloorFaceKey key = forFloorTiles
                    ? face.ToFloorFaceKeyForFloorTileSave(schemaVersion)
                    : face.ToFloorFaceKeyForLiquidAuthoring();
                TileIdentityUtil.AppendFloorIncidentCells(key, sizeY, cells);
            }
        }

        static void AccumulateLiquidCells(List<MapLiquidCellSaveData> liquidCells, HashSet<Vector3Int> cells)
        {
            if (liquidCells == null)
                return;

            for (int i = 0; i < liquidCells.Count; i++)
            {
                MapLiquidCellSaveData cell = liquidCells[i];
                if (cell == null || string.IsNullOrEmpty(cell.typeId))
                    continue;
                if (cell.level == 0 && cell.remainderMl == 0)
                    continue;

                cells.Add(new Vector3Int(cell.x, cell.y, cell.z));
            }
        }

        /// <summary>
        /// V5 <see cref="MapSaveJsonDto.buildings"/> — 피벗+로컬 → 월드 셀로 union.
        /// </summary>
        static void AccumulateBuildings(
            List<BuildingSaveData> buildings,
            HashSet<Vector3Int> cells,
            int schemaVersion)
        {
            if (buildings == null)
                return;

            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingSaveData building = buildings[i];
                if (building == null)
                    continue;

                var pivot = new Vector3Int(building.pivotX, building.pivotY, building.pivotZ);
                AccumulateTilesOffset(building.tiles, cells, pivot);
                AccumulateWallEdgesOffset(building.wallEdges, cells, pivot);
                AccumulateFloorFacesOffset(
                    building.floorFaces, cells, schemaVersion, forFloorTiles: true, pivot);
            }
        }

        static void AccumulateTilesOffset(
            List<TileSaveData> tiles,
            HashSet<Vector3Int> cells,
            Vector3Int pivot)
        {
            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
            {
                TileSaveData t = tiles[i];
                if (t == null)
                    continue;

                int sx = Mathf.Max(1, t.sizeX);
                int sy = Mathf.Max(1, t.sizeY);
                int sz = Mathf.Max(1, t.sizeZ);
                var basePos = new Vector3Int(t.x, t.y, t.z) + pivot;
                TileIdentityUtil.AppendOccupiedCellBox(basePos, new Vector3Int(sx, sy, sz), cells);
            }
        }

        static void AccumulateWallEdgesOffset(
            List<WallEdgeSaveData> edges,
            HashSet<Vector3Int> cells,
            Vector3Int pivot)
        {
            if (edges == null)
                return;

            for (int i = 0; i < edges.Count; i++)
            {
                WallEdgeSaveData edge = edges[i];
                if (edge == null || string.IsNullOrEmpty(edge.prefabId))
                    continue;

                int sizeY = ResolveSizeY(edge.prefabId);
                var key = new WallEdgeKey(
                    new Vector3Int(edge.x, edge.y, edge.z) + pivot,
                    (WallFace)edge.face);
                TileIdentityUtil.AppendWallIncidentCells(key, sizeY, cells);
            }
        }

        static void AccumulateFloorFacesOffset(
            List<FloorFaceSaveData> faces,
            HashSet<Vector3Int> cells,
            int schemaVersion,
            bool forFloorTiles,
            Vector3Int pivot)
        {
            if (faces == null)
                return;

            for (int i = 0; i < faces.Count; i++)
            {
                FloorFaceSaveData face = faces[i];
                if (face == null || string.IsNullOrEmpty(face.prefabId))
                    continue;

                int sizeY = ResolveSizeY(face.prefabId);
                FloorFaceKey key = forFloorTiles
                    ? face.ToFloorFaceKeyForFloorTileSave(schemaVersion)
                    : face.ToFloorFaceKeyForLiquidAuthoring();
                var worldKey = new FloorFaceKey(key.Anchor + pivot, key.Face);
                TileIdentityUtil.AppendFloorIncidentCells(worldKey, sizeY, cells);
            }
        }

        static int ResolveSizeY(string prefabId)
        {
            if (TilePrefabDB.TryResolveDefinitionSize(prefabId, out Vector3Int size))
                return Mathf.Max(1, size.y);
            return 1;
        }
    }
}
