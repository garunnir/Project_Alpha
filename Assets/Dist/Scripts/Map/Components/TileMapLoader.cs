using IsoTilemap;
using Sirenix.OdinInspector;
using System.IO;
using UnityEngine;
[DisallowMultipleComponent]
public class TileMapLoader : SerializedMonoBehaviour
{
    [Header("Prefab DB for loading")]
    [SerializeField] private TilePrefabDB prefabDB;
    [Header("Where to save/read the map file")]
    [SerializeField] private string fileName = "map01.json";
    [SerializeField] private bool usePersistentPath = true;

    public IMapModel Model { get; private set; }

    private TileObjFactory _tileFactory;
    private IMapViewBuilder _viewBuilder;
    private IMapSerializer _serializer;
    private IMapModelBuilder _modelBuilder;
    private IMapMapper _mapper;

    void Awake()
    {
        _serializer = new TileMapSerializer();
        _modelBuilder = new TileMapModelBuilder();
        _mapper = new TileMapDtoMapper();
    }
#if UNITY_EDITOR
    // === Deserialize: JSON 파일 → 씬 ===
    [ContextMenu("Load Map From JSON")]
    public void LoadMapEditor()
    {
        //TODO: 에디트모드에서 작동 하게해야함
LoadMapRuntime();
    }
#endif
    public void LoadMapRuntime()
    {
        LoadMapRuntime(GetFullPath());
    }
    public void LoadMapRuntime(string path)
    {
        // 1. 데이터 로드
        Model = new MapLoadPipeline(
                serializer: _serializer,
                modelBuilder: _modelBuilder,
                mapper: _mapper).LoadModel(path);

        // 2. 팩토리 & 뷰 준비
        _tileFactory = new TileObjFactory(this.transform, prefabDB);
        _viewBuilder = new TileMapVisualizer(_tileFactory);

        // 3. 뷰에 데이터 변화 구독 연결
        _viewBuilder.Bind(Model);

        // 4. 초기 화면 그리기
        _viewBuilder.Build(Model);
    }
#if UNITY_EDITOR
    // === Serialize: 씬 → JSON 파일 ===
    [ContextMenu("Save Map To JSON")]
    private void SaveMapInEditor()
    {
        // FindObjectsOfType(T) is obsolete in newer Unity versions.
        // Use FindObjectsByType and explicitly include inactive objects for the same behavior.
        var tileInfos = UnityEngine.Object.FindObjectsByType<TileView>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

        MapSaveJsonDto mapData = new MapSaveJsonDto();
        mapData.tiles.Clear();

        foreach (var info in tileInfos)
        {
            // 혹시 월드에서 그리드 역산해야 하면:
            // info.CaptureGridFromWorld(cellSize);

            TileSaveData td = new TileSaveData
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

            mapData.tiles.Add(td);
        }

        string json = JsonUtility.ToJson(mapData, true);

        string fullPath = GetFullPath();
        File.WriteAllText(fullPath, json);

        Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapData.tiles.Count})");
    }
#endif
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

}