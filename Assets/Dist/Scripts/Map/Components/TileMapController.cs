using System.Collections.Generic;
using UnityEngine;
using IsoTilemap;

// 타일 편집 조작과 뷰 갱신만 담당합니다. 로드/저장은 TileMapManager 책임.
public class TileMapController : MonoBehaviour
{
    private IMapModel _model;
    private IMapViewBuilder _viewBuilder;

    private readonly HashSet<Vector3Int> _dirty = new();

    public void Init(IMapModel model, IMapViewBuilder viewBuilder)
    {
        _model = model;
        _viewBuilder = viewBuilder;
        _viewBuilder.Bind(model);
        _viewBuilder.Build(model);
    }

    public void MarkDirty(Vector3Int cell) => _dirty.Add(cell);

    public void FlushDirty()
    {
        foreach (var cell in _dirty)
            RefreshCell(cell);
        _dirty.Clear();
    }

    private void RefreshCell(Vector3Int cellPos)
    {
        if (_model.TryGetTiles(cellPos, out IReadOnlyList<TileData> tiles))
            _viewBuilder.RefreshCell(cellPos, tiles);
    }
}
