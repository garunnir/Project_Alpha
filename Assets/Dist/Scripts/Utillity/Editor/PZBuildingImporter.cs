// PZBuildingImporter (Editor)
// ScriptedImporter for .tbx custom building files.
//
// Behavior:
// - Runs in the Editor only (file placed under an Editor folder).
// - Parses the .tbx XML and creates a lightweight BuildingData ScriptableObject
//   which is added to the imported asset. Heavy assets (Tilemaps, Prefabs)
//   are NOT created here. Use a separate Bake step (EditorWindow or menu)
//   to turn BuildingData into prefabs/tilemaps.

using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine.Tilemaps;
using System.Xml.Linq;
using System.Collections.Generic;
using System;
using IsoBuilder;

// Types referenced by this importer (BuildingData, PlacedObject, TileCatalog)
// must be defined in compiled assemblies at import time. BuildingData must
// therefore live in a separate compiled file (see BuildingData.cs).

[ScriptedImporter(1, "tbx")]
public class PZBuildingImporter : ScriptedImporter
{
    // Assign a TileCatalog asset in the inspector to allow lookup during baking.
    // We register a dependency on the catalog so changes to it trigger re-import.
    public TileCatalog catalog;

    // Parse width/height from root attributes xml구조에서 필요한 속성과 값을 파싱
    (int width, int height) ParseSize(XDocument xml)
    {
        var root = xml.Root;//가장 최상위 루트 노드를 가져옴
        if (root == null) return (0, 0);
        int width = 0, height = 0;
        var wAttr = root.Attribute("width"); //속성값에 해당하는 값을 가져옴
        var hAttr = root.Attribute("height");
        if (wAttr != null) int.TryParse(wAttr.Value, out width);
        if (hAttr != null) int.TryParse(hAttr.Value, out height);
        return (width, height);
    }
    static readonly Dictionary<ObjType, IFloorObjParser> Parsers = new()
    {
        [ObjType.Wall] = new WallParser(),
        [ObjType.Door] = new DoorParser(),
        [ObjType.Window] = new WindowParser(),
    };

    // 공통 파서에서 타입별 파서 호출
    void EnrichByType(XElement e, FloorObject fo)
    {
        if (Parsers.TryGetValue(fo.Type, out var p))
            p.Parse(e, fo);
    }
    // Convert XML to BuildingData (lightweight) and add as imported asset. 최초 실행
    public override void OnImportAsset(AssetImportContext ctx)
    {
        // Load XML (catch errors so importer doesn't crash hard) 좀보이드 빌딩데이터 구조는 xml이므로 xml 데이터를 읽어옴
        XDocument xml;
        try { xml = XDocument.Load(ctx.assetPath); }
        catch (Exception ex)
        {
            Debug.LogError($"PZBuildingImporter: Failed to load XML '{ctx.assetPath}': {ex}");
            return;
        }

        var (width, height) = ParseSize(xml);

        // Create BuildingData instance (must be a compiled ScriptableObject type)
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.width = width;
        data.height = height;
        data.floors = new List<BuildingData.RoomMaskLayer>();
        data.objects = new List<PlacedObject>();

        // Floors: preserve original rooms CSV as a metadata placed object
        foreach (var floorNode in xml.Root.Elements("floor"))
        {
            var layer = new BuildingData.RoomMaskLayer();
            layer.mask = new int[0, 0]; // placeholder; Bake step may parse CSV into mask
            var roomsCsv = floorNode.Element("rooms")?.Value ?? string.Empty;
            foreach (var obj in floorNode.Elements("object"))
            {
                // 필수 공통 속성
                string typeStr = (string)obj.Attribute("type");      // "wall" / "door" / "window"
                string dirStr = (string)obj.Attribute("dir");       // "N" / "E" / "S" / "W"
                int x = (int?)obj.Attribute("x") ?? 0;
                int y = (int?)obj.Attribute("y") ?? 0;
                int tile = (int?)obj.Attribute("Tile") ?? 0;

                // enum 안전 파싱 (대/소문자 무시)
                if (!Enum.TryParse(typeStr, true, out ObjType type))
                    continue; // 혹은 기본값/로그 등 정책 선택
                if (!Enum.TryParse(dirStr, true, out Dir direction))
                    direction = Dir.N;

                var fo = new FloorObject
                {
                    Type = type,
                    X = x,
                    Y = y,
                    Direction = direction,
                    Tile = tile,

                    // 선택적 속성(없으면 null)
                };
                EnrichByType(obj, fo);
            }
            var meta = new PlacedObject();
            meta.type = "__rooms_csv";
            meta.x = data.floors.Count; // use x to store floor index for this meta
            meta.y = 0;
            meta.dir = string.Empty;
            meta.attrs.Add(new PlacedObject.AttrEntry { key = "roomsCsv", value = roomsCsv });
            data.objects.Add(meta);

            data.floors.Add(layer);
        }

        // Gather object nodes (doors/windows/roof/etc.) into PlacedObject list
        foreach (var floorNode in xml.Root.Elements("floor"))
        {
            foreach (var obj in floorNode.Elements("object"))
            {
                var po = new PlacedObject();
                po.type = (string)obj.Attribute("type") ?? string.Empty;
                po.x = (int?)obj.Attribute("x") ?? 0;
                po.y = (int?)obj.Attribute("y") ?? 0;
                var dirAttr = obj.Attribute("dir");
                po.dir = dirAttr != null ? (string)dirAttr : "N";

                var id = GetTileId(obj);
                if (!string.IsNullOrEmpty(id)) po.attrs.Add(new PlacedObject.AttrEntry { key = "tileId", value = id });

                var openAttr = obj.Attribute("open") ?? obj.Attribute("isOpen");
                if (openAttr != null) po.attrs.Add(new PlacedObject.AttrEntry { key = "open", value = openAttr.Value });

                data.objects.Add(po);
            }
        }

#if UNITY_EDITOR
        // Register dependency on the TileCatalog asset if assigned so changes re-trigger re-import
        if (catalog != null)
        {
            var catPath = AssetDatabase.GetAssetPath(catalog);
            if (!string.IsNullOrEmpty(catPath)) ctx.DependsOnSourceAsset(catPath);
        }
#endif

        // Add BuildingData to the imported asset and make it the main object
        ctx.AddObjectToAsset("data", data);
        ctx.SetMainObject(data);
    }

    // --- Utility helpers used by Bake step (kept here for convenience) ---
    static Vector3Int XmlToUnity(int x, int y, int w, int h) => new Vector3Int(x, (h - 1) - y, 0);

    static string GetTileId(XElement el)
    {
        if (el == null) return null;
        var candidates = new[] { "tileId", "id", "tile", "sprite" };
        foreach (var n in candidates)
        {
            var a = el.Attribute(n);
            if (a != null && !string.IsNullOrEmpty(a.Value)) return a.Value;
        }
        var v = el.Value;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    static Dir ParseDirection(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return Dir.N;
        switch (dir.ToUpperInvariant())
        {
            case "N": return Dir.N;
            case "E": return Dir.E;
            case "S": return Dir.S;
            case "W": return Dir.W;
            default: return Dir.N;
        }
    }

    static bool ParseOpenFlag(XElement el)
    {
        if (el == null) return false;
        var a = el.Attribute("open") ?? el.Attribute("isOpen");
        if (a == null) return false;
        bool b; return bool.TryParse(a.Value, out b) && b;
    }

    // Lightweight placement helpers for Bake step; these are not used during import
    static void PlaceEdgePrefab(GameObject prefab, Transform parent, Vector3Int cell, Dir dir)
    {
        if (prefab == null) return;
        var go = GameObject.Instantiate(prefab);
        go.name = prefab.name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        switch (dir)
        {
            case Dir.N: go.transform.localRotation = Quaternion.Euler(0, 0, 0); break;
            case Dir.E: go.transform.localRotation = Quaternion.Euler(0, 90, 0); break;
            case Dir.S: go.transform.localRotation = Quaternion.Euler(0, 180, 0); break;
            case Dir.W: go.transform.localRotation = Quaternion.Euler(0, 270, 0); break;
        }
    }

    static void PlaceCellPrefab(GameObject prefab, Transform parent, Vector3Int cell, Dir dir)
    {
        if (prefab == null) return;
        var go = GameObject.Instantiate(prefab);
        go.name = prefab.name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    static void PlaceRoofTiles(Tilemap roof, XElement obj, int width, int height, TileCatalog catalog)
    {
        if (roof == null || catalog == null || obj == null) return;
        var id = GetTileId(obj);
        if (string.IsNullOrEmpty(id)) return;
        var sprite = catalog.LookupRoof(id);
        if (sprite == null) return;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                SetTile(roof, XmlToUnity(x, y, width, height), sprite);
    }

    static void SetTile(Tilemap tm, Vector3Int cell, Sprite sprite)
    {
        if (sprite == null) return;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tm.SetTile(cell, tile);
    }
}
