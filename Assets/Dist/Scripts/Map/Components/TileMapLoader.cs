using IsoTilemap;
using System.IO;
using UnityEngine;
[DisallowMultipleComponent, RequireComponent(typeof(TileMapSession))]
public class TileMapLoader : MonoBehaviour
{
    [Header("Prefab DB for loading")]
    public TilePrefabDB prefabDB;
    private TileObjFactory _tileFactory;
    private TileMapSession _session;
    [SerializeField] IMapSerializer _serializer;
    [SerializeField] IMapModelBuilder _modelBuilder;
    [SerializeField] IMapViewBuilder _viewBuilder;
    [SerializeField] IMapRuntimeBuilder _runtimeBuilder;
    [SerializeField] IMapMapper _mapper;
    [Header("Where to save/read the map file")]
    [SerializeField] private string fileName = "map01.json";      // 파일 이름
    [SerializeField] private bool usePersistentPath = true;       // Application.persistentDataPath 사용할지
    void Awake()
    {
        _session = GetComponent<TileMapSession>();
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
        MapLoadPipeline pipeline = new MapLoadPipeline(
            serializer: _serializer,
            modelBuilder: _modelBuilder,
            runtimeBuilder: _runtimeBuilder,
            mapper: _mapper);
        IMapSession session = pipeline.LoadModel(GetFullPath());
        _tileFactory = new TileObjFactory(this.transform, prefabDB);
        _viewBuilder = new TileMapVisualizer(_tileFactory);
        _session.Initialize(session, _viewBuilder);
        _viewBuilder.Build(session.Model, _session);
    }
#endif
    public void LoadMapRuntime()
    {
        LoadMapRuntime(GetFullPath());
    }
    public void LoadMapRuntime(string path)
    {
        MapLoadPipeline pipeline = new MapLoadPipeline(
            serializer: _serializer,
            modelBuilder: _modelBuilder,
            runtimeBuilder: _runtimeBuilder,
            mapper: _mapper);
        IMapSession session = pipeline.LoadModel(path);
        _tileFactory = new TileObjFactory(this.transform, prefabDB);
        _viewBuilder = new TileMapVisualizer(_tileFactory);
        _session.Initialize(session, _viewBuilder);
        _viewBuilder.Build(session.Model, _session);
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