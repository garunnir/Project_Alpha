using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    //컨트롤러는 타일맵의 상태를 관리하고, 변경 사항을 시각화하는 역할을 합니다.
    public class TileMapController : MonoBehaviour
    {
        [SerializeField] private IMapModel _model;
        [SerializeField] private IMapViewBuilder _viewBuilder;


        HashSet<Vector3Int> dirty = new();

        //public ref TileState GetOrCreate(Vector3Int cell) { /* ... */ }

        public void MarkDirty(Vector3Int cell) => dirty.Add(cell);

        public void FlushDirty()
        {
            foreach (var cell in dirty)
                RefreshCell(cell); // 그 셀만 갱신
            dirty.Clear();
        }
        void Update()
        {
            FlushDirty();
        }
        private void RefreshCell(Vector3Int cellPos)
        {
            //모델의 데이터를 조회하여 뷰를 갱신
            if (_model.TryGetTiles(cellPos, out IReadOnlyList<TileData> tiles))
            {
                _viewBuilder.RefreshCell(cellPos, tiles);
            }
        }
    }
}
