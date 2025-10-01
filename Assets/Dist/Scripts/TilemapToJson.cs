using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;


public class TilemapToJson : MonoBehaviour
{
    public Tilemap tilemap;
    public int layer = 1; // 층 정보가 있다면 지정

    public string TilemapToJsonString()
    {
        var bounds = tilemap.cellBounds;
        var tileList = new List<TileSaveData>();
        foreach (var pos in bounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(pos);
            if (tile != null)
            {
                tileList.Add(new TileSaveData
                {
                    x = pos.x,
                    y = pos.y,
                    layer = layer,
                    tileType = tile.name
                });
            }
        }
        TileSaveDataArray wrapper = new TileSaveDataArray { tiles = tileList.ToArray() };
        return JsonUtility.ToJson(wrapper, true);
    }
}
