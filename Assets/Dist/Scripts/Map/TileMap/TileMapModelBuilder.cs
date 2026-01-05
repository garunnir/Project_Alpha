using System;
using UnityEngine;
namespace IsoTilemap
{
    //맵 도메인 모델 빌더 담당
    public class TileMapModelBuilder : IMapModelBuilder
    {
        private TileMapRuntimeData _runtimeData;

        public IMapModel BuildRuntime(IMapTilesReadOnly prepared)
        {
            _runtimeData = new TileMapRuntimeData { tiles = prepared.TryGetTiles().tiles };
           
            return  new MapModelReadOnlyImpl(_runtimeData);
        }

    }
}


