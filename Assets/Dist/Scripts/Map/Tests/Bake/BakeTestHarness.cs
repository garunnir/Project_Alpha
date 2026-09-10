// ============================================================
// BakeTestHarness — prod bake 진입점 단일 캡슐화 (BuildingGroupBuilder는 여기만)
// ============================================================
using System;
using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;

namespace IsoTilemap.Tests
{
    sealed class BakeTestHarness
    {
        TileMapModel _model;
        TileMapCacheHub _hub;
        BuildingGroupBuilder _builder;
        BakeIdReader _reader;

        public BakeTestHarness()
        {
            _model = new TileMapModel();
        }

        public void LoadTilesWithoutBake(IReadOnlyList<TileData> tiles)
        {
            _model.SetMapCacheHub(null);
            _model.SetBuildingGroupBuilder(null);
            _hub = null;
            _builder = null;
            _reader = null;

            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
                _model.SetTile(tiles[i]);
        }

        public IBakeIdReader FullRebake()
        {
            EnsureRuntimeWired();
            _builder.AssignAll();
            return CurrentReader();
        }

        public IBakeIdReader ApplySet(TileData tile)
        {
            EnsureRuntimeWired();
            _model.SetTile(tile);
            return CurrentReader();
        }

        public IBakeIdReader ApplyRemove(TileData tile)
        {
            EnsureRuntimeWired();
            _model.RemoveTile(tile);
            return CurrentReader();
        }

        public TileData FindTile(Func<TileData, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (TileData tile in _model.TilesSnapshot)
            {
                if (predicate(tile))
                    return tile;
            }

            throw new InvalidOperationException("FindTile: no matching tile in model.");
        }

        public bool TryFindTile(Func<TileData, bool> predicate, out TileData tile)
        {
            tile = default;
            if (predicate == null)
                return false;

            foreach (TileData candidate in _model.TilesSnapshot)
            {
                if (!predicate(candidate))
                    continue;

                tile = candidate;
                return true;
            }

            return false;
        }

        public BakeTestHarness CloneFromCurrentTiles()
        {
            var clone = new BakeTestHarness();
            var copy = new List<TileData>(_model.TilesSnapshot);
            clone.LoadTilesWithoutBake(copy);
            return clone;
        }

        void EnsureRuntimeWired()
        {
            if (_hub != null && _builder != null)
                return;

            var registry = new BuildingGroupRegistry();
            _hub = TileMapCacheHub.Create(_model, registry);
            _model.SetMapCacheHub(_hub);
            _builder = new BuildingGroupBuilder(_model, _hub);
            // BindRoomBakeBuilder is internal — AssignAll/SetTile/RemoveTile do not need it.
            // EnsureRoomAtFloorCell lazy path is out of scope for bake ID tests.
            _model.SetBuildingGroupBuilder(_builder);
            _reader = new BakeIdReader(_hub);
        }

        IBakeIdReader CurrentReader()
        {
            if (_reader == null)
                EnsureRuntimeWired();
            return _reader;
        }
    }
}
