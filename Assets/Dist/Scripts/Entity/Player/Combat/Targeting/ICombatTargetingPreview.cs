// ============================================================
// ICombatTargetingPreview — Leaf별 조준 피드백 (Layer1c). Perform와 대칭
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// RMB 조준 프리뷰 라우트. Resolve·CanApply 후 시각만.
/// Layer2 <see cref="ICombatPerformDriver"/>와 대칭 — 시전 없음.
/// </summary>
public interface ICombatTargetingPreview
{
    bool MatchesLeaf(CombatLeaf leaf);

    /// <summary>perform driver Tick 이후 매 프레임.</summary>
    void TickPreview(in CombatPerformContext ctx);

    void ClearPreview();
}
