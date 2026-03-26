using IsoTilemap;
using UnityEngine;

/// <summary>
/// 타일맵 생명주기 조율자.
/// 로드 → 컨트롤러/세이버 초기화 → 저장 흐름을 책임집니다.
/// </summary>
[DisallowMultipleComponent]
public class TileMapManager : MonoBehaviour
{
    [SerializeField] private MapFileLoader _loader;
    [SerializeField] private MapFileSaver _saver;
    [SerializeField] private TileMapController _controller;

    [Header("Prefab DB")]
    [SerializeField] private TilePrefabDB _prefabDB;

    public IMapModel Model { get; private set; }
    public TilePrefabDB PrefabDB => _prefabDB;

    void Start()
    {
        _loader.Load();
        Model = _loader.Model;

        var factory = new TileObjFactory(this.transform, _prefabDB);
        var viewBuilder = new TileMapVisualizer(factory);

        _controller.Init(Model, viewBuilder);
        _saver.Init(Model);
    }

    public void Load() => _loader.Load();
    public void Save() => _saver.Save();
}
