// ============================================================
// TilePresentationResolved — 비저장(Transient) 타일 표현 합성 출력
// ============================================================
namespace IsoTilemap
{
    /// <summary>
    /// 비저장 축 합성. 우선순위: 구조적 숨김 &gt; 시선 가림 &gt; Ghost &gt; Visible.
    /// Selected·VividEmphasis 포함. 저장 파생(크랙)은 이 DTO에 없음 —
    /// <see cref="TileView.ApplyPersistentPresentation"/>.
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

        public TilePresentationResolved(
            bool structuralHidden,
            bool sightLineTrace,
            float characterOcclusion,
            bool ghosted,
            bool selected,
            float vividEmphasis = 0f)
        {
            StructuralHidden = structuralHidden;
            SightLineTrace = sightLineTrace;
            CharacterOcclusion = characterOcclusion;
            Ghosted = ghosted;
            Selected = selected;
            VividEmphasis = vividEmphasis;
        }
    }
}
