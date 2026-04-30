using UnityEngine;
using IsoTilemap;

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
        ApplyTileMutation(tileData);
    }
    public void AddAndFlush(TileData tileData)
    {
        AddTile(tileData);
    }
    public void RemoveAndFlush(TileData tileData)
    {
        RemoveTile(tileData);
    }

    private void ApplyTileMutation(TileData tileData)
    {
        _model.SetTile(tileData);
    }
}
