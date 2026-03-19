
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 타일맵 시각화 담당 클래스
    /// 모델 데이터를 게임 월드에 3D 타일로 인스턴스화합니다.
    /// </summary>
    public class TileMapVisualizer : IMapViewBuilder, IDisposable
    {
        private Dictionary<Guid, TileView> _tileViews = new Dictionary<Guid, TileView>();

        private TileObjFactory _tileFactory;

        public TileMapVisualizer(TileObjFactory tileFactory)
        {
            _tileFactory = tileFactory;
        }



        /// <summary>
        /// 모델 데이터를 기반으로 타일맵을 구축합니다.
        /// </summary>
        public void Build(IMapModelReadOnly model)
        {
            IReadOnlyList<TileData> tiles = model.TilesSnapshot;
            if (tiles == null || tiles.Count() == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }

            ClearTiles();
            _tileViews = _tileFactory.SpawnTiles(tiles);
        }


        /// <summary>
        /// 기존 타일들을 정리합니다.
        /// </summary>
        private void ClearTiles()
        {
            foreach (var view in _tileViews.Values)
            {
                if (view != null)
                    GameObject.Destroy(view.gameObject);
            }

            _tileViews.Clear();
        }

        /// <summary>
        /// ID로 타일 인스턴스를 조회합니다.
        /// </summary>
        public bool TryGetTile(Guid tileId, out TileView tileView)
        {
            return _tileViews.TryGetValue(tileId, out tileView);
        }

        public void Bind(IMapModelReadOnly runtime) { }

        public void RefreshCell(Vector3Int cellPos, IReadOnlyList<TileData> tiles)
        {
            foreach (var tileData in tiles)
            {
                if (TryGetTile(tileData.tileDefId, out TileView tileView))
                    tileView.UpdateTile(tileData);
                else
                {
                    var newView = _tileFactory.SpawnTile(tileData);
                    _tileViews[tileData.tileDefId] = newView;
                }
            }
        }

        public void Dispose()
        {
            ClearTiles();
        }
    }
}
