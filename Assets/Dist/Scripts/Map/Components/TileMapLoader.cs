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
LoadMapRuntime();
    }
#endif
    public void LoadMapRuntime()
    {
        LoadMapRuntime(GetFullPath());
    }
    public void LoadMapRuntime(string path)
    {
        // 1. 데이터 로드 (DTO 느낌으로 받기)
        IMapSession loadedSession = new MapLoadPipeline(
                serializer: _serializer,
                modelBuilder: _modelBuilder,
                runtimeBuilder: _runtimeBuilder,
                mapper: _mapper).LoadModel(path);

        // 2. 팩토리 & 뷰 준비
        _tileFactory = new TileObjFactory(this.transform, prefabDB);
        _viewBuilder = new TileMapVisualizer(_tileFactory);

        // 3. [핵심] 컨트롤러가 "중재자" 역할
        // 세션한테는 "데이터"만 줍니다.
        _session.Initialize(loadedSession.Runtime);

        // 뷰한테는 "데이터 변화 감지해라"라고 연결해줍니다.
        // (세션이 아니라, 뷰가 런타임 이벤트를 구독하게 함)
        _viewBuilder.Bind(loadedSession.Runtime);

        // 4. 초기 화면 그리기
        // 뷰는 모델(Model)만 보고 1번 그립니다. (_session 필요 없음)
        _viewBuilder.Build(loadedSession.Model);
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