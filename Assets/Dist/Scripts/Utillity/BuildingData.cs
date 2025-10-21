using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoBuilder
{
    // BuildingData: intermediate ScriptableObject produced by the importer
    [CreateAssetMenu]
    public class BuildingData : ScriptableObject
    {
        public int width, height;
        // RoomMaskLayer: each floor's room mask placeholder (kept for compatibility)
        [Serializable]
        public class RoomMaskLayer
        {
            // Unity can't serialize 2D arrays directly; keep as an implementation detail
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
}
