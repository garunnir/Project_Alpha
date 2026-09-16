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
        SameSpaceIds=4,
    }

    public enum BakeIdSimStepKind : byte
    {
        None = 0,
        RemoveFloorAtProbe = 1,
        AddFloorAtProbe = 2,
        RemoveThinWallBetweenProbes = 3,
        AddCubeAtProbe = 4,
        RemoveCubeAtProbe = 5,
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

        public static bool EntriesEqual(in BakeIdSimTileEntry a, in BakeIdSimTileEntry b)
        {
            if (a.kind != b.kind)
                return false;
            if (a.cell != b.cell)
                return false;
            return a.kind != BakeIdSimTileKind.ThinWall || a.cellB == b.cellB;
        }

        /// <summary>ThinWall A↔B가 same Y 카드널 이웃인지.</summary>
        public static bool IsThinWallEdgeValid(Vector3Int cellA, Vector3Int cellB) =>
            WallEdgeKey.TryBetween(cellA, cellB, out _);

        public bool TryToTileData(out TileData tile, out string error)
        {
            tile = default;
            error = null;
            switch (kind)
            {
                case BakeIdSimTileKind.Floor:
                    tile = BakeIdSyntheticTiles.Floor(cell);
                    return true;

                case BakeIdSimTileKind.ThinWall:
                    if (!IsThinWallEdgeValid(cell, cellB))
                    {
                        error =
                            $"ThinWall requires cardinal neighbors at same Y (Δx or Δz = 1, same y): {cell} ↔ {cellB}";
                        return false;
                    }

                    tile = BakeIdSyntheticTiles.ThinWall(cell, cellB);
                    return true;

                case BakeIdSimTileKind.Cube:
                    tile = BakeIdSyntheticTiles.Cube(cell);
                    return true;

                default:
                    error = $"Unknown BakeIdSimTileKind {kind}";
                    return false;
            }
        }

        public TileData ToTileData()
        {
            if (!TryToTileData(out TileData tile, out string error))
                throw new InvalidOperationException(error);
            return tile;
        }

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
        public static BakeIdSimRule SameSpace(string a, string b) =>
            new()
            {
                kind = BakeIdSimRuleKind.SameSpaceIds,
                probeA = a,
                probeB = b
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
        [ShowIf("@kind == BakeIdSimStepKind.RemoveThinWallBetweenProbes")]
        public string probeB;

        public static BakeIdSimStep AddFloor(string probeA) =>
            new() { kind = BakeIdSimStepKind.AddFloorAtProbe, probeA = probeA };

        public static BakeIdSimStep RemoveFloor(string probeA) =>
            new() { kind = BakeIdSimStepKind.RemoveFloorAtProbe, probeA = probeA };

        public static BakeIdSimStep AddCube(string probeA) =>
            new() { kind = BakeIdSimStepKind.AddCubeAtProbe, probeA = probeA };

        public static BakeIdSimStep RemoveCube(string probeA) =>
            new() { kind = BakeIdSimStepKind.RemoveCubeAtProbe, probeA = probeA };

        public static BakeIdSimStep RemoveThinWall(string probeA, string probeB) =>
            new()
            {
                kind = BakeIdSimStepKind.RemoveThinWallBetweenProbes,
                probeA = probeA,
                probeB = probeB,
            };
    }

    public static class BakeIdSimTileUtil
    {
        public const string DefaultLogPrefix = "[BakeIdSim]";

        public static List<TileData> ToTileDataList(
            IReadOnlyList<BakeIdSimTileEntry> entries,
            out int skippedCount,
            string logPrefix = DefaultLogPrefix)
        {
            TryToTileDataList(entries, out List<TileData> tiles, out skippedCount, logPrefix);
            return tiles;
        }

        /// <summary>invalid 항목은 skip + LogWarning. tiles는 유효한 것만.</summary>
        public static bool TryToTileDataList(
            IReadOnlyList<BakeIdSimTileEntry> entries,
            out List<TileData> tiles,
            out int skippedCount,
            string logPrefix = DefaultLogPrefix)
        {
            tiles = new List<TileData>(entries?.Count ?? 0);
            skippedCount = 0;
            if (entries == null)
                return true;

            for (int i = 0; i < entries.Count; i++)
            {
                BakeIdSimTileEntry entry = entries[i];
                if (entry.TryToTileData(out TileData tile, out string error))
                {
                    tiles.Add(tile);
                    continue;
                }

                skippedCount++;
                Debug.LogWarning(
                    $"{logPrefix} simTiles[{i}] skipped kind={entry.kind} cell={entry.cell} cellB={entry.cellB}: {error}");
            }

            if (skippedCount > 0)
            {
                Debug.LogWarning(
                    $"{logPrefix} skipped {skippedCount}/{entries.Count} invalid simTiles — fix Sim list or re-Import Seed Layout");
            }

            return skippedCount == 0;
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

        /// <summary>BakeIdAuthoring 아래 TileView → simTiles 엔트리. unsupported·invalid thin wall skip.</summary>
        public static List<BakeIdSimTileEntry> FromAuthoringViews(
            IReadOnlyList<TileView> views,
            float cellSize,
            out int skippedCount,
            string logPrefix = DefaultLogPrefix)
        {
            skippedCount = 0;
            var entries = new List<BakeIdSimTileEntry>(views?.Count ?? 0);
            if (views == null || views.Count == 0)
                return entries;

            float cs = Mathf.Max(1e-4f, cellSize);
            for (int i = 0; i < views.Count; i++)
            {
                TileView view = views[i];
                if (view == null)
                {
                    skippedCount++;
                    continue;
                }

                view.gizmoCellSize = cs;
            }

            List<TileData> snapshot = TileViewSceneGather.BuildTileDataSnapshot(views);
            for (int i = 0; i < snapshot.Count; i++)
            {
                TileData tile = snapshot[i];
                if (!BakeIdSimTileEntry.TryFromTileData(tile, out BakeIdSimTileEntry entry))
                {
                    skippedCount++;
                    Debug.LogWarning(
                        $"{logPrefix} authoring view skipped — unsupported prefabId='{tile.identity.PrefabId}' @ {tile.identity.GridPos}");
                    continue;
                }

                if (!entry.TryToTileData(out _, out string error))
                {
                    skippedCount++;
                    Debug.LogWarning(
                        $"{logPrefix} authoring entry skipped kind={entry.kind} cell={entry.cell} cellB={entry.cellB}: {error}");
                    continue;
                }

                if (ContainsEntry(entries, entry))
                {
                    skippedCount++;
                    Debug.LogWarning(
                        $"{logPrefix} duplicate authoring entry skipped kind={entry.kind} cell={entry.cell} cellB={entry.cellB}");
                    continue;
                }

                entries.Add(entry);
            }

            return entries;
        }

        /// <summary>단일 Authoring TileView → sim 엔트리. unsupported·invalid면 false.</summary>
        public static bool TryFromAuthoringView(
            TileView view,
            float cellSize,
            out BakeIdSimTileEntry entry,
            out string error,
            string logPrefix = DefaultLogPrefix)
        {
            entry = default;
            error = null;
            if (view == null)
            {
                error = "view is null";
                return false;
            }

            List<BakeIdSimTileEntry> gathered = FromAuthoringViews(
                new[] { view },
                cellSize,
                out int skipped,
                logPrefix);
            if (gathered.Count == 0)
            {
                error = skipped > 0
                    ? "unsupported or invalid tile"
                    : "no entry gathered";
                return false;
            }

            entry = gathered[0];
            return true;
        }

        static bool ContainsEntry(IReadOnlyList<BakeIdSimTileEntry> entries, in BakeIdSimTileEntry candidate)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (BakeIdSimTileEntry.EntriesEqual(entries[i], candidate))
                    return true;
            }

            return false;
        }
    }
}
