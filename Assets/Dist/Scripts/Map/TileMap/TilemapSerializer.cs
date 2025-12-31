using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    public class TileMapSerializer : MonoBehaviour,IMapSerializer
    {


        // === Deserialize: JSON 파일 → 씬 ===
        // [ContextMenu("Load Map From JSON")]
        // public void LoadMap()
        // {
        //     string fullPath = GetFullPath();
        //     if (!File.Exists(fullPath))
        //     {
        //         Debug.LogWarning($"Map file not found: {fullPath}");
        //         return;
        //     }

        //     string json = File.ReadAllText(fullPath);
        //     MapSaveJsonDto mapData = JsonUtility.FromJson<MapSaveJsonDto>(json);

        //     if (mapData == null || mapData.tiles == null)
        //     {
        //         Debug.LogWarning("Map data is null or invalid.");
        //         return;
        //     }
        //     _visualizer.BuildVisualFromData(AssemblyTileData(mapData));
        // }
        //저장용 타일데이터를 인게임에서 사용하는 형태로 변환한다.
        
        public MapSaveJsonDto Read(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Map file not found: {fullPath}");
                return null;
            }

            string json = File.ReadAllText(fullPath);
            MapSaveJsonDto mapData = JsonUtility.FromJson<MapSaveJsonDto>(json);

            if (mapData == null || mapData.tiles == null)
            {
                Debug.LogWarning("Map data is null or invalid.");
                return null;
            }
            return mapData;
        }
     


        public void Write(string path, MapSaveJsonDto dto)
        {
            string json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(path, json);
        }
    }
}
