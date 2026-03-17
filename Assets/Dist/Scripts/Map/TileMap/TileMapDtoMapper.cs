using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    //Json->DTO
    public class TileMapDtoMapper : IMapMapper
    {

        public MapModelDTO ToPrepared(MapSaveJsonDto tileMapData)
        {
            if (tileMapData == null || tileMapData.tiles == null)
            {
                Debug.LogWarning("TileMapData or its tiles are null.");
                return null;
            }
            // 내부적으로는 수정 가능한 List로 수집한 뒤 IReadOnlyDictionary/IReadOnlyList로 변환하여 반환합니다.
            List<TileData> prepareData = new List<TileData>();
            foreach (var td in tileMapData.tiles)
            {
                prepareData.Add(new TileData
                {
                    tileDefId = Guid.NewGuid(),
                    state = new TileState { },
                    identity = new TileIdentity
                    {
                        PrefabId = td.prefabId,
                        tileType = td.tileType,
                        GridPos = new Vector3Int(td.x, td.y, td.z),
                        sizeUnit = new Vector3Int(td.sizeX, td.sizeY, td.sizeZ),

                    }
                });
            }

            return new MapModelDTO(prepareData);
        }

        public MapSaveJsonDto FromPrepared(MapModelDTO prepared)
        {
            IReadOnlyList<TileData> tiles = prepared.TilesData;
            //DTO로 변환하여 집어넣을 컨테이너 생성
            MapSaveJsonDto tile = new MapSaveJsonDto();

            foreach (var ti in tiles)
            {
                tile.tiles.Add(new TileSaveData
                {
                    sizeX = ti.identity.sizeUnit.x,
                    sizeY = ti.identity.sizeUnit.y,
                    sizeZ = ti.identity.sizeUnit.z,
                    x = ti.identity.GridPos.x,
                    y = ti.identity.GridPos.y,
                    z = ti.identity.GridPos.z,
                    tileType = ti.identity.tileType,
                    prefabId = ti.identity.PrefabId,
                });
            }
            return tile;

        }
    }
}