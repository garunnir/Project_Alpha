// ============================================================
// MeleeBlockTargetHandler — melee_block_target: Excavate cue → 잠금 Dig 타겟 피해
// ============================================================

using IsoTilemap;

/// <summary>
/// Impact cue는 DigPipeline 홀드 잠금 타겟만 친다 (live Aim 재 Resolve 없음).
/// 잠금/해제는 <see cref="ExcavateHoldPerformDriver"/>.
/// </summary>
public sealed class MeleeBlockTargetHandler : IActionHandler
{
    public string LogicId => ActionHandlerIds.MeleeBlockTarget;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        CharacterActionHost actionHost =
            CharacterBodyResolve.GetInBody<CharacterActionHost>(attacker);
        CharacterDigPipeline pipeline = actionHost != null ? actionHost.DigPipeline : null;
        if (pipeline == null || !pipeline.IsActive)
            return;

        TileDefinition definition = pipeline.ActiveDefinition;
        int damage = attacker.ResolveMeleeBlockBreakDamage(in context, definition);
        if (damage <= 0)
            return;

        pipeline.ApplyDamage(damage);
    }
}
