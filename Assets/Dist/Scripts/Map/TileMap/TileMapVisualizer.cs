
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 타일맵 시각화 담당 클래스
    /// 모델 이벤트를 받아 게임 월드 렌더 상태를 동기화합니다.
    /// </summary>
    public class TileMapVisualizer : IMapViewBuilder, IDisposable
    {
        private Dictionary<Guid, TileView> _tileViews = new Dictionary<Guid, TileView>();

        private readonly TileObjFactory _tileFactory;
        private IMapModelReadOnly _boundRuntime;

        public TileMapVisualizer(TileObjFactory tileFactory)
        {
            _tileFactory = tileFactory;
        }



        /// <summary>
        /// 모델 스냅샷을 기반으로 초기 렌더를 구성합니다.
        /// </summary>
        public void Build(IMapModelReadOnly model)
        {
            RenderInitialMap(model);
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

        public void Bind(IMapModelReadOnly runtime)
        {
            if (_boundRuntime != null)
            {
                _boundRuntime.OnRuntimeDataChanged -= RefreshCell;
                _boundRuntime.OnRuntimeBatchChanged -= RefreshCells;
            }

            _boundRuntime = runtime;

            if (_boundRuntime != null)
            {
                _boundRuntime.OnRuntimeDataChanged += RefreshCell;
                _boundRuntime.OnRuntimeBatchChanged += RefreshCells;
            }
        }

        public void RefreshCell(Vector3Int cellPos, IReadOnlyList<TileData> tiles)
        {
            RenderCell(cellPos, tiles);
        }

        private void RefreshCells(IReadOnlyCollection<Vector3Int> changedCells)
        {
            if (_boundRuntime == null) return;
            var buffer = new List<TileData>();
            foreach (var cellPos in changedCells)
            {
                _boundRuntime.GatherRenderableTiles(cellPos, buffer);
                if (buffer.Count > 0)
                    RenderCell(cellPos, buffer);
            }
        }

        private void RenderInitialMap(IMapModelReadOnly model)
        {
            IReadOnlyList<TileData> tiles = model.TilesSnapshot;

            if (tiles == null || tiles.Count == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }

            ClearTiles();
            _tileViews = _tileFactory.SpawnTiles(tiles);
        }

        private void RenderCell(Vector3Int cellPos, IReadOnlyList<TileData> tiles)
        {
            _ = cellPos;
            foreach (var tileData in tiles)
            {
                if (TryGetTile(tileData.tileDefId, out TileView tileView))
                    tileView.UpdateTile(tileData);
                else
                {
                    var newView = _tileFactory.SpawnTile(tileData);
                    if (newView != null)
                        _tileViews[tileData.tileDefId] = newView;
                }
            }
        }

        public void Dispose()
        {
            if (_boundRuntime != null)
            {
                _boundRuntime.OnRuntimeDataChanged -= RefreshCell;
                _boundRuntime.OnRuntimeBatchChanged -= RefreshCells;
                _boundRuntime = null;
            }
            ClearTiles();
        }
    }
}
