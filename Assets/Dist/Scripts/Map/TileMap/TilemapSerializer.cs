using UnityEngine;
using System.IO;

namespace IsoTilemap
{
    public class TileMapSerializer : IMapSerializer
    {


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
            if (mapData.wallEdges == null)
                mapData.wallEdges = new System.Collections.Generic.List<WallEdgeSaveData>();
            return mapData;
        }
     


        public void Write(string path, MapSaveJsonDto dto)
        {
            string json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(path, json);
        }
    }
}
