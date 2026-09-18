#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoTilemap.Tests
{
    public sealed class FloorMapIndexIncrementalTests
    {
        TileMapModel _model;
        TileMapCacheHub _hub;
        bool _suppressDefer;
        readonly HashSet<Vector3Int> _visited = new();

        [SetUp]
        public void SetUp()
        {
            _suppressDefer = MapTopologyBakeDeferral.SuppressDefer;
            MapTopologyBakeDeferral.SuppressDefer = true;
            _model = new TileMapModel();
            _hub = TileMapCacheHub.Create(_model, new BuildingGroupRegistry());
            _model.SetMapCacheHub(_hub);
            _visited.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            MapTopologyBakeDeferral.Clear(_hub);
            MapTopologyBakeDeferral.SuppressDefer = _suppressDefer;
        }

        [Test]
        public void OverlappingBoxes_DeleteSibling_MoveAndPatch_KeepLiveReferences()
        {
            var first = Resize(BakeIdSyntheticTiles.Cube(0, 0, 0), new Vector3Int(3, 4, 2));
            var second = Resize(BakeIdSyntheticTiles.Cube(0, 0, 0), new Vector3Int(2, 2, 3));
            var overlap = BakeIdSyntheticTiles.Cube(1, 1, 1);
            Add(first); Add(second); Add(overlap);
            Remove(first);
            _model.PatchTileIdentity(second.tileDefId, 47, 9);
            AssertMatchesRebuild();
            Assert.IsTrue(_hub.Topology.TryGetCellTiles(0, 0, 0, out var live));
            Assert.AreEqual(47, live.Single().identity.buildingId);
            var moved = Resize(second, new Vector3Int(1, 3, 1), new Vector3Int(-3, 5, 0));
            Add(moved);
            // A stale caller snapshot must remove the current tile, including its new box.
            Remove(second);
            Remove(overlap);
        }

        [Test]
        public void TallFaces_ReplacementAndOverlap_PreserveIncidentsAndColumnHeights()
        {
            var tall = Resize(BakeIdSyntheticTiles.Floor(0, 2, 0), new Vector3Int(1, 5, 1));
            var roof = BakeIdSyntheticTiles.Floor(0, 6, 0);
            var wall = Resize(BakeIdSyntheticTiles.ThinWall(0, 0, 0, 1, 0, 0), new Vector3Int(1, 8, 1));
            Add(tall); Add(roof); Add(wall);
            Add(BakeIdSyntheticTiles.Floor(0, 2, 0)); // Different ID, same face, smaller height.
            Assert.IsFalse(_model.TryGetTileById(tall.tileDefId, out _));
            Assert.IsTrue(_hub.Topology.Index.TryGetColumnSealTopY(0, 0, out int top));
            Assert.AreEqual(6, top);
            Remove(roof);
            Assert.IsTrue(_hub.Topology.Index.TryGetColumnSealTopY(0, 0, out top));
            Assert.AreEqual(2, top);
            Add(BakeIdSyntheticTiles.ThinWall(0, 0, 0, 1, 0, 0));
            Assert.IsFalse(_model.TryGetTileById(wall.tileDefId, out _));
        }

        [Test]
        public void StratumAndFloor_SharedWalkableHeight_RemoveEitherContributor()
        {
            var definition = AssetDatabase.LoadAssetAtPath<TileDefinition>(
                "Assets/Dist/SOData/Tile/Terrain/DirtBlock.asset");
            Assert.IsNotNull(definition);
            Assert.IsTrue(MapDigTerrainUtil.IsWalkableStratumBlock(definition));
            var db = ScriptableObject.CreateInstance<TilePrefabDB>();
            db.entries.Add(definition);
            try
            {
                var block = new TileData
                {
                    tileDefId = Guid.NewGuid(),
                    identity = new TileIdentity
                    {
                        PrefabId = definition.prefabId, GridPos = Vector3Int.zero,
                        sizeUnit = new Vector3Int(2, 3, 2),
                        placementSlot = (byte)TilePlacementSlot.OccupiedCell,
                        collisionFlags = (byte)TileCollisionFlags.ProvidesLogicalFloor,
                    },
                };
                var floor = BakeIdSyntheticTiles.Floor(0, 3, 0);
                // These incidents make the support's CellHasFloor contribution seal candidates.
                var incident = Resize(BakeIdSyntheticTiles.ThinWall(0, 0, 0, 1, 0, 0), new Vector3Int(1, 5, 1));
                Add(incident); Add(block); Add(floor); Remove(floor);
                Assert.IsTrue(_hub.Topology.Index.TryGetHighestWalkableFloorAtOrBelow(0, 0, 10, out int y));
                Assert.AreEqual(3, y);
                Add(floor); Remove(block); Remove(floor); Remove(incident);
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void BulkRepeatedReplacement_UsesOriginalRemovalAndFinalAddition()
        {
            var original = Resize(BakeIdSyntheticTiles.Cube(0, 0, 0), new Vector3Int(3, 3, 3));
            Add(original);
            var middle = Resize(original, Vector3Int.one, new Vector3Int(7, 1, 0));
            var final = Resize(original, new Vector3Int(2, 2, 2), new Vector3Int(-2, -2, 0));
            _model.ApplyTiles(new[] { middle, final });
            AssertMatchesRebuild();
            Assert.IsFalse(_hub.Topology.HasOccupancy(7, 0, 1));
            Remove(original);
        }

        [Test]
        public void MetadataApply_PreservesViewLifetime_AndMoveNotifiesAfterOccupancy()
        {
            var tile = BakeIdSyntheticTiles.Cube(0, 0, 0);
            Add(tile);
            int added = 0, removed = 0;
            _model.OnRuntimeTileAdded += current =>
            {
                added++;
                Assert.IsTrue(_hub.Topology.HasOccupancy(current.identity.GridPos.x,
                    current.identity.GridPos.z, current.identity.GridPos.y));
            };
            _model.OnRuntimeTileRemoved += old =>
            {
                removed++;
                Assert.IsFalse(_hub.Topology.HasOccupancy(old.identity.GridPos.x,
                    old.identity.GridPos.z, old.identity.GridPos.y));
            };
            var metadata = new TileData
            {
                tileDefId = tile.tileDefId,
                identity = new TileIdentity
                {
                    PrefabId = tile.identity.PrefabId, GridPos = tile.identity.GridPos,
                    sizeUnit = tile.identity.sizeUnit, placementSlot = tile.identity.placementSlot,
                    collisionFlags = tile.identity.collisionFlags, buildingId = 73, roomId = 4,
                },
            };
            _model.ApplyTiles(new[] { metadata });
            Assert.AreEqual(0, added); Assert.AreEqual(0, removed);
            AssertMatchesRebuild();
            var moved = Resize(metadata, Vector3Int.one, Vector3Int.right * 8);
            _model.ApplyTiles(new[] { moved });
            Assert.AreEqual(1, added); Assert.AreEqual(1, removed);
            AssertMatchesRebuild();
        }

        [Test]
        public void LocalEdit_DoesNotRecreateDistantOccupancyEntry()
        {
            Add(BakeIdSyntheticTiles.Cube(1000, 0, 1000));
            var entries = (IDictionary)typeof(FloorMapIndex).GetField("_occupiedEntries",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_hub.Topology.Index);
            var distant = new Vector3Int(1000, 0, 1000);
            object before = entries[distant];
            var local = BakeIdSyntheticTiles.Cube(0, 0, 0);
            Add(local); Remove(local);
            Assert.AreSame(before, entries[distant], "A local edit rebuilt distant occupancy.");
        }

        [Test]
        public void DeferredBatches_AreMapScoped_AndImmediateEditConsumesOwnPending()
        {
            var otherModel = new TileMapModel();
            var other = TileMapCacheHub.Create(otherModel, new BuildingGroupRegistry());
            var tile = BakeIdSyntheticTiles.Cube(0, 0, 0);
            var change = new TileTopologyChange();
            change.Add(tile);
            try
            {
                MapTopologyBakeDeferral.Enqueue(_hub, change);
                MapTopologyBakeDeferral.Enqueue(other, change);
                Add(tile);
                Assert.IsFalse(MapTopologyBakeDeferral.TryTakePending(_hub, out _));
                Assert.IsTrue(MapTopologyBakeDeferral.TryTakePending(other, out var remaining));
                Assert.AreEqual(tile.tileDefId, remaining.AddedTiles.Single().tileDefId);
            }
            finally { MapTopologyBakeDeferral.Clear(other); }
        }

        [Test]
        public void CoalescedChanges_CancelTransientTiles_RetainOldFootprint()
        {
            var old = BakeIdSyntheticTiles.Cube(0, 0, 0);
            var moved = Resize(old, Vector3Int.one, new Vector3Int(3, 0, 0));
            var first = new TileTopologyChange(); first.Remove(old); first.Add(moved);
            var second = new TileTopologyChange(); second.Remove(moved);
            first.Merge(second);
            Assert.IsEmpty(first.AddedTiles);
            Assert.AreEqual(old.identity.GridPos, first.RemovedTiles.Single().identity.GridPos);
            CollectionAssert.AreEquivalent(new[] { old.identity.GridPos, moved.identity.GridPos }, first.ChangedCells);
            var transient = new TileTopologyChange(); transient.Add(old); transient.Remove(old);
            Assert.IsEmpty(transient.AddedTiles); Assert.IsEmpty(transient.RemovedTiles);
        }

        [Test]
        public void RandomEdits_AllPublicOccupancyQueries_MatchFullRebuild()
        {
            var random = new System.Random(19371);
            for (int step = 0; step < 120; step++)
            {
                var current = _model.TilesSnapshot.ToArray();
                if (current.Length > 0 && random.Next(3) == 0)
                    Remove(current[random.Next(current.Length)]);
                else if (current.Length > 0 && random.Next(4) == 0)
                {
                    var original = current[random.Next(current.Length)];
                    Add(Resize(original, new Vector3Int(2, random.Next(1, 5), 2),
                        new Vector3Int(random.Next(-3, 4), random.Next(-2, 7), random.Next(-3, 4))));
                }
                else
                {
                    var cell = new Vector3Int(random.Next(-3, 4), random.Next(-2, 7), random.Next(-3, 4));
                    var tile = random.Next(3) switch
                    {
                        0 => BakeIdSyntheticTiles.Floor(cell),
                        1 => BakeIdSyntheticTiles.ThinWall(cell, cell + Vector3Int.right),
                        _ => BakeIdSyntheticTiles.Cube(cell),
                    };
                    Add(Resize(tile, new Vector3Int(random.Next(1, 4), random.Next(1, 5), random.Next(1, 4))));
                }
            }
        }

        void Add(TileData tile) { _model.SetTile(tile); AssertMatchesRebuild(); }
        void Remove(TileData tile) { _model.RemoveTile(tile); AssertMatchesRebuild(); }

        void AssertMatchesRebuild()
        {
            var expected = FloorMapIndex.FromModel(_model);
            var actual = _hub.Topology.Index;
            CollectionAssert.AreEquivalent(expected.EnumerateOccupiedCells(), actual.EnumerateOccupiedCells());
            foreach (var (x, z, y) in expected.EnumerateOccupiedCells())
                _visited.Add(new Vector3Int(x, y, z));
            var a = new List<TileData>(); var b = new List<TileData>();
            foreach (var cell in _visited)
            {
                string context = cell.ToString();
                Assert.AreEqual(expected.HasAnyTile(cell.x, cell.z, cell.y), actual.HasAnyTile(cell.x, cell.z, cell.y), context);
                expected.TryCollectTilesAtOccupiedCell(cell, a); actual.TryCollectTilesAtOccupiedCell(cell, b);
                CollectionAssert.AreEquivalent(a, b, context);
                expected.TryGetCellTiles(cell.x, cell.z, cell.y, out var ea);
                actual.TryGetCellTiles(cell.x, cell.z, cell.y, out var aa);
                CollectionAssert.AreEquivalent(ea ?? new List<TileData>(), aa ?? new List<TileData>(), context);
                Assert.AreEqual(expected.CellHasFloor(cell.x, cell.y, cell.z), actual.CellHasFloor(cell.x, cell.y, cell.z), context);
                foreach (var direction in new[] { Vector3Int.right, Vector3Int.forward })
                {
                    Assert.AreEqual(expected.TryGetEdgeSealingLateral(cell, cell + direction, out var ew),
                        actual.TryGetEdgeSealingLateral(cell, cell + direction, out var aw), context);
                    Assert.AreEqual(ew, aw, context);
                }
            }
            foreach (var column in _visited.Select(c => (c.x, c.z)).Distinct())
            {
                Assert.AreEqual(expected.TryGetColumnSealTopY(column.x, column.z, out int et),
                    actual.TryGetColumnSealTopY(column.x, column.z, out int at));
                Assert.AreEqual(et, at, $"seal {column}");
                for (int y = -4; y <= 14; y++)
                {
                    Assert.AreEqual(expected.TryGetHighestWalkableFloorAtOrBelow(column.x, column.z, y, out int ef),
                        actual.TryGetHighestWalkableFloorAtOrBelow(column.x, column.z, y, out int af));
                    Assert.AreEqual(ef, af, $"walkable {column} at {y}");
                }
            }
        }

        static TileData Resize(TileData tile, Vector3Int size, Vector3Int? pos = null) => new TileData
        {
            tileDefId = tile.tileDefId, state = tile.state, plant = tile.plant,
            identity = new TileIdentity
            {
                PrefabId = tile.identity.PrefabId, GridPos = pos ?? tile.identity.GridPos,
                sizeUnit = size, placementSlot = tile.identity.placementSlot,
                wallFace = tile.identity.wallFace, floorFace = tile.identity.floorFace,
                collisionFlags = tile.identity.collisionFlags,
                buildingId = tile.identity.buildingId, roomId = tile.identity.roomId,
            },
        };
    }
}
#endif
