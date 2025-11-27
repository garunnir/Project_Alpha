using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public class TileMapSerializer : MonoBehaviour
{
    [Header("Where to save/read the map file")]
    public string fileName = "map01.json";      // 파일 이름
    public bool usePersistentPath = true;       // Application.persistentDataPath 사용할지

    [Header("Prefab DB for loading")]
    public TilePrefabDB prefabDB;

    [Header("Grid / World Settings")]
    public float cellSize = 1f;                 // 그리드 셀 월드 크기

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

    // === Serialize: 씬 → JSON 파일 ===
    [ContextMenu("Save Map To JSON")]
    public void SaveMap()
    {
        var tileInfos = FindObjectsOfType<TileInfo>(true);

        TileMapData mapData = new TileMapData();
        mapData.tiles.Clear();

        foreach (var info in tileInfos)
        {
            // 혹시 월드에서 그리드 역산해야 하면:
            // info.CaptureGridFromWorld(cellSize);

            TileData td = new TileData
            {
                x      = info.gridPos.x,
                y      = info.gridPos.y,
                z      = info.gridPos.z,
                sizeX  = info.size.x,
                sizeY  = info.size.y,
                sizeZ  = info.size.z,
                prefabId = info.prefabId
            };

            mapData.tiles.Add(td);
        }

        string json = JsonUtility.ToJson(mapData, true);

        string fullPath = GetFullPath();
        File.WriteAllText(fullPath, json);

        Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapData.tiles.Count})");
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

        // 기존 타일들 정리할지 말지 선택 (여기선 다 지우는 예시)
        ClearExistingTiles();

        foreach (var td in mapData.tiles)
        {
            GameObject prefab = prefabDB != null ? prefabDB.GetPrefab(td.prefabId) : null;

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab for id: {td.prefabId}");
                continue;
            }

            // Anchor 기준 월드 좌표
            Vector3 worldPos = new Vector3(
                td.x * cellSize,
                td.y * cellSize,
                td.z * cellSize
            );

            var go = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);

            var info = go.GetComponent<TileInfo>();
            if (info == null)
            {
                info = go.AddComponent<TileInfo>();
            }

            info.gridPos = new Vector3(td.x, td.y, td.z);
            info.size    = new Vector3Int(td.sizeX, td.sizeY, td.sizeZ);
            info.prefabId = td.prefabId;

            // 필요하면, 멀티타일용으로 콜라이더/메시 사이즈 조정 로직 추가
            // e.g. info.ApplyGridToWorld(cellSize);
        }

        Debug.Log($"TileMap loaded from: {fullPath} (tiles: {mapData.tiles.Count})");
    }

    void ClearExistingTiles()
    {
        var tileInfos = FindObjectsOfType<TileInfo>(true);

        // 타일만 날린다고 가정 (이 스크립트가 붙은 오브젝트는 남김)
        foreach (var info in tileInfos)
        {
            if (info != null)
            {
                // 본인 자신(TileMapSerializer의 GameObject) 밑에 있는지만 보고 날릴 수도 있음
                DestroyImmediate(info.gameObject);
            }
        }
    }
    
}

// 한 타일(Anchor 기준)의 순수 데이터 구조
[Serializable]
public class TileData
{
    public float x;
    public float y;
    public float z;

    public int sizeX;
    public int sizeY;
    public int sizeZ;

    public string prefabId;
}

// 맵 전체 데이터
[Serializable]
public class TileMapData
{
    public List<TileData> tiles = new List<TileData>();
}
