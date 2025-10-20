// PZBuildingImporter: XML 기반 건물 데이터를 Unity Tilemap 및 프리팹으로 변환하는 임포터
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Xml.Linq;
using System.Collections.Generic;
using System;
using IsoBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ScriptedImporter: .pzbuild.xml 파일을 임포트할 때 자동 실행
[ScriptedImporter(1, "pzbuild.xml")] // 확장자는 원하는 대로
public class PZBuildingImporter : ScriptedImporter
{
    // XML의 TileID를 Sprite/Prefab으로 매핑하는 ScriptableObject
    public TileCatalog catalog;

    // XML에서 width, height 속성 파싱
    (int width, int height) ParseSize(XDocument xml)
    {
        var root = xml.Root;
        if (root == null) return (0, 0);

        int width = 0, height = 0;
        var wAttr = root.Attribute("width");
        var hAttr = root.Attribute("height");
        if (wAttr != null) int.TryParse(wAttr.Value, out width);
        if (hAttr != null) int.TryParse(hAttr.Value, out height);

        return (width, height);
    }

    // 에셋 임포트 시 호출: XML을 파싱해 Tilemap/프리팹 구조 생성
    public override void OnImportAsset(AssetImportContext ctx)
    {
    // XML 파일 로드
    var xml = XDocument.Load(ctx.assetPath);
    // 맵 크기 파싱
    var (width, height) = ParseSize(xml);
    // 최상위 GameObject 생성
    var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath));

        // 1) Grid & Tilemaps
    // 그리드 및 각 Tilemap(바닥, 벽, 지붕 등) 생성
    var grid = root.AddComponent<Grid>();
    grid.cellLayout = GridLayout.CellLayout.Isometric; // 또는 Isometric Z as Y
    var floor = CreateTilemap(root, "Tilemap_Floor");
    var wallN = CreateTilemap(root, "Tilemap_Wall_N");
    var wallW = CreateTilemap(root, "Tilemap_Wall_W");
    var roof = CreateTilemap(root, "Tilemap_Roof");
    var props = new GameObject("Props"); props.transform.SetParent(root.transform);

        // 2) Rooms/Floors를 돌며 바닥 채우기
    // 각 층(floor)별로 반복
    foreach (var floorNode in xml.Root.Elements("floor"))
        {
            // 방 정보 파싱 및 마스킹
            var roomsCsv = floorNode.Element("rooms")?.Value;
            var mask = ParseRooms(roomsCsv, width, height);
            // 바닥 타일 배치
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y] > 0)
                    {
                        var unityPos = XmlToUnity(x, y, width, height);
                        var sprite = catalog.LookupFloor("floors_interior_tilesandwood_01_008"); // 또는 Floor 인덱스로 결정
                        if (sprite != null) SetTile(floor, unityPos, sprite);
                    }
                }

            // 3) object 노드들(door/window/stairs/wall/roof)
            // 오브젝트(문, 창문, 벽, 계단, 지붕 등) 배치
            foreach (var obj in floorNode.Elements("object"))
            {
                string type = (string)obj.Attribute("type");
                int x = (int)obj.Attribute("x");
                int y = (int)obj.Attribute("y");
                string dir = (string)obj.Attribute("dir") ?? "N";
                var unityPos = XmlToUnity(x, y, width, height);

                // 타입별로 분기 처리
                switch (type)
                {
                    case "wall":
                        // dir==N → N전용 타일맵, dir==W → W전용 타일맵에 타일 배치
                        var wallSprite = catalog.LookupWall(obj, dir);
                        var tm = (dir == "N") ? wallN : wallW;
                        SetTile(tm, unityPos, wallSprite);
                        break;
                    case "door":
                        var doorPrefab = catalog.LookupDoor(obj, dir);
                        PlaceEdgePrefab(doorPrefab, props.transform, unityPos, dir);
                        break;
                    case "window":
                        var winPrefab = catalog.LookupWindow(obj, dir);
                        PlaceEdgePrefab(winPrefab, props.transform, unityPos, dir);
                        break;
                    case "stairs":
                        var stairsPrefab = catalog.LookupStairs(obj, dir);
                        PlaceCellPrefab(stairsPrefab, props.transform, unityPos, dir);
                        break;
                    case "roof":
                        // width×height 영역 루프 돌며 경사/캡 선택 로직
                        PlaceRoofTiles(roof, obj, width, height, catalog);
                        break;
                }
            }
        }

        // 4) 결과 등록
    // 결과 오브젝트를 에셋에 등록
    ctx.AddObjectToAsset("root", root);
    ctx.SetMainObject(root);
    }

    // Tilemap GameObject 생성 및 컴포넌트 추가
    static Tilemap CreateTilemap(GameObject root, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>();
        return go.GetComponent<Tilemap>();
    }

    // XML 좌표를 Unity 좌표계로 변환
    static Vector3Int XmlToUnity(int x, int y, int w, int h) => new Vector3Int(x, (h - 1) - y, 0);

    // Tilemap에 Sprite를 타일로 배치
    static void SetTile(Tilemap tm, Vector3Int cell, Sprite sprite)
    {
        if (sprite == null) return;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tm.SetTile(cell, tile);
    }

    // ParseSize/ParseRooms/Lookup* 등은 별도 유틸로 분리

    /// <summary>
    /// roomsCsv(예: "0,1,1,0\n1,1,0,0")를 받아 width×height int[,] 마스크 배열로 변환
    /// 방이 있으면 1, 없으면 0
    /// </summary>
    static int[,] ParseRooms(string roomsCsv, int width, int height)
    {
        var mask = new int[width, height];
        if (string.IsNullOrEmpty(roomsCsv)) return mask;
        var lines = roomsCsv.Split(new[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
        for (int y = 0; y < Math.Min(height, lines.Length); y++)
        {
            var cells = lines[y].Split(',');
            for (int x = 0; x < Math.Min(width, cells.Length); x++)
            {
                int val = 0;
                int.TryParse(cells[x], out val);
                mask[x, y] = val;
            }
        }
        return mask;
    }
}
// 건물 데이터 저장용 ScriptableObject
[CreateAssetMenu]
public class BuildingData : ScriptableObject
{
    public int width, height;
    // RoomMaskLayer: 각 층의 방 마스크를 표현하는 간단한 래퍼
    [Serializable]
    public class RoomMaskLayer {
        public int[,] mask;
    }
    public List<RoomMaskLayer> floors;     // 각 층의 방 마스크 정보
    public List<PlacedObject> objects;     // 배치된 오브젝트 정보
}

[System.Serializable]
public class PlacedObject
{
    public string type; // "door","window","wall","roof","stairs"
    public int x, y; public string dir;
    // SerializableDict: 직렬화 가능한 string→string 딕셔너리 예시
    [Serializable]
    public class SerializableDict<TKey, TValue> : Dictionary<TKey, TValue> { }
    public SerializableDict<string, string> attrs; // 추가 속성 (FrameTile, Tile 등)
}


namespace IsoBuilder
{
    // 방향 정보 (북, 동, 남, 서)
    public enum Direction { N, E, S, W }

    /// <summary>
    /// XML tile id (예: "walls_exterior_house_01_032") → Sprite 매핑
    /// 바닥, 벽, 지붕 등 모든 타일에 사용
    /// </summary>
    [Serializable]
    public class TileSpriteEntry
    {
        [Tooltip("Exact tile id string from XML (e.g., floors_interior_tilesandwood_01_008)")]
        public string id;
        public Sprite sprite;
    }

    /// <summary>
    /// 방향별 프리팹(문, 창문, 계단 등)
    /// </summary>
    [Serializable]
    public class OrientedPrefab
    {
        public Direction direction = Direction.N;
        [Tooltip("For doors/windows: true = open state prefab, false = closed state prefab")] public bool openState = false;
        public GameObject prefab;
    }

    /// <summary>
    /// XML tile id → 프리팹 매핑 (방향/상태별 변형 지원, 없으면 기본 프리팹)
    /// </summary>
    [Serializable]
    public class TilePrefabEntry
    {
        [Tooltip("Exact tile id string from XML (e.g., fixtures_doors_01_036)")] public string id;
        public GameObject defaultPrefab;
        public List<OrientedPrefab> variants = new List<OrientedPrefab>();

    // 방향/상태별 프리팹 반환
    public GameObject Get(Direction dir, bool open)
        {
            // 1) exact direction+state
            foreach (var v in variants)
                if (v.direction == dir && v.openState == open && v.prefab)
                    return v.prefab;
            // 2) direction-only
            foreach (var v in variants)
                if (v.direction == dir && v.prefab)
                    return v.prefab;
            // 3) state-only
            foreach (var v in variants)
                if (v.openState == open && v.prefab)
                    return v.prefab;
            // 4) default
            return defaultPrefab;
        }
    }

    /// <summary>
    /// 임포터/빌더에서 사용하는 ScriptableObject 카탈로그
    /// 타일맵용 스프라이트, 프리팹 조회 기능 제공
    /// </summary>
    [CreateAssetMenu(fileName = "TileCatalog", menuName = "IsoBuilder/Tile Catalog", order = 10)]
    public class TileCatalog : ScriptableObject
    {
        [Header("Sprites (tile id → Sprite)")]
        public List<TileSpriteEntry> spriteEntries = new List<TileSpriteEntry>();

        [Header("Prefabs (tile id → Prefab variants)")]
        public List<TilePrefabEntry> prefabEntries = new List<TilePrefabEntry>();

        // Runtime dictionaries (built on demand)
        private Dictionary<string, Sprite> _spriteMap;
        private Dictionary<string, TilePrefabEntry> _prefabMap;

    // 에셋 활성화 시 캐시 빌드
    void OnEnable()
        {
            BuildCaches();
        }

    // 런타임 딕셔너리 빌드 (id→Sprite, id→Prefab)
    public void BuildCaches()
        {
            _spriteMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var e in spriteEntries)
            {
                if (string.IsNullOrEmpty(e?.id)) continue;
                if (!_spriteMap.ContainsKey(e.id)) _spriteMap.Add(e.id, e.sprite);
                else _spriteMap[e.id] = e.sprite; // last wins
            }

            _prefabMap = new Dictionary<string, TilePrefabEntry>(StringComparer.Ordinal);
            foreach (var e in prefabEntries)
            {
                if (string.IsNullOrEmpty(e?.id)) continue;
                if (!_prefabMap.ContainsKey(e.id)) _prefabMap.Add(e.id, e);
                else _prefabMap[e.id] = e; // last wins
            }
        }

        /// <summary>
        /// Direct sprite lookup by XML tile id.
        /// Returns null if not found.
        /// </summary>
    // tileId로 스프라이트 조회
    public Sprite LookupSprite(string tileId)
        {
            if (string.IsNullOrEmpty(tileId)) return null;
            if (_spriteMap == null) BuildCaches();
            _spriteMap.TryGetValue(tileId, out var sp);
            return sp;
        }

        /// <summary>
        /// Direct prefab lookup by XML tile id + direction + open-state.
        /// Returns null if not found.
        /// </summary>
    // tileId+방향+상태로 프리팹 조회
    public GameObject LookupPrefab(string tileId, Direction dir, bool openState)
        {
            if (string.IsNullOrEmpty(tileId)) return null;
            if (_prefabMap == null) BuildCaches();
            if (_prefabMap.TryGetValue(tileId, out var entry))
                return entry?.Get(dir, openState);
            return null;
        }

        // Convenience aliases for readability in import/builder code
    // 가독성을 위한 별칭 함수들
    public Sprite LookupFloor(string tileId) => LookupSprite(tileId);
    public Sprite LookupWall(string tileId) => LookupSprite(tileId);
    public Sprite LookupRoof(string tileId) => LookupSprite(tileId);

    public GameObject LookupDoor(string tileId, Direction dir, bool open) => LookupPrefab(tileId, dir, open);
    public GameObject LookupWindow(string tileId, Direction dir, bool open) => LookupPrefab(tileId, dir, open);
    public GameObject LookupStairs(string tileId, Direction dir) => LookupPrefab(tileId, dir, false);

#if UNITY_EDITOR
    // 에디터에서 누락된 스프라이트/프리팹 검사
    [ContextMenu("Validate & Report Missing")]
        void ValidateAndReport()
        {
            var missingSprites = new List<string>();
            foreach (var e in spriteEntries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.id) || e.sprite != null) continue;
                missingSprites.Add(e.id);
            }

            var missingPrefabs = new List<string>();
            foreach (var e in prefabEntries)
            {
                if (e == null) continue;
                bool hasAny = e.defaultPrefab != null;
                if (!hasAny && e.variants != null)
                {
                    foreach (var v in e.variants) { if (v != null && v.prefab) { hasAny = true; break; } }
                }
                if (!hasAny) missingPrefabs.Add(e.id);
            }

            if (missingSprites.Count == 0 && missingPrefabs.Count == 0)
            {
                Debug.Log("TileCatalog: all good. No missing assets.");
                return;
            }

            if (missingSprites.Count > 0)
                Debug.LogWarning($"TileCatalog: Missing Sprites for ids: {string.Join(", ", missingSprites)}");
            if (missingPrefabs.Count > 0)
                Debug.LogWarning($"TileCatalog: Missing Prefabs for ids: {string.Join(", ", missingPrefabs)}");
        }

    // 에디터 메뉴에서 TileCatalog 생성
    [MenuItem("Assets/Create/IsoBuilder/Tile Catalog")]
        static void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<TileCatalog>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/TileCatalog.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
#endif
    }
}
