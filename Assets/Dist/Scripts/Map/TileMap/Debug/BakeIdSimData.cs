// ============================================================
// [BakeIdSimData] — BakeId 시뮬레이션 씬 직렬화 엔트리 (타일·probe·rule·step)
// ============================================================

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace IsoTilemap
{
    public enum BakeIdSimTileKind : byte
    {
        Floor = 0,
        ThinWall = 1,
        Cube = 2,
    }

    public enum BakeIdSimIdField : byte
    {
        BuildingId = 0,
        RoomId = 1,
        SpaceId = 2,
        IsOutdoor = 3,
    }

    public enum BakeIdSimRuleKind : byte
    {
        SameIds = 0,
        Differ = 1,
        SameBuilding = 2,
        IsOutdoor = 3,
    }

    public enum BakeIdSimStepKind : byte
    {
        None = 0,
        RemoveFloorAtProbe = 1,
        AddFloorAtProbe = 2,
        RemoveThinWallBetweenProbes = 3,
    }

    [Serializable]
    public struct BakeIdSimTileEntry
    {
        [HorizontalGroup("Row"), LabelWidth(36)]
        public BakeIdSimTileKind kind;

        [HorizontalGroup("Row"), LabelWidth(28)]
        public Vector3Int cell;

        [HorizontalGroup("Row"), LabelWidth(28)]
        [ShowIf(nameof(kind), BakeIdSimTileKind.ThinWall)]
        public Vector3Int cellB;

        public static BakeIdSimTileEntry Floor(Vector3Int walkable) =>
            new() { kind = BakeIdSimTileKind.Floor, cell = walkable };

        public static BakeIdSimTileEntry Floor(int x, int y, int z) =>
            Floor(new Vector3Int(x, y, z));

        public static BakeIdSimTileEntry ThinWall(Vector3Int a, Vector3Int b) =>
            new() { kind = BakeIdSimTileKind.ThinWall, cell = a, cellB = b };

        public static BakeIdSimTileEntry Cube(Vector3Int occupied) =>
            new() { kind = BakeIdSimTileKind.Cube, cell = occupied };

        public static BakeIdSimTileEntry Cube(int x, int y, int z) =>
            Cube(new Vector3Int(x, y, z));

        public TileData ToTileData() =>
            kind switch
            {
                BakeIdSimTileKind.Floor => BakeIdSyntheticTiles.Floor(cell),
                BakeIdSimTileKind.ThinWall => BakeIdSyntheticTiles.ThinWall(cell, cellB),
                BakeIdSimTileKind.Cube => BakeIdSyntheticTiles.Cube(cell),
                _ => throw new InvalidOperationException($"Unknown BakeIdSimTileKind {kind}"),
            };

        public static bool TryFromTileData(in TileData tile, out BakeIdSimTileEntry entry)
        {
            entry = default;
            if (TileIdentityUtil.IsHorizontalFace(tile.identity))
            {
                entry = Floor(tile.identity.GridPos);
                return true;
            }

            if (TileIdentityUtil.IsVerticalFace(tile.identity))
            {
                WallEdgeKey key = WallEdgeKey.FromWallTileIdentity(tile.identity);
                entry = ThinWall(key.CellA, key.CellB);
                return true;
            }

            if (TileIdentityUtil.IsOccupiedCell(tile.identity))
            {
                entry = Cube(tile.identity.GridPos);
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public struct BakeIdSimProbe
    {
        [HorizontalGroup("Row"), LabelWidth(40)]
        public string name;

        [HorizontalGroup("Row"), LabelWidth(28)]
        public Vector3Int cell;

        public BakeIdSimProbe(string name, Vector3Int cell)
        {
            this.name = name;
            this.cell = cell;
        }
    }

    [Serializable]
    public struct BakeIdSimRule
    {
        [HorizontalGroup("Kind"), LabelWidth(36)]
        public BakeIdSimRuleKind kind;

        [HorizontalGroup("Probes"), LabelWidth(20)]
        public string probeA;

        [HorizontalGroup("Probes"), LabelWidth(20)]
        [HideIf(nameof(kind), BakeIdSimRuleKind.IsOutdoor)]
        public string probeB;

        [ShowIf(nameof(kind), BakeIdSimRuleKind.Differ)]
        [LabelWidth(70)]
        public BakeIdSimIdField differField;

        [ShowIf(nameof(kind), BakeIdSimRuleKind.IsOutdoor)]
        [LabelWidth(70)]
        public bool outdoorExpected;

        public static BakeIdSimRule SameIds(string a, string b) =>
            new() { kind = BakeIdSimRuleKind.SameIds, probeA = a, probeB = b };

        public static BakeIdSimRule Differ(string a, string b, BakeIdSimIdField field) =>
            new() { kind = BakeIdSimRuleKind.Differ, probeA = a, probeB = b, differField = field };

        public static BakeIdSimRule SameBuilding(string a, string b) =>
            new() { kind = BakeIdSimRuleKind.SameBuilding, probeA = a, probeB = b };

        public static BakeIdSimRule IsOutdoor(string probe, bool expected) =>
            new()
            {
                kind = BakeIdSimRuleKind.IsOutdoor,
                probeA = probe,
                outdoorExpected = expected,
            };
    }

    [Serializable]
    public struct BakeIdSimStep
    {
        [HorizontalGroup("Row"), LabelWidth(36)]
        public BakeIdSimStepKind kind;

        [HorizontalGroup("Row"), LabelWidth(20)]
        public string probeA;

        [HorizontalGroup("Row"), LabelWidth(20)]
        [ShowIf(nameof(kind), BakeIdSimStepKind.RemoveThinWallBetweenProbes)]
        public string probeB;
    }

    public static class BakeIdSimTileUtil
    {
        public static List<TileData> ToTileDataList(IReadOnlyList<BakeIdSimTileEntry> entries)
        {
            var list = new List<TileData>(entries?.Count ?? 0);
            if (entries == null)
                return list;

            for (int i = 0; i < entries.Count; i++)
                list.Add(entries[i].ToTileData());
            return list;
        }

        public static List<BakeIdSimTileEntry> FromTileDataList(IReadOnlyList<TileData> tiles)
        {
            var list = new List<BakeIdSimTileEntry>(tiles?.Count ?? 0);
            if (tiles == null)
                return list;

            for (int i = 0; i < tiles.Count; i++)
            {
                if (BakeIdSimTileEntry.TryFromTileData(tiles[i], out BakeIdSimTileEntry entry))
                    list.Add(entry);
            }

            return list;
        }
    }
}
