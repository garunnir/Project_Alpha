// ============================================================
// BakeTestScenario — 사용자·테스트 유일 퍼사드 (Harness + Reader)
// ============================================================
using System;
using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;

namespace IsoTilemap.Tests
{
    public sealed class BakeTestScenario
    {
        readonly BakeTestHarness _harness;
        IBakeIdReader _lastReader;

        BakeTestScenario(BakeTestHarness harness)
        {
            _harness = harness;
        }

        public IBakeIdReader Reader => _lastReader;

        public static BakeTestScenario FromLayout(IReadOnlyList<TileData> tiles)
        {
            var harness = new BakeTestHarness();
            harness.LoadTilesWithoutBake(tiles);
            return new BakeTestScenario(harness);
        }

        /// <summary>초기 전체 rebake. 이후 Reader 사용.</summary>
        public BakeTestScenario Bake()
        {
            _lastReader = _harness.FullRebake();
            return this;
        }

        public IBakeIdReader Set(TileData tile)
        {
            _lastReader = _harness.ApplySet(tile);
            return _lastReader;
        }

        public IBakeIdReader Remove(TileData tile)
        {
            _lastReader = _harness.ApplyRemove(tile);
            return _lastReader;
        }

        /// <summary>현재 타일 복제 후 FullRebake — Golden 비교용.</summary>
        public IBakeIdReader GoldenRebake()
        {
            BakeTestHarness clone = _harness.CloneFromCurrentTiles();
            return clone.FullRebake();
        }

        public TileData FindTile(Func<TileData, bool> predicate) =>
            _harness.FindTile(predicate);

        public bool TryFindTile(Func<TileData, bool> predicate, out TileData tile) =>
            _harness.TryFindTile(predicate, out tile);
    }
}
