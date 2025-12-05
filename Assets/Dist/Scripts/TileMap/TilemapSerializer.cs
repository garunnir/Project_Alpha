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

            TileMapData mapData = new TileMapData();
            mapData.tiles.Clear();

            foreach (var info in tileInfos)
            {
                // 혹시 월드에서 그리드 역산해야 하면:
                // info.CaptureGridFromWorld(cellSize);

                TileData td = new TileData
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
            TileMapData mapDatas = new TileMapData();
            foreach (var item in mapData.tiles.Values)
            {
                foreach (var info in item)
                {
                    TileData td = new TileData
                    {
                        x = info.gridPos.x,
                        y = info.gridPos.y,
                        z = info.gridPos.z,
                        sizeX = info.size.x,
                        sizeY = info.size.y,
                        sizeZ = info.size.z,
                        prefabId = info.prefabId,
                        tileType = (byte)info.tileType
                    };

                    mapDatas.tiles.Add(td);

                }
            }


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
            TileMapData mapData = JsonUtility.FromJson<TileMapData>(json);

            if (mapData == null || mapData.tiles == null)
            {
                Debug.LogWarning("Map data is null or invalid.");
                return;
            }
            _visualizer.BuildVisualFromData(mapData);
        }

 

    }
    [Serializable]
    public class TileMapData
    {
        public List<TileData> tiles = new List<TileData>();
    }
}
