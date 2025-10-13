using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEditor;

[System.Serializable]
public class TileSaveData
{
    public int x, y, layer;
    public string tileType;
}

[System.Serializable]
public class TileSaveDataArray
{
    public TileSaveData[] tiles;
}

public class MultiLayerTilemapToJson : MonoBehaviour
{

    // Addressables 타일 캐시 (라벨 기반)
    private Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();
    private bool tilesLoaded = false;

    // 'Tiles' 라벨로 모든 타일을 미리 로드 (최초 1회만)
    public void PreloadAllTilesFromLabel()
    {
        if (tilesLoaded) return;
        var handle = Addressables.LoadAssetsAsync<TileBase>("Tiles", tile => {
            if (!tileCache.ContainsKey(tile.name))
                tileCache[tile.name] = tile;
        });
        handle.WaitForCompletion();
        tilesLoaded = true;
    }

    // 캐시에서 타일 찾기
    private TileBase GetTileFromCache(string name)
    {
        if (!tilesLoaded) PreloadAllTilesFromLabel();
        tileCache.TryGetValue(name, out var tile);
        return tile;
    }

    // Addressables로 타일 비동기 로드 및 캐싱
    private TileBase LoadTileAddressable(string key)
    {
        if (tileCache.TryGetValue(key, out var cached))
            return cached;
        var handle = Addressables.LoadAssetAsync<TileBase>(key);
        handle.WaitForCompletion();
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            tileCache[key] = handle.Result;
            return handle.Result;
        }
        return null;
    }

    [Header("저장/불러오기용 JSON 파일 경로 (예: Assets/TilemapData.json)")]
    public string jsonFilePath = "Assets/TilemapData.json";

    public List<Tilemap> tilemaps = new List<Tilemap>(); // Inspector에서 Layer0~Layer4 할당

    // 이 오브젝트의 자식 Tilemap만 자동으로 찾음
    public void AutoAssignTilemapsFromChildren()
    {
        tilemaps.Clear();
        foreach (Transform child in transform)
        {
            var tm = child.GetComponent<Tilemap>();
            if (tm != null)
                tilemaps.Add(tm);
        }
        // 레이어 순서 보장: 이름에 숫자가 있으면 정렬
        tilemaps.Sort((a, b) => a.gameObject.name.CompareTo(b.gameObject.name));
    }

    // 모든 레이어의 타일 정보를 JSON으로 변환
    public string AllTilemapsToJson()
    {
        var allTiles = new List<TileSaveData>();
        for (int i = 0; i < tilemaps.Count; i++)
        {
            var tilemap = tilemaps[i];
            var bounds = tilemap.cellBounds;
            foreach (var pos in bounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(pos);
                if (tile != null)
                {
                    allTiles.Add(new TileSaveData
                    {
                        x = pos.x,
                        y = pos.y,
                        layer = i,
                        tileType = tile.name
                    });
                }
            }
        }
        TileSaveDataArray wrapper = new TileSaveDataArray { tiles = allTiles.ToArray() };
        return JsonUtility.ToJson(wrapper, true);
    }

    // JSON 파일에서 맵을 읽어 Tilemap에 배치
    public void LoadMapFromJson(string json)
    {
        tilemaps.RemoveAll(item => item == null);
        // 기존 타일 모두 삭제
        foreach (var tilemap in tilemaps)
            tilemap.ClearAllTiles();

        TileSaveDataArray wrapper = JsonUtility.FromJson<TileSaveDataArray>(json);
        if (wrapper == null || wrapper.tiles == null) return;

        foreach (var tile in wrapper.tiles)
        {
            if (tile.layer < 0) continue;
            // 인덱스 접근 보장: 부족하면 null로 채움
            while (tile.layer >= tilemaps.Count)
            {
                tilemaps.Add(null);
            }
            if (tilemaps[tile.layer] == null)
            {
                GameObject obj = new GameObject("Tilemap_Layer" + tile.layer, typeof(Tilemap));
                TilemapRenderer tilemapRenderer=obj.AddComponent<TilemapRenderer>();
                tilemapRenderer.sortOrder=TilemapRenderer.SortOrder.TopRight;
                obj.transform.parent = this.transform;
                tilemaps[tile.layer] = obj.GetComponent<Tilemap>();
            }

            var tilemap = tilemaps[tile.layer];
            if (tilemap == null) continue;
            // 'Tiles' 라벨로 미리 로드된 캐시에서 타일 찾기
            TileBase tileAsset = GetTileFromCache(tile.tileType);
            if (tileAsset != null)
            {
                tilemap.SetTile(new Vector3Int(tile.x, tile.y, 0), tileAsset);
            }
            else
            {
                Debug.LogWarning($"타일 에셋을 찾을 수 없습니다: {tile.tileType} (Layer {tile.layer} at {tile.x},{tile.y})");
            }
        }
    }
}
