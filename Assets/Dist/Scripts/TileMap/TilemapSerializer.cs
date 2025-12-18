using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    [RequireComponent(typeof(TileMapVisualizer),typeof(TileMapRuntime))]
    public class TileMapSerializer : MonoBehaviour
    {
        [Header("Where to save/read the map file")]
        [SerializeField] private string fileName = "map01.json";      // 파일 이름
        [SerializeField] private bool usePersistentPath = true;       // Application.persistentDataPath 사용할지


        private TileMapRuntime _tileMapData;
        private TileMapVisualizer _visualizer;
        void Awake()
        {
            _visualizer = GetComponent<TileMapVisualizer>();
            _tileMapData = GetComponent<TileMapRuntime>();
        }
        string GetFullPath()
        {
            if (usePersistentPath)
            {
                return Path.Combine(Application.persistentDataPath, fileName);
            }
            else
            {
                // 프로젝트 루트 기준 편의용 (에디터에서만)
                return Path.Combine(Application.dataPath, "..", fileName);
            }
        }
#if UNITY_EDITOR
        // === Serialize: 씬 → JSON 파일 ===
        [SerializeField,ContextMenu("Save Map To JSON")]
        private void SaveMapInEditor()
        {
            // FindObjectsOfType(T) is obsolete in newer Unity versions.
            // Use FindObjectsByType and explicitly include inactive objects for the same behavior.
            var tileInfos = UnityEngine.Object.FindObjectsByType<TileInfo>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

            TileSaveJsonData mapData = new TileSaveJsonData();
            mapData.tiles.Clear();

            foreach (var info in tileInfos)
            {
                // 혹시 월드에서 그리드 역산해야 하면:
                // info.CaptureGridFromWorld(cellSize);

                TileSaveData td = new TileSaveData
                {
                    x = info.gridPos.x,
                    y = info.gridPos.y,
                    z = info.gridPos.z,
                    sizeX = info.size.x,
                    sizeY = info.size.y,
                    sizeZ = info.size.z,
                    prefabId = info.prefabId,
                    tileType =(byte)info.tileType
                };

                mapData.tiles.Add(td);
            }

            string json = JsonUtility.ToJson(mapData, true);

            string fullPath = GetFullPath();
            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapData.tiles.Count})");
        }
#endif
        //런타임에 사용한다.
        public void SaveMap()
        {
            var mapData = _tileMapData.GetRuntimeData();

            TileSaveJsonData mapDatas = AssemblyTileSaveData(mapData);

            string json = JsonUtility.ToJson(mapDatas, true);

            string fullPath = GetFullPath();
            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
        // === Deserialize: JSON 파일 → 씬 ===
        [ContextMenu("Load Map From JSON")]
        public void LoadMap()
        {
            string fullPath = GetFullPath();
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Map file not found: {fullPath}");
                return;
            }

            string json = File.ReadAllText(fullPath);
            TileSaveJsonData mapData = JsonUtility.FromJson<TileSaveJsonData>(json);

            if (mapData == null || mapData.tiles == null)
            {
                Debug.LogWarning("Map data is null or invalid.");
                return;
            }
            _visualizer.BuildVisualFromData(AssemblyTileData(mapData));
        }
        //저장용 타일데이터를 인게임에서 사용하는 형태로 변환한다.
        public TileMapRuntimeData AssemblyTileData(TileSaveJsonData tileMapData)
        {
            TileMapRuntimeData mapRuntimeData=new TileMapRuntimeData();
            
            foreach (var td in tileMapData.tiles)
            {
                Vector3Int v = new Vector3Int(td.x, td.y, td.z);

                if (mapRuntimeData.tiles.ContainsKey(v))
                {
                    mapRuntimeData.tiles[v].Add(new TileData
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
            }
            //맵아이디 어차피 참고해야할 부분인데 굳이 따로 바인드해가면서 쓸 일인가?
            //내가 방황하는 이유는 이 타일데이터라는 항목의 목적이 명확하지 않기 때문인 듯.
            //타일데이터의 목표... 그것은 데이터적으로 숨겨야 할 벽에 접근하기 위함.
            return mapRuntimeData;
        }
        public TileSaveJsonData AssemblyTileSaveData(TileMapRuntimeData tileMapRuntimeData)
        {
            TileSaveJsonData tile=new TileSaveJsonData();

            foreach(var td in tileMapRuntimeData.tiles)
            {
                foreach (var ti in td.Value)
                {
                    tile.tiles.Add(new TileSaveData
                    {
                        sizeX = ti.identity.sizeUnit.x,
                        sizeY = ti.identity.sizeUnit.y,
                        sizeZ = ti.identity.sizeUnit.z,
                        x=ti.identity.GridPos.x,
                        y=ti.identity.GridPos.y,
                        z=ti.identity.GridPos.z,
                        tileType=ti.identity.tileType,
                        prefabId=ti.identity.PrefabId,
                    });
                }
            }
            return tile;
        }

        [Serializable]
        public class TileSaveJsonData
        {
            public List<TileSaveData> tiles = new List<TileSaveData>();
        }
    }
}
