using IsoTilemap;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 타일맵 생명주기 조율자.
/// 로드 → Factory/Visualizer/Controller/Saver 조립. 스트리밍 설정은 여기서만 노출합니다.
/// 런타임 청크 틱은 <see cref="TileMapChunkStreamer"/>가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
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
    [Tooltip("비어 있으면 아래 Grid Cell Size를 사용합니다.")]
    [SerializeField] private CharacterState _characterState;
    [SerializeField] private float _gridCellSize = 1f;

    [Header("Chunk Streaming")]
    [SerializeField] private bool _useStreaming = true;
    [SerializeField, Min(1)] private int _chunkSize = 16;
    [SerializeField, Min(0)] private int _playerChunkRadius = 2;
    [SerializeField, Min(0)] private int _cameraChunkMargin = 2;
    [SerializeField] private Camera _gameCamera;
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private TileMapChunkStreamer _chunkStreamer;

    [Header("Tile Pooling (streaming only)")]
    [SerializeField] private bool _enableTilePooling = true;
    [SerializeField, Min(0)] private int _maxPooledInstances = 2000;
    [SerializeField, Min(0f)] private float _maxPoolMemoryMb;
    [SerializeField, Min(1024)] private int _estimatedBytesPerTile = 65536;
    [SerializeField, Range(0f, 0.5f)] private float _poolReserveRatio = 0.15f;
    [SerializeField, Min(0)] private int _minPoolPerPrefab = 1;
    [SerializeField, Min(1)] private int _maxPoolPerPrefab = 256;
    [Tooltip("0이면 맵·스트리밍 설정으로 자동 추정합니다.")]
    [SerializeField, Min(0)] private int _streamingPeakOverride;

    public IMapModel Model { get; private set; }
    public TilePrefabDB PrefabDB => _prefabDB;

    void Start()
    {
        _loader.Load();
        Model = _loader.Model;

        Transform tileContainer = new GameObject("TileContainer").transform;
        tileContainer.SetParent(_tileContainer);
        var factory = CreateTileFactory(tileContainer, streaming: _useStreaming);
        float cellSize = ResolveGridCellSize();
        IMapViewBuilder viewBuilder = CreateViewBuilder(factory, cellSize, streaming: _useStreaming);

        _controller.Init(Model, viewBuilder);
        if (_useStreaming)
            _chunkStreamer?.SyncNow();
        _saver.Init(Model);
    }

    private float ResolveGridCellSize() =>
        _characterState != null ? _characterState.GridCellSize : Mathf.Max(1e-4f, _gridCellSize);

    private TileObjFactory CreateTileFactory(Transform tileContainer, bool streaming)
    {
        TileViewPoolRegistry pool = null;
        if (streaming && _enableTilePooling && _loader.LastLoadedDto != null)
        {
            var poolSettings = new TilePoolSettings(
                _maxPooledInstances,
                _maxPoolMemoryMb,
                _estimatedBytesPerTile,
                _poolReserveRatio,
                _minPoolPerPrefab,
                _maxPoolPerPrefab,
                _streamingPeakOverride);

            var streamEstimate = new TilePoolStreamEstimate(
                _chunkSize,
                _playerChunkRadius,
                _cameraChunkMargin);

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

    private IMapViewBuilder CreateViewBuilder(TileObjFactory factory, float cellSize, bool streaming)
    {
        if (!streaming)
        {
            DisableChunkStreamer();
            return new TileMapVisualizer(factory, cellSize);
        }

        var streamingVisualizer = new TileMapStreamingVisualizer(factory, cellSize, _chunkSize);
        ConfigureChunkStreamer(streamingVisualizer, cellSize);
        return streamingVisualizer;
    }

    private void ConfigureChunkStreamer(TileMapStreamingVisualizer streamingVisualizer, float cellSize)
    {
        if (_chunkStreamer == null)
            _chunkStreamer = GetComponent<TileMapChunkStreamer>();

        if (_chunkStreamer == null)
            _chunkStreamer = gameObject.AddComponent<TileMapChunkStreamer>();

        var settings = new TileChunkStreamSettings(
            cellSize,
            _chunkSize,
            _playerChunkRadius,
            _cameraChunkMargin,
            _gameCamera,
            _cinemachineCamera,
            _characterState);

        _chunkStreamer.Configure(streamingVisualizer, settings);
    }

    private void DisableChunkStreamer()
    {
        if (_chunkStreamer == null)
            _chunkStreamer = GetComponent<TileMapChunkStreamer>();

        _chunkStreamer?.Shutdown();
    }

    public void Load() => _loader.Load();
    public void Save() => _saver.Save();

#if UNITY_EDITOR
    [ContextMenu("Load Editor")]
    private void LoadEditor()
    {
        // 기존 자식 GameObject를 에디터 모드에서 즉시 제거
        if(_tileContainer.childCount > 0)
        {
            DestroyImmediate(_tileContainer.GetChild(0).gameObject);
        }

        _loader.Load();
        Model = _loader.Model;

        if (Model == null)
        {
            Debug.LogError("[TileMapManager] LoadEditor: 맵 로드 실패 — 파일 경로나 JSON을 확인하세요.");
            return;
        }
        Transform tileContainer = new GameObject("TileContainer").transform;
        tileContainer.SetParent(_tileContainer);
        var factory = CreateTileFactory(tileContainer, streaming: false);
        float cellSize = ResolveGridCellSize();
        IMapViewBuilder viewBuilder = CreateViewBuilder(factory, cellSize, streaming: false);
        _controller.Init(Model, viewBuilder);

        Debug.Log("[TileMapManager] LoadEditor 완료.");
    }
#endif
}
