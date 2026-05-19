using IsoTilemap;
using UnityEngine;

/// <summary>
/// 타일맵 생명주기 조율자.
/// 로드 → Factory / ViewBuilder / Controller / Saver 조립.
/// <see cref="IsoWorldGrid"/>가 그리드 규칙의 단일 출처입니다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class TileMapManager : MonoBehaviour
{
    [Header("로드 → 컨트롤러/세이버 초기화 → 저장 흐름을 책임집니다.")]
    [SerializeField] private MapFileLoader _loader;
    [SerializeField] private MapFileSaver _saver;
    [SerializeField] private TileMapController _controller;
    [SerializeField] private Transform _tileContainer;

    [Header("Prefab DB")]
    [SerializeField] private TilePrefabDB _prefabDB;

    [Header("Grid")]
    [SerializeField] private float _gridCellSize = 1f;

    [Header("Chunk Streaming")]
    [Tooltip("연결 시 청크 스트리밍 경로를 사용합니다. 청크·카메라 설정은 TileMapChunkStreamer에 있습니다.")]
    [SerializeField] private TileMapChunkStreamer _chunkStreamer;

    [Header("Tile Pooling (chunk streaming only)")]
    [SerializeField] private bool _enableTilePooling = true;
    [SerializeField, Min(0)] private int _maxPooledInstances = 2000;
    [SerializeField, Min(0f)] private float _maxPoolMemoryMb;
    [SerializeField, Min(1024)] private int _estimatedBytesPerTile = 65536;
    [SerializeField, Range(0f, 0.5f)] private float _poolReserveRatio = 0.15f;
    [SerializeField, Min(0)] private int _minPoolPerPrefab = 1;
    [SerializeField, Min(1)] private int _maxPoolPerPrefab = 256;
    [Tooltip("0이면 맵·스트리밍 설정으로 자동 추정합니다.")]
    [SerializeField, Min(0)] private int _streamingPeakOverride;

    private readonly IsoWorldGrid _worldGrid = new();

    public IMapModel Model { get; private set; }
    public TilePrefabDB PrefabDB => _prefabDB;
    public IWorldGrid WorldGrid => _worldGrid;

    private bool UseChunkStreaming => _chunkStreamer != null;

    void Start()
    {
        _loader.Load();
        Model = _loader.Model;

        _worldGrid.ApplyFromMap(_loader.LastLoadedDto, _gridCellSize);
        BindWorldGridToCharacters();

        Transform tileContainer = new GameObject("TileContainer").transform;
        tileContainer.SetParent(_tileContainer);

        var factory = CreateTileFactory(tileContainer, UseChunkStreaming);
        IMapViewBuilder viewBuilder = CreateViewBuilder(factory, UseChunkStreaming);

        _controller.Init(Model, viewBuilder);
        _chunkStreamer?.SyncNow();
        _saver.Init(Model, _worldGrid);
    }

    private void BindWorldGridToCharacters()
    {
        var states = FindObjectsByType<CharacterState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < states.Length; i++)
            states[i].BindWorldGrid(_worldGrid);
    }

    private TileObjFactory CreateTileFactory(Transform tileContainer, bool chunkStreaming)
    {
        TileViewPoolRegistry pool = null;
        if (chunkStreaming && _enableTilePooling && _loader.LastLoadedDto != null)
        {
            var poolSettings = new TilePoolSettings(
                _maxPooledInstances,
                _maxPoolMemoryMb,
                _estimatedBytesPerTile,
                _poolReserveRatio,
                _minPoolPerPrefab,
                _maxPoolPerPrefab,
                _streamingPeakOverride);

            var streamEstimate = _chunkStreamer.CreatePoolStreamEstimate(_worldGrid);

            var caps = TilePoolBudgetBuilder.Build(
                _loader.LastLoadedDto,
                poolSettings,
                streamEstimate);

            pool = new TileViewPoolRegistry(tileContainer, _prefabDB);
            foreach (var kv in caps)
                pool.RegisterCap(kv.Key, kv.Value);
        }

        return new TileObjFactory(tileContainer, _prefabDB, pool);
    }

    private IMapViewBuilder CreateViewBuilder(TileObjFactory factory, bool chunkStreaming)
    {
        if (!chunkStreaming)
            return new TileMapVisualizer(factory, _worldGrid);

        var streamingVisualizer = new TileMapStreamingVisualizer(
            factory, _worldGrid, _chunkStreamer.ChunkSize);
        _chunkStreamer.Attach(streamingVisualizer, _worldGrid);
        return streamingVisualizer;
    }

    public void Load() => _loader.Load();
    public void Save() => _saver.Save();

#if UNITY_EDITOR
    [ContextMenu("Load Editor")]
    private void LoadEditor()
    {
        if (_tileContainer.childCount > 0)
            DestroyImmediate(_tileContainer.GetChild(0).gameObject);

        _chunkStreamer?.Shutdown();

        _loader.Load();
        Model = _loader.Model;

        if (Model == null)
        {
            Debug.LogError("[TileMapManager] LoadEditor: 맵 로드 실패 — 파일 경로나 JSON을 확인하세요.");
            return;
        }

        _worldGrid.ApplyFromMap(_loader.LastLoadedDto, _gridCellSize);
        BindWorldGridToCharacters();

        Transform tileContainer = new GameObject("TileContainer").transform;
        tileContainer.SetParent(_tileContainer);

        var factory = CreateTileFactory(tileContainer, chunkStreaming: false);
        IMapViewBuilder viewBuilder = CreateViewBuilder(factory, chunkStreaming: false);
        _controller.Init(Model, viewBuilder);

        Debug.Log("[TileMapManager] LoadEditor 완료.");
    }
#endif
}
