using IsoTilemap;
using Sirenix.OdinInspector;
using System.IO;
using UnityEngine;
[DisallowMultipleComponent]
public class TileMapLoader : SerializedMonoBehaviour
{
    [Header("Prefab DB for loading")]
    public TilePrefabDB prefabDB;
    public IMapModel Model { get; private set; }
    private TileObjFactory _tileFactory;
    [SerializeField] IMapSerializer _serializer;
    [SerializeField] IMapModelBuilder _modelBuilder;
    [SerializeField] IMapViewBuilder _viewBuilder;
    [SerializeField] IMapMapper _mapper;
    [Header("Where to save/read the map file")]
    [SerializeField] private string fileName = "map01.json";      // 파일 이름
    [SerializeField] private bool usePersistentPath = true;       // Application.persistentDataPath 사용할지

    void Awake()
    {
        //런타임에서 자동으로 맵을 불러오도록 설정 (테스트용)
        Initialize();   
    }
    public void Initialize()
    {
        // Initialize any required components or dependencies
        if (_serializer == null || _modelBuilder == null || _mapper == null)
        {
            Debug.LogError("Missing required dependencies in TileMapLoader");
        }
    }
    private void Start()
    {
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
    [SerializeField, ContextMenu("Save Map To JSON")]
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