using IsoTilemap;
using System.IO;
using UnityEngine;
public class TileMapLoader: MonoBehaviour
{
    [SerializeField] IMapSerializer _serializer;
    [SerializeField] IMapDomainBuilder _domainBuilder;
    [SerializeField] IMapViewBuilder _viewBuilder;
    [SerializeField] IMapMapper _mapper;
            [Header("Where to save/read the map file")]
        [SerializeField] private string fileName = "map01.json";      // 파일 이름
        [SerializeField] private bool usePersistentPath = true;       // Application.persistentDataPath 사용할지

    private void Start()
    {
        MapLoadPipeline pipeline = new MapLoadPipeline(
            serializer: _serializer,
            domainBuilder: _domainBuilder,
            viewBuilder: _viewBuilder,
            mapper: _mapper);
        pipeline.Load(GetFullPath());
    }

        //런타임에 사용한다.
        public void SaveMap()
        {
            var mapData = _domainBuilder.GetRuntimeData();

            MapSaveJsonDto mapDatas = _mapper.FromDomain(mapData.tiles);

            string json = JsonUtility.ToJson(mapDatas, true);

            string fullPath = GetFullPath();
            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
        [ContextMenu("Load Map From JSON")]
    public void LoadMap()
    {
        MapLoadPipeline pipeline = new MapLoadPipeline(
            serializer: _serializer,
            domainBuilder: _domainBuilder,
            viewBuilder: _viewBuilder,
            mapper: _mapper);
        pipeline.Load(GetFullPath());
    }
    #if UNITY_EDITOR
        // === Serialize: 씬 → JSON 파일 ===
        [SerializeField,ContextMenu("Save Map To JSON")]
        private void SaveMapInEditor()
        {
            // FindObjectsOfType(T) is obsolete in newer Unity versions.
            // Use FindObjectsByType and explicitly include inactive objects for the same behavior.
            var tileInfos = UnityEngine.Object.FindObjectsByType<TileInfo>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

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
                    tileType =(byte)info.tileType
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