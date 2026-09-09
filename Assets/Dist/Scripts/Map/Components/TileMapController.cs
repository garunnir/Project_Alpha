using UnityEngine;
using IsoTilemap;
using System.Collections.Generic;

// 타일 편집 "명령"만 담당합니다. 렌더 반영은 모델 이벤트 -> Visualizer가 담당합니다.
public class TileMapController : MonoBehaviour
{
    private IMapModel _model;
    private IMapViewBuilder _visualizer;

    public void Init(IMapModel model, IMapViewBuilder viewBuilder)
    {
        _model = model;
        _visualizer = viewBuilder;
        _visualizer.Bind(model);
        _visualizer.Build(model);
    }

    // 하위 호환용 no-op: 이제 셀 갱신은 OnRuntimeDataChanged 이벤트로 자동 처리됩니다.
    public void MarkDirty(Vector3Int cell)
    {
    }
    // 하위 호환용 no-op: 이제 셀 갱신은 OnRuntimeDataChanged 이벤트로 자동 처리됩니다.
    public void FlushDirty() { }
    public void AddTile(TileData tileData)
    {
        ApplyTileMutation(tileData);
    }
    public void RemoveTile(TileData tileData)
    {
        _model.RemoveTile(tileData);
    }
    public void AddAndFlush(TileData tileData)
    {
        AddTile(tileData);
    }
    public void RemoveAndFlush(TileData tileData)
    {
        RemoveTile(tileData);
    }

    /// <summary>
    /// Walkable cell floor-material layer: same HorizontalFace key replaces previous
    /// (TileMapModel.SetFloorFaceTile). Returns false if definition missing or build fails.
    /// </summary>
    public bool TryReplaceFloorMaterial(Vector3Int walkableCell, TileDefinition floorDef)
    {
        if (_model == null || floorDef == null)
            return false;
        if (!TilePlaceUtil.TryBuildTileData(floorDef, walkableCell, out TileData tileData))
            return false;
        if (TileIdentityUtil.GetPlacementSlot(tileData.identity) != TilePlacementSlot.HorizontalFace)
            return false;
        AddAndFlush(tileData);
        return true;
    }

    /// <summary>
    /// 지층 walkable stratum 블록 — 앵커는 <see cref="MapDigTerrainUtil.SupportBlockAnchor"/>.
    /// </summary>
    public bool TryPlaceStratumBlock(Vector3Int walkableCell, TileDefinition blockDef)
    {
        if (_model == null || blockDef == null)
            return false;
        if (!MapDigTerrainUtil.IsWalkableStratumBlock(blockDef))
            return false;

        Vector3Int anchor = MapDigTerrainUtil.SupportBlockAnchor(walkableCell);
        if (!TilePlaceUtil.TryBuildTileData(blockDef, anchor, out TileData tileData))
            return false;
        if (TileIdentityUtil.GetPlacementSlot(tileData.identity) != TilePlacementSlot.OccupiedCell)
            return false;

        AddAndFlush(tileData);
        return true;
    }

    private void ApplyTileMutation(TileData tileData)
    {
        _model.SetTile(tileData);
    }
}
