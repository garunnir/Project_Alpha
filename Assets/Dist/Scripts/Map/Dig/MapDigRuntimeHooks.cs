// ============================================================
// MapDigRuntimeHooks — Dist.Map ↔ DistScript 런타임 브리지 계약
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

namespace IsoTilemap
{
    public delegate bool TryResolveDigActorCellDelegate(out Vector3Int cell);

    public struct MapDigRuntimeHooks
    {
        public Func<bool> IsMoodBlocked;
        public Func<ItemData, bool> HasDigQuality;
        public Func<bool> PlayerHasDigTool;
        public Func<string> DigBlockedLabel;
        public Action<string, int, Vector3> GrantItem;
        public TryResolveDigActorCellDelegate TryResolveActorCell;

        public static MapDigRuntimeHooks Default => new MapDigRuntimeHooks
        {
            IsMoodBlocked = () => false,
            HasDigQuality = _ => false,
            PlayerHasDigTool = () => false,
            DigBlockedLabel = () => "채굴할 수 없음",
            GrantItem = (_, _, _) => { },
            TryResolveActorCell = (out Vector3Int cell) =>
            {
                cell = default;
                return false;
            },
        };
    }
}
