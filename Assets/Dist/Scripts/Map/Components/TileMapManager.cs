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

    public IMapModel Model { get; private set; }

    void Start()
    {
        _loader.Load();
        Model = _loader.Model;
        _controller.Init(Model, _loader.ViewBuilder);
        _saver.Init(Model);
    }

    public void Load() => _loader.Load();
    public void Save() => _saver.Save();
}
