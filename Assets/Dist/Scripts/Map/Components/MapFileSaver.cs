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
    private IWorldGrid _worldGrid;

    public void Init(IMapModel model, IWorldGrid worldGrid)
    {
        _model = model;
        _worldGrid = worldGrid;
        _mapper = new TileMapDtoMapper();
    }

    public void Save()
    {
        SaveTo(GetFullPath());
    }

    public void SaveTo(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return;

        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        float cellSize = _worldGrid != null ? _worldGrid.CellSize : 1f;
        new MapSavePipeline(_model, _mapper).Save(
            fullPath,
            cellSize,
            MapBloodHost.Runtime,
            MapPlantHost.Runtime,
            MapLiquidHost.Runtime,
            MapDigColumnHost.Runtime);
    }

    public string FileName
    {
        get => fileName;
        set => fileName = value;
    }

    public bool UsePersistentPath
    {
        get => usePersistentPath;
        set => usePersistentPath = value;
    }

    public string ResolveFullPath() => GetFullPath();

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
    public void SaveFromSceneInEditor()
    {
        string fullPath = GetFullPath();

        // 편집 모드에는 Awake가 안 돌아 호스트가 전부 null이다. 기존 파일을 먼저 읽어야
        // 액체·혈흔·시계를 계승할 수 있고, 못 읽으면 덮어쓰는 대신 중단한다.
        if (!MapSaveLayerCarryOver.TryReadExisting(fullPath, out MapSaveJsonDto existing))
            return;

        var mapper = _mapper ?? new TileMapDtoMapper();

        var tileViews = Object.FindObjectsByType<TileView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var liquidMarkers = Object.FindObjectsByType<LiquidAuthoringView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var snapshot = TileViewSceneGather.BuildTileDataSnapshot(tileViews);
        var dtoModel = new MapModelDTO(snapshot);
        MapSaveJsonDto jsonDto = mapper.FromPrepared(dtoModel);
        jsonDto.gridCellSize = _worldGrid != null ? _worldGrid.CellSize : 1f;

        // liquidCells bake의 입력이므로 CarryOver보다 먼저 채워야 한다.
        jsonDto.liquidAuthoringFaces = TileViewSceneGather.BuildLiquidAuthoringFaces(liquidMarkers);

        MapSaveLayerCarryOver.Apply(
            jsonDto,
            existing,
            MapLiquidHost.Runtime,
            MapBloodHost.Runtime,
            MapPlantHost.Runtime,
            MapDigColumnHost.Runtime);

        MapBoundsBake.ApplyToDto(jsonDto);

        _model?.Initialize(dtoModel);

        File.WriteAllText(fullPath, JsonUtility.ToJson(jsonDto, true));
        Debug.Log(
            $"TileMap saved to: {fullPath} (tiles: {jsonDto.tiles.Count}, wallEdges: {jsonDto.wallEdges?.Count ?? 0}, bloodStamps: {jsonDto.bloodStamps?.Count ?? 0}, liquidAuthoringFaces: {jsonDto.liquidAuthoringFaces?.Count ?? 0}, liquidCells: {jsonDto.liquidCells?.Count ?? 0}, hasLiquidSnapshot: {jsonDto.hasLiquidSnapshot})");
    }
#endif
}
