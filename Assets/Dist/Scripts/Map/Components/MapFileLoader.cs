using IsoTilemap;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MapFileLoader : MonoBehaviour
{
    [Header("Map file")]
    [SerializeField] private string fileName = "map01.json";
    [SerializeField] private bool usePersistentPath = false;

    public IMapModel Model { get; private set; }

    private IMapSerializer _serializer;
    private IMapModelBuilder _modelBuilder;
    private IMapMapper _mapper;

    void Awake()
    {
        _serializer = new TileMapSerializer();
        _modelBuilder = new TileMapModelBuilder();
        _mapper = new TileMapDtoMapper();
    }

    public void Load()
    {
        Load(GetFullPath());
    }

    public void Load(string path)
    {
        Debug.Log($"[MapFileLoader] 로드 시도 경로: {path}");
        Model = new MapLoadPipeline(
            serializer: _serializer,
            modelBuilder: _modelBuilder,
            mapper: _mapper).LoadModel(path);
    }

    private string GetFullPath()
    {
        if (usePersistentPath)
            return Path.Combine(Application.persistentDataPath, fileName);
        else
            return Path.Combine(Application.dataPath, "..", fileName);
    }
}
