using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace IsoTilemap
{
    public class MapSavePipline
    {
        private readonly IMapMapper _mapper;
        private readonly TileMapRuntime _runtime;

        public MapSavePipline(TileMapRuntime runtime,
            IMapMapper mapper)
        {
            _mapper = mapper;
            _runtime = runtime;
        }

        public void Save(string fullPath)
        {
            IMapTilesReadOnly mapData = new MapTilesDTO(_runtime.tiles);
            MapSaveJsonDto mapDatas = _mapper.FromPrepared(mapData);

            string json = JsonUtility.ToJson(mapDatas, true);

            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
        // void 대신 async Task 또는 async void(이벤트성일 때만) 사용
        public async void SaveAsync(string fullPath)
        {
            // 1. 메인 스레드 작업: 데이터 캡처 (유니티 API 접근은 메인 스레드에서 해야 함)
            // 런타임 데이터가 저장 중에 바뀌면 안되므로 여기서 DTO 변환까지는 끝내는 것이 안전함
            IMapTilesReadOnly mapData = new MapTilesDTO(_runtime.tiles);
            MapSaveJsonDto mapDatas = _mapper.FromPrepared(mapData);

            // 2. 백그라운드 작업: JSON 변환 및 파일 쓰기 (무거운 작업)
            await Task.Run(() =>
            {
                // JsonUtility는 메인 스레드 전용이므로, 백그라운드에서는 Newtonsoft.Json을 쓰거나
                // 혹은 JsonUtility를 위쪽(1번 영역)에서 미리 수행하고 string만 넘겨야 함.
                // 여기서는 string을 넘겨받았다고 가정하거나, JsonUtility를 메인에서 호출하는 방식으로 수정 권장.
                
                // *수정 전략*: JsonUtility는 스레드 안전하지 않으므로 메인 스레드에서 string으로 변환 후
                // 파일 쓰기만 비동기로 넘기는 것이 가장 깔끔합니다.
            });
            
            // 더 나은 비동기 저장 패턴 (JsonUtility 사용 시)
            string json = JsonUtility.ToJson(mapDatas, true); // 메인 스레드에서 수행 (빠름)
            
            await File.WriteAllTextAsync(fullPath, json); // .NET Standard 2.1 (Unity 2021.2+) 지원

            Debug.Log($"TileMap saved asynchronously to: {fullPath}");
        }
    }
}