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
        // Separate maps (including simulations) must never consume each other's edits.
        static readonly Dictionary<TileMapCacheHub, TileTopologyChange> Pending = new();

        public static bool HasPending => Pending.Count > 0;

        public static bool ShouldDefer =>
            Application.isPlaying && !SuppressDefer;

        /// <summary>에디터 bulk ApplyTiles 등 — 이번 호출만 즉시 bake.</summary>
        public static bool SuppressDefer { get; set; }

        public static void Enqueue(
            TileMapCacheHub owner,
            TileTopologyChange change)
        {
            if (change.ChangedCells.Count == 0)
                return;

            if (!Pending.TryGetValue(owner, out var batch))
                Pending.Add(owner, batch = new TileTopologyChange());
            batch.Merge(change);
        }

        public static bool TryTakePending(TileMapCacheHub owner, out TileTopologyChange change)
        {
            if (!Pending.TryGetValue(owner, out change))
                return false;
            Pending.Remove(owner);
            return true;
        }

        public static void Clear(TileMapCacheHub owner) => Pending.Remove(owner);

        public static void Clear() => Pending.Clear();
    }
}
