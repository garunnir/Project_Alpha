// ============================================================
// MapDigConsts — 채굴·굴착 타이밍·사거리·드롭 매핑 SSOT
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// Dig 사거리 설계 상수(셀 단위). 월드 반경 = <see cref="ActionRangeWorld"/>.
        /// </summary>
        public const int DigActionRangeCells = 2;

        /// <summary>Aim 주변 diggable 탐색 반경(셀). 사거리 clamp 전 ideal 픽용.</summary>
        public const int DigPickSearchRadiusCells = DigActionRangeCells + 2;

        /// <summary>바깥면 facing 가점 가중(셀² 단위에 곱함).</summary>
        public const float DigOuterFaceFacingWeight = 0.25f;

        /// <summary>Dig RMB 조준 face Add 강조량 (0~1, 셰이더 _EmphasisAdd). <see cref="TilePresentationSystem.SetDigHighlight"/>.</summary>
        public const float AimVividEmphasisAmount = 0.35f;

        /// <summary>지층 생성 폴백 walkable stratum 블록 prefabId (<see cref="TilePrefabDB"/>).</summary>
        public const string DefaultStratumBlockPrefabId = "Terrain/StoneBlock";

        /// <summary>레거시 alias — <see cref="DefaultStratumBlockPrefabId"/>.</summary>
        public const string DefaultStratumFloorPrefabId = DefaultStratumBlockPrefabId;

        /// <summary>액터 발끝→목표 walkable 셀 중심 월드 유클리드 반경.</summary>
        public static float ActionRangeWorld(float cellSize) =>
            DigActionRangeCells * Mathf.Max(1e-4f, cellSize);

        /// <summary>목표 walkable 셀 중심 월드 좌표.</summary>
        public static Vector3 TargetWorld(Vector3Int targetWalkableCell, float cellSize) =>
            TileHelper.ConvertGridToWorldPos(targetWalkableCell, cellSize);

        /// <summary>
        /// Dig 사거리 게이트 SSOT. actorWorld = live transform 발끝, target = walkable 셀 중심.
        /// </summary>
        public static bool IsWithinActionRangeWorld(
            Vector3 actorWorld,
            Vector3Int targetWalkableCell,
            float cellSize)
        {
            float r = ActionRangeWorld(cellSize);
            Vector3 targetWorld = TargetWorld(targetWalkableCell, cellSize);
            return (targetWorld - actorWorld).sqrMagnitude <= r * r;
        }

        static readonly (string PrefabId, string ItemId)[] DropTable =
        {
            ("Floor/GrassFloor", "dirt"),
            ("Floor/Floor", "rock"),
            ("Terrain/DirtBlock", "dirt"),
            ("Terrain/StoneBlock", "rock"),
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
