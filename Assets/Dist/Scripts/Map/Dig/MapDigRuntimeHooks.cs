// ============================================================
// MapDigRuntimeHooks — Dist.Map ↔ DistScript 런타임 브리지 계약
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

namespace IsoTilemap
{
    public delegate bool TryResolveDigActorWorldDelegate(out Vector3 actorWorld);

    public struct MapDigRuntimeHooks
    {
        public Func<bool> IsMoodBlocked;
        public Func<ItemData, bool> HasDigQuality;
        public Func<bool> PlayerHasDigTool;
        public Func<string> DigBlockedLabel;
        public Action<string, int, Vector3> GrantItem;
        /// <summary>사거리 게이트용 live transform 발끝. <see cref="GridPos"/>와 분리.</summary>
        public TryResolveDigActorWorldDelegate TryResolveActorWorld;

        public static MapDigRuntimeHooks Default => new MapDigRuntimeHooks
        {
            IsMoodBlocked = () => false,
            HasDigQuality = _ => false,
            PlayerHasDigTool = () => false,
            DigBlockedLabel = () => "채굴할 수 없음",
            GrantItem = (_, _, _) => { },
            TryResolveActorWorld = (out Vector3 actorWorld) =>
            {
                actorWorld = default;
                return false;
            },
        };
    }
}
