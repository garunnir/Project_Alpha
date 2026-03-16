using IsoTilemap;
using Sirenix.OdinInspector;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MapFileLoader : SerializedMonoBehaviour
{
    [Header("Prefab DB for loading")]
    [SerializeField] private TilePrefabDB prefabDB;

    [Header("Map file")]
    [SerializeField] private string fileName = "map01.json";
    [SerializeField] private bool usePersistentPath = true;

    public IMapModel Model { get; private set; }
    public IMapViewBuilder ViewBuilder { get; private set; }

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
        Model = new MapLoadPipeline(
            serializer: _serializer,
            modelBuilder: _modelBuilder,
            mapper: _mapper).LoadModel(path);

        var factory = new TileObjFactory(this.transform, prefabDB);
        ViewBuilder = new TileMapVisualizer(factory);
    }

    private string GetFullPath()
    {
        if (usePersistentPath)
            return Path.Combine(Application.persistentDataPath, fileName);
        else
            return Path.Combine(Application.dataPath, "..", fileName);
    }
}
