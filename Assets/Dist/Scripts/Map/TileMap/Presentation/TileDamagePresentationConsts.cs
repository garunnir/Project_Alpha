// ============================================================
// TileDamagePresentationConsts — 타일 내구도 크랙 단계 SSOT
// ============================================================

using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// remaining/max → 크랙 단계. 저장은 HP만; stage는 파생.
    /// </summary>
    public static class TileDamagePresentationConsts
    {
        /// <summary>손상 표시 최대 단계 (1..Max). 0 = 풀 HP·오버레이 없음.</summary>
        public const int MaxCrackStage = 3;

        /// <summary>face 위 크랙 쿼드 Y 오프셋 (월드, z-fight 방지).</summary>
        public const float CrackYOffset = 0.02f;

        /// <summary>셀 대비 크랙 쿼드 수평 스케일.</summary>
        public const float CrackPlaneScale = 0.92f;

        /// <summary>프로시저럴 크랙 텍스처 한 변(px).</summary>
        public const int CrackTextureSize = 64;

        public static int StageFromRemaining(int remaining, int maxHp)
        {
            maxHp = Mathf.Max(1, maxHp);
            remaining = Mathf.Clamp(remaining, 0, maxHp);
            if (remaining >= maxHp)
                return 0;

            float damaged01 = 1f - (remaining / (float)maxHp);
            int stage = Mathf.CeilToInt(damaged01 * MaxCrackStage);
            return Mathf.Clamp(stage, 1, MaxCrackStage);
        }
    }
}
