using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

namespace IsoBuilder
{
    public enum ObjType { Wall, Door, Window }
    public enum Dir { N, E, S, W }

    public sealed class FloorObject
    {
        // 필수 공통
        public ObjType Type;
        public int X;
        public int Y;
        public Dir Direction;
        public int Tile;

        // 나머지 전부
        public Dictionary<string, string> Extras = new();
    }
    public interface IFloorObjParser { void Parse(XElement e, FloorObject fo); }

    public class WallParser : IFloorObjParser
    {
        public void Parse(XElement e, FloorObject fo)
        {
            fo.Extras["length"] = (string)e.Attribute("length") ?? "0";
            fo.Extras["InteriorTile"] = (string)e.Attribute("InteriorTile") ?? "0";
            fo.Extras["ExteriorTrim"] = (string)e.Attribute("ExteriorTrim") ?? "0";
            fo.Extras["InteriorTrim"] = (string)e.Attribute("InteriorTrim") ?? "0";
        }
    }
    public class DoorParser : IFloorObjParser
    {
        public void Parse(XElement e, FloorObject fo)
        {
            fo.Extras["FrameTile"] = (string)e.Attribute("FrameTile") ?? "0";
        }
    }
    public class WindowParser : IFloorObjParser
    {
        public void Parse(XElement e, FloorObject fo)
        {
            fo.Extras["CurtainsTile"] = (string)e.Attribute("CurtainsTile") ?? "0";
            fo.Extras["ShuttersTile"] = (string)e.Attribute("ShuttersTile") ?? "0";
        }
}

static readonly Dictionary<ObjType, IFloorObjParser> Parsers = new()
{
    [ObjType.Wall] = new WallParser(),
    [ObjType.Door] = new DoorParser(),
    [ObjType.Window] = new WindowParser(),
};
    // BuildingData: intermediate ScriptableObject produced by the importer
    [CreateAssetMenu(fileName = "BuildingData", menuName = "IsoBuilder/Building Data", order = 11)]
    public class BuildingData : ScriptableObject
    {
        public int width, height;
        // RoomMaskLayer: each floor's room mask placeholder
        [Serializable]
        public class RoomMaskLayer
        {
            // Unity can't serialize 2D arrays directly; kept for reference only
            public int[,] mask;
        }
        public List<RoomMaskLayer> floors = new List<RoomMaskLayer>();
        public List<PlacedObject> objects = new List<PlacedObject>();
    }

    [Serializable]
    public class PlacedObject
    {
        public string type; // "door","window","wall","roof","stairs" or meta keys like "__rooms_csv"
        public int x, y;
        public string dir;
        [Serializable]
        public class AttrEntry { public string key; public string value; }
        public List<AttrEntry> attrs = new List<AttrEntry>(); // key/value attrs
    }

    // Minimal TileCatalog used by the importer to look up sprites/prefabs by tile id.
    // This is intentionally lightweight: it stores entries and builds runtime dictionaries on demand.
    [CreateAssetMenu(fileName = "TileCatalog", menuName = "IsoBuilder/Tile Catalog", order = 10)]
    public class TileCatalog : ScriptableObject
    {
        [Serializable]
        public class TileSpriteEntry { public string id; public Sprite sprite; }
        [Serializable]
        public class OrientedPrefab { public Dir direction; public bool openState; public GameObject prefab; }
        [Serializable]
        public class TilePrefabEntry { public string id; public GameObject defaultPrefab; public List<OrientedPrefab> variants = new List<OrientedPrefab>(); public GameObject Get(Dir dir, bool open)
            {
                foreach (var v in variants) if (v != null && v.direction == dir && v.openState == open && v.prefab) return v.prefab;
                foreach (var v in variants) if (v != null && v.direction == dir && v.prefab) return v.prefab;
                foreach (var v in variants) if (v != null && v.openState == open && v.prefab) return v.prefab;
                return defaultPrefab;
            }
        }

        public List<TileSpriteEntry> spriteEntries = new List<TileSpriteEntry>();
        public List<TilePrefabEntry> prefabEntries = new List<TilePrefabEntry>();

        Dictionary<string, Sprite> _spriteMap;
        Dictionary<string, TilePrefabEntry> _prefabMap;

        void OnEnable() { BuildCaches(); }

        public void BuildCaches()
        {
            _spriteMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var e in spriteEntries) if (e != null && !string.IsNullOrEmpty(e.id)) _spriteMap[e.id] = e.sprite;
            _prefabMap = new Dictionary<string, TilePrefabEntry>(StringComparer.Ordinal);
            foreach (var e in prefabEntries) if (e != null && !string.IsNullOrEmpty(e.id)) _prefabMap[e.id] = e;
        }

        public Sprite LookupSprite(string tileId) { if (string.IsNullOrEmpty(tileId)) return null; if (_spriteMap == null) BuildCaches(); _spriteMap.TryGetValue(tileId, out var sp); return sp; }
        public GameObject LookupPrefab(string tileId, Dir dir, bool openState) { if (string.IsNullOrEmpty(tileId)) return null; if (_prefabMap == null) BuildCaches(); if (_prefabMap.TryGetValue(tileId, out var ent)) return ent?.Get(dir, openState); return null; }

        public Sprite LookupFloor(string id) => LookupSprite(id);
        public Sprite LookupWall(string id) => LookupSprite(id);
        public Sprite LookupRoof(string id) => LookupSprite(id);
        public GameObject LookupDoor(string id, Dir dir, bool open) => LookupPrefab(id, dir, open);
        public GameObject LookupWindow(string id, Dir dir, bool open) => LookupPrefab(id, dir, open);
        public GameObject LookupStairs(string id, Dir dir) => LookupPrefab(id, dir, false);
    }
}
