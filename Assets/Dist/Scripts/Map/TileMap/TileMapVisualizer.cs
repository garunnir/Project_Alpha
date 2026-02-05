
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
    public class TileMapVisualizer : IMapViewBuilder
    {
        [Header("Grid / World Settings")]
        public float cellSize = 1f;

        private Dictionary<Guid, TileView> _tileInstances = new Dictionary<Guid, TileView>();

        private Transform _targetTransform;
        private TileObjFactory _tileFactory;

        public TileMapVisualizer(TileObjFactory tileFactory)
        {
            _tileFactory = tileFactory;
        }



        /// <summary>
        /// 모델 데이터를 기반으로 타일맵을 구축합니다.
        /// </summary>
        public void Build(IMapModelReadOnly modelData, TileMapContext context)
        {
            var tiles = modelData.Tiles();
            if (tiles == null || tiles.Count() == 0)
            {
                Debug.LogWarning("No tile data to build visual.");
                return;
            }

            ClearExistingTiles();
            Dictionary<Guid, TileView> spawnedTiles = _tileFactory.SpawnTiles(tiles);
            _tileInstances = spawnedTiles;
        }


        /// <summary>
        /// 기존 타일들을 정리합니다.
        /// </summary>
        private void ClearExistingTiles()
        {
            foreach (Transform child in _targetTransform)
            {
                GameObject.Destroy(child.gameObject);
            }

            _tileInstances.Clear();
        }

        /// <summary>
        /// ID로 타일 인스턴스를 조회합니다.
        /// </summary>
        public bool TryGetTile(Guid tileId, out TileView tileInfo)
        {
            return _tileInstances.TryGetValue(tileId, out tileInfo);
        }
    }
}
