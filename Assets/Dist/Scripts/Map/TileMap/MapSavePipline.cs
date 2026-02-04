using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
namespace IsoTilemap
{
    public class MapSavePipline
    {
        private readonly IMapMapper _mapper;
        private readonly IMapRuntime _runtime;

        public MapSavePipline(IMapRuntime runtime,
            IMapMapper mapper)
        {
            _mapper = mapper;
            _runtime = runtime;
        }

        public void Save(string fullPath)
        {
            IMapTilesReadOnly mapData = new MapTilesDTO(_runtime.GetAllTiles());
            MapSaveJsonDto mapDatas = _mapper.FromPrepared(mapData);

            string json = JsonUtility.ToJson(mapDatas, true);

            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
        // void 대신 async Task 또는 async void(이벤트성일 때만) 사용
        public async UniTask SaveAsync(string fullPath)
        {
            // 1. 메인 스레드: 데이터 캡처
            var mapData = new MapTilesDTO(_runtime.GetAllTiles());
            var mapDatas = _mapper.FromPrepared(mapData);

            // 2. 백그라운드 작업 (이 블록 안에서만 백그라운드임이 보장됨)
            await UniTask.RunOnThreadPool(async () =>
            {
                // 여기서 Newtonsoft.Json 사용 (CPU Heavy)
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(mapDatas, Newtonsoft.Json.Formatting.Indented);

                // 파일 쓰기 (I/O Heavy)
                await File.WriteAllTextAsync(fullPath, json);
            });

            // 3. 여기는 자동으로 "메인 스레드"로 복귀되어 있음!
            // 별도의 SwitchToMainThread() 호출이 필요 없어 실수가 발생하지 않음
            Debug.Log($"TileMap saved asynchronously to: {fullPath}");
        }
        public async UniTask SaveSafeAsync(string fileName)
        {
            string fullPath = Path.Combine(Application.persistentDataPath, fileName);

            // 1. 데이터 준비
            var mapDatas = _mapper.FromPrepared(new MapTilesDTO(_runtime.GetAllTiles()));

            await UniTask.RunOnThreadPool(() =>
            {
                // 2. 스트림을 열고 바로 쓴다 (메모리에 거대 string을 만들지 않음)
                using (var fileStream = File.CreateText(fullPath))
                using (var writer = new Newtonsoft.Json.JsonTextWriter(fileStream))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.Indented; // 들여쓰기 설정
                    var serializer = new Newtonsoft.Json.JsonSerializer();

                    // 데이터를 조각내어 파일로 바로 흘려보냄
                    serializer.Serialize(writer, mapDatas);
                }
            });

            Debug.Log($"Saved safely to: {fullPath}");
        }
    }
}