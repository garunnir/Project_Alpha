using IsoTilemap;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MapFileSaver : MonoBehaviour
{
    [Header("Map file")]
    [SerializeField] private string fileName = "map01.json";

    private IMapModel _model;
    private IMapMapper _mapper;

    public void Init(IMapModel model)
    {
        _model = model;
        _mapper = new TileMapDtoMapper();
    }

    public void Save()
    {
        new MapSavePipline(_model, _mapper)
            .Save(Path.Combine(Application.persistentDataPath, fileName));
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

        string fullPath = Path.Combine(Application.dataPath, "..", fileName);
        File.WriteAllText(fullPath, JsonUtility.ToJson(mapData, true));
        Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapData.tiles.Count})");
    }
#endif
}
