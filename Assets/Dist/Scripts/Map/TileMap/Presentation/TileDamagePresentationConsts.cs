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

        /// <summary>크랙 큐브가 host bounds보다 살짝 커지는 양(월드).</summary>
        public const float CrackBoundsInflate = 0.06f;

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
