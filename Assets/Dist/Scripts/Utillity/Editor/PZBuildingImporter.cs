// PZBuildingImporter (Editor): XML 기반 건물 데이터를 Unity Tilemap 및 프리팹으로 변환하는 임포터
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Xml.Linq;
using System.Collections.Generic;
using System;
using IsoBuilder;

[ScriptedImporter(1, "tbx")] // 확장자는 원하는 대로
public class PZBuildingImporter : ScriptedImporter
{
    public TileCatalog catalog;

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

    public override void OnImportAsset(AssetImportContext ctx)
    {
        var xml = XDocument.Load(ctx.assetPath);
        var (width, height) = ParseSize(xml);

        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.width = width;
        data.height = height;
        data.floors = new List<BuildingData.RoomMaskLayer>();
        data.objects = new List<PlacedObject>();

        foreach (var floorNode in xml.Root.Elements("floor"))
        {
            var layer = new BuildingData.RoomMaskLayer();
            layer.mask = new int[0,0];
            var roomsCsv = floorNode.Element("rooms")?.Value ?? string.Empty;
            var meta = new PlacedObject();
            meta.type = "__rooms_csv";
            meta.x = data.floors.Count;
            meta.y = 0;
            meta.dir = "";
            meta.attrs.Add(new PlacedObject.AttrEntry { key = "roomsCsv", value = roomsCsv });
            data.objects.Add(meta);

            data.floors.Add(layer);
        }

        foreach (var floorNode in xml.Root.Elements("floor"))
        {
            foreach (var obj in floorNode.Elements("object"))
            {
                var po = new PlacedObject();
                po.type = (string)obj.Attribute("type");
                po.x = (int)obj.Attribute("x");
                po.y = (int)obj.Attribute("y");
                var dirAttr = obj.Attribute("dir");
                po.dir = dirAttr != null ? (string)dirAttr : "N";
                var id = GetTileId(obj);
                if (!string.IsNullOrEmpty(id)) po.attrs.Add(new PlacedObject.AttrEntry { key = "tileId", value = id });
                var openAttr = obj.Attribute("open") ?? obj.Attribute("isOpen");
                if (openAttr != null) po.attrs.Add(new PlacedObject.AttrEntry { key = "open", value = openAttr.Value });
                data.objects.Add(po);
            }
        }

        if (catalog != null)
        {
            var catPath = AssetDatabase.GetAssetPath(catalog);
            if (!string.IsNullOrEmpty(catPath)) ctx.DependsOnSourceAsset(catPath);
        }

        ctx.AddObjectToAsset("data", data);
        ctx.SetMainObject(data);
    }

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

    static Direction ParseDirection(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return Direction.N;
        switch (dir.ToUpperInvariant())
        {
            case "N": return Direction.N;
            case "E": return Direction.E;
            case "S": return Direction.S;
            case "W": return Direction.W;
            default: return Direction.N;
        }
    }

    static bool ParseOpenFlag(XElement el)
    {
        if (el == null) return false;
        var a = el.Attribute("open") ?? el.Attribute("isOpen");
        if (a == null) return false;
        bool b; return bool.TryParse(a.Value, out b) && b;
    }

    static void PlaceEdgePrefab(GameObject prefab, Transform parent, Vector3Int cell, Direction dir)
    {
        if (prefab == null) return;
        var go = GameObject.Instantiate(prefab);
        go.name = prefab.name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        switch (dir)
        {
            case Direction.N: go.transform.localRotation = Quaternion.Euler(0, 0, 0); break;
            case Direction.E: go.transform.localRotation = Quaternion.Euler(0, 90, 0); break;
            case Direction.S: go.transform.localRotation = Quaternion.Euler(0, 180, 0); break;
            case Direction.W: go.transform.localRotation = Quaternion.Euler(0, 270, 0); break;
        }
    }

    static void PlaceCellPrefab(GameObject prefab, Transform parent, Vector3Int cell, Direction dir)
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
