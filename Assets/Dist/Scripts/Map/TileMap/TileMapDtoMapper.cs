using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    // DTO 와 도메인 모델 간의 매핑을 담당하는 클래스
    public class TileMapDtoMapper : IMapMapper
    {
        public IMapTilesReadOnly ToPrepared(MapSaveJsonDto tileMapData)
        {
            if (tileMapData == null || tileMapData.tiles == null)
            {
                Debug.LogWarning("TileMapData or its tiles are null.");
                return null;
            }
            // 내부적으로는 수정 가능한 List로 수집한 뒤 IReadOnlyDictionary/IReadOnlyList로 변환하여 반환합니다.
            Dictionary<Vector3Int, List<TileData>> prepareData = new Dictionary<Vector3Int, List<TileData>>();

            foreach (var td in tileMapData.tiles)
            {
                Vector3Int v = new Vector3Int(td.x, td.y, td.z);

                if (!prepareData.TryGetValue(v, out var existingList))
                {
                    existingList = new List<TileData>();
                    prepareData[v] = existingList;
                }
                existingList.Add(new TileData
                {
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
            //맵아이디 어차피 참고해야할 부분인데 굳이 따로 바인드해가면서 쓸 일인가?
            //내가 방황하는 이유는 이 타일데이터라는 항목의 목적이 명확하지 않기 때문인 듯.
            //타일데이터의 목표... 그것은 데이터적으로 숨겨야 할 벽에 접근하기 위함.
            // prepareData를 IReadOnlyDictionary<Vector3Int,IReadOnlyList<TileData>>로 변환
            var readonlyDict = new Dictionary<Vector3Int, IReadOnlyList<TileData>>();
            foreach (var kv in prepareData)
                readonlyDict[kv.Key] = kv.Value;

            return new MapTilesDTO(readonlyDict);
        }

        public MapSaveJsonDto FromPrepared(IMapTilesReadOnly prepared)
        {
            IEnumerable<Vector3Int> tiles = prepared.Positions;
            //DTO로 변환하여 집어넣을 컨테이너 생성
            MapSaveJsonDto tile = new MapSaveJsonDto();

            foreach (var td in tiles)
            {
                if (prepared.TryGetTiles(td, out var tileList))
                {
                    foreach (var ti in tileList)
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
                }
            }
            return tile;

        }
    }
}