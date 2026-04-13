using IsoTilemap;
using UnityEngine;

/// <summary>
/// 타일맵 생명주기 조율자.
/// 로드 → 컨트롤러/세이버 초기화 → 저장 흐름을 책임집니다.
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

    public IMapModel Model { get; private set; }
    public TilePrefabDB PrefabDB => _prefabDB;

    void Start()
    {
        _loader.Load();
        Model = _loader.Model;

        Transform tileContainer = new GameObject("TileContainer").transform;
        tileContainer.SetParent(_tileContainer);
        var factory = new TileObjFactory(tileContainer, _prefabDB);
        var viewBuilder = new TileMapVisualizer(factory);

        _controller.Init(Model, viewBuilder);
        _saver.Init(Model);
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
        var factory = new TileObjFactory(tileContainer, _prefabDB);
        var viewBuilder = new TileMapVisualizer(factory);
        _controller.Init(Model, viewBuilder);

        Debug.Log("[TileMapManager] LoadEditor 완료.");
    }
#endif
}
