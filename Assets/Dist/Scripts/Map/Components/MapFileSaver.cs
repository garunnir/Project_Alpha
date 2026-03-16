using IsoTilemap;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MapFileSaver : MonoBehaviour
{
    [Header("Map file")]
    [SerializeField] private string fileName = "map01.json";
    [SerializeField] private bool usePersistentPath = false;

    private IMapModel _model;
    private IMapMapper _mapper;

    public void Init(IMapModel model)
    {
        _model = model;
        _mapper = new TileMapDtoMapper();
    }

    public void Save()
    {
        new MapSavePipline(_model, _mapper).Save(GetFullPath());
    }

    private string GetFullPath()
    {
        if (usePersistentPath)
            return Path.Combine(Application.persistentDataPath, fileName);
        else
            return Path.Combine(Application.dataPath, "..", fileName);
    }

#if UNITY_EDITOR
    // 에디터에서 씬의 TileView 오브젝트를 JSON으로 직렬화
    [ContextMenu("Save Map To JSON")]
    private void SaveInEditor()
    {
        var tileInfos = Object.FindObjectsByType<TileView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var mapData = new MapSaveJsonDto();
        foreach (var info in tileInfos)
        {
            mapData.tiles.Add(new TileSaveData
            {
                x = info.gridPos.x,
                y = info.gridPos.y,
                z = info.gridPos.z,
                sizeX = info.size.x,
                sizeY = info.size.y,
                sizeZ = info.size.z,
                prefabId = info.prefabId,
                tileType = (byte)info.tileType
            });
        }

        File.WriteAllText(GetFullPath(), JsonUtility.ToJson(mapData, true));
        Debug.Log($"TileMap saved to: {GetFullPath()} (tiles: {mapData.tiles.Count})");
    }
#endif
}
