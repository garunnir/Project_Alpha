// ============================================================
// MapDigConsts — 채굴·굴착 타이밍·사거리·드롭 매핑 SSOT
// ============================================================

using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    public static class MapDigConsts
    {
        /// <summary>벽시계 홀드 대략치(문서·패리티). 실제 break는 Dig cue 피해 누적.</summary>
        public const float BaseBreakSeconds = 2.5f;

        /// <summary>
        /// TileDefinition.breakDurability 미지정 폴백 (level 1 삽 ≈ 5 cue break 패리티).
        /// cue당 피해 SSOT는 <see cref="CombatMath.ResolveExcavateDamage"/> /
        /// <see cref="CombatMath.ResolveStructureDamage"/>.
        /// </summary>
        public const int DefaultDigDurability = 5;

        public const float MaxRayDistance = 200f;

        /// <summary>액터 점유셀에서 목표 walkable 셀까지 XZ Chebyshev 반경 (목표 포함).</summary>
        public const int DigActionRangeCells = 1;

        /// <summary>지층 생성 폴백 바닥 prefabId (<see cref="TilePrefabDB"/>).</summary>
        public const string DefaultStratumFloorPrefabId = "Floor/Floor";

        static readonly (string PrefabId, string ItemId)[] DropTable =
        {
            ("Floor/GrassFloor", "dirt"),
            ("Floor/Floor", "rock"),
        };

        static readonly Dictionary<string, string> DropByPrefabId = BuildDropTable();

        static Dictionary<string, string> BuildDropTable()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < DropTable.Length; i++)
            {
                (string prefabId, string itemId) = DropTable[i];
                if (string.IsNullOrEmpty(prefabId) || string.IsNullOrEmpty(itemId))
                    continue;
                map[prefabId] = itemId;
            }

            return map;
        }


        public static bool TryGetDropItemId(string prefabId, out string itemId)
        {
            itemId = null;
            if (string.IsNullOrEmpty(prefabId))
                return false;

            return DropByPrefabId.TryGetValue(prefabId, out itemId);
        }
    }
}
