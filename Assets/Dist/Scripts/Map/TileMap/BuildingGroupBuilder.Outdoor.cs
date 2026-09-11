// ============================================================
// BuildingGroupBuilder.Outdoor — outdoor 레이어 스탬프·cellY 범위
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public sealed partial class BuildingGroupBuilder
    {
        /// <summary>
        /// 등록된 outdoor 레이어 칸에 <see cref="TileIdentity.BuildingIdOutdoor"/>를 스탬프합니다.
        /// minCellY plaza BFS는 사용하지 않습니다.
        /// </summary>
        public void StampOutdoorLayerFromRegistry()
        {
            foreach (Vector3Int cell in _registry.OutdoorFloorCells)
            {
                if (!_topology.Index.CellHasFloor(cell.x, cell.y, cell.z))
                    continue;
                SetFloorBuildingRoom(cell.x, cell.y, cell.z, TileIdentity.BuildingIdOutdoor, 0);
            }
        }

        /// <summary>
        /// 모델에서 이미 -1인 floor를 outdoor 인덱스에 합칩니다 (기존 레이어 유지).
        /// </summary>
        public void SyncOutdoorLayerFromOutdoorIdTiles()
        {
            foreach (TileData tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsFloorTile(tile.identity))
                    continue;
                if (tile.identity.buildingId != TileIdentity.BuildingIdOutdoor)
                    continue;
                _registry.AddOutdoorFloorCell(tile.identity.GridPos);
            }

            StampOutdoorLayerFromRegistry();
        }

        void ComputeCellYRange()
        {
            _minCellY = int.MaxValue;
            _maxCellY = int.MinValue;

            foreach (var tile in _model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    continue;

                int y = TileIdentityUtil.IsFloorTile(tile.identity)
                    ? FloorFaceKey.FromFloorTileIdentity(tile.identity).CellAbove.y
                    : tile.identity.GridPos.y;

                if (y < _minCellY) _minCellY = y;
                if (y > _maxCellY) _maxCellY = y;
            }

            if (_minCellY == int.MaxValue)
            {
                _minCellY = 0;
                _maxCellY = 0;
            }
        }

        void ResetStructuralIds()
        {
            _model.ForEachRuntimeTileMutating(tile =>
            {
                if (!TileIdentityUtil.IsStructural(tile.identity))
                    return;

                // Outdoor(-1) 불변 — bake·merge가 덮지 않음.
                if (BuildingIdBakeRules.IsImmutableOutdoorBuildingId(tile.identity.buildingId))
                    return;

                if (TileIdentityUtil.IsFloorTile(tile.identity) &&
                    _registry.IsOutdoorFloorCell(tile.identity.GridPos))
                    return;

                _model.PatchTileIdentity(tile.tileDefId, TileIdentity.BuildingIdUnassigned, 0);
            });
        }
    }
}
