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
    /// <summary>씬 TileView 스냅샷으로 모델을 갱신한 뒤 <see cref="TileMapDtoMapper"/>와 동일 규칙으로 JSON 저장합니다.</summary>
    [ContextMenu("Save Map To JSON")]
    private void SaveInEditor()
    {
        var mapper = _mapper ?? new TileMapDtoMapper();

        var tileViews = Object.FindObjectsByType<TileView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var snapshot = TileViewSceneGather.BuildTileDataSnapshot(tileViews);
        var dtoModel = new MapModelDTO(snapshot);
        MapSaveJsonDto jsonDto = mapper.FromPrepared(dtoModel);

        _model?.Initialize(dtoModel);

        File.WriteAllText(GetFullPath(), JsonUtility.ToJson(jsonDto, true));
        Debug.Log($"TileMap saved to: {GetFullPath()} (tiles: {jsonDto.tiles.Count}, wallEdges: {jsonDto.wallEdges?.Count ?? 0})");
    }
#endif
}
