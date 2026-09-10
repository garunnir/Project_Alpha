// ============================================================
// MapTopologyBakeDeferral — Play 중 topology bake 프레임 지연·병합
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// <see cref="TileMapModel"/> SetTile/RemoveTile topology bake를 프레임 말까지 지연·병합합니다.
    /// 채굴 cue 등 <see cref="CharacterLocomotionAnim"/> Update 스택에서 동기 bake를 피합니다.
    /// </summary>
    public static class MapTopologyBakeDeferral
    {
        sealed class PendingBatch
        {
            public readonly HashSet<Vector3Int> ChangedCells = new();
            public readonly List<TileData> Removals = new();
        }

        static PendingBatch _pending;

        public static bool HasPending => _pending != null && _pending.ChangedCells.Count > 0;

        public static bool ShouldDefer =>
            Application.isPlaying && !SuppressDefer;

        /// <summary>에디터 bulk ApplyTiles 등 — 이번 호출만 즉시 bake.</summary>
        public static bool SuppressDefer { get; set; }

        public static void Enqueue(
            IReadOnlyCollection<Vector3Int> changedCells,
            bool isRemoval,
            in TileData removedTile)
        {
            if (changedCells == null || changedCells.Count == 0)
                return;

            _pending ??= new PendingBatch();
            foreach (Vector3Int cell in changedCells)
                _pending.ChangedCells.Add(cell);

            if (isRemoval)
                _pending.Removals.Add(removedTile);
        }

        public static bool TryTakePending(out HashSet<Vector3Int> changedCells, out List<TileData> removals)
        {
            if (_pending == null || _pending.ChangedCells.Count == 0)
            {
                changedCells = null;
                removals = null;
                return false;
            }

            changedCells = _pending.ChangedCells;
            removals = _pending.Removals;
            _pending = null;
            return true;
        }

        public static void Clear()
        {
            _pending = null;
        }
    }
}
