// ============================================================
// TilePresentationResolved — 타일 화면 표현 SSOT 출력
// ============================================================
namespace IsoTilemap
{
    /// <summary>
    /// Applier가 합성한 최종 표현. 우선순위:
    /// 구조적 숨김 &gt; 시선 가림 강도 &gt; Ghost &gt; Visible.
    /// Selected·VividEmphasis·크랙(DamageStage)은 별도 오버레이축.
    /// </summary>
    public readonly struct TilePresentationResolved
    {
        public bool StructuralHidden { get; }
        public bool SightLineTrace { get; }
        public float CharacterOcclusion { get; }
        public bool Ghosted { get; }
        public bool Selected { get; }
        /// <summary>Add 강조 오버레이 (0 = 없음, 셰이더 _EmphasisAdd).</summary>
        public float VividEmphasis { get; }
        /// <summary>0 = 없음, 1..<see cref="TileDamagePresentationConsts.MaxCrackStage"/>.</summary>
        public int DamageStage { get; }

        public TilePresentationResolved(
            bool structuralHidden,
            bool sightLineTrace,
            float characterOcclusion,
            bool ghosted,
            bool selected,
            int damageStage = 0,
            float vividEmphasis = 0f)
        {
            StructuralHidden = structuralHidden;
            SightLineTrace = sightLineTrace;
            CharacterOcclusion = characterOcclusion;
            Ghosted = ghosted;
            Selected = selected;
            VividEmphasis = vividEmphasis;
            DamageStage = damageStage;
        }
    }
}
