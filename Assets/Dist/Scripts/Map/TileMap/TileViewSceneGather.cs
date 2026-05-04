using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 씬의 <see cref="TileView"/>를 <see cref="TileData"/> 목록으로 바꿔 모델 초기화·JSON 저장에 씁니다.
    /// </summary>
    public static class TileViewSceneGather
    {
        /// <summary>
        /// tileType이 none이 아닌 뷰만 포함합니다. 새 export용 <see cref="TileData.tileDefId"/>는 매번 새로 만듭니다.
        /// </summary>
        public static List<TileData> BuildTileDataSnapshot(IEnumerable<TileView> views)
        {
            var list = new List<TileData>();
            foreach (var v in views)
            {
                if (v == null || v.tileType == TileView.TileType.none) continue;

                byte t = (byte)v.tileType;
                byte ef = TileIdentity.EdgeFaceNone;
                Vector3Int size = v.size;
                Vector3Int grid = v.gridPos;

                if (v.tileType == TileView.TileType.EdgeWall)
                {
                    ef = (byte)Mathf.Clamp(v.wallEdgeFace, 0, 1);
                    size = Vector3Int.one;
                }

                list.Add(new TileData
                {
                    tileDefId = Guid.NewGuid(),
                    state = default,
                    identity = new TileIdentity
                    {
                        PrefabId = v.prefabId ?? string.Empty,
                        GridPos = grid,
                        sizeUnit = size,
                        tileType = t,
                        edgeFace = ef
                    }
                });
            }

            return list;
        }
    }
}
