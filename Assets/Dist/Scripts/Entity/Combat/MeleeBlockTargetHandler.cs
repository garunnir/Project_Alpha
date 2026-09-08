// ============================================================
// MeleeBlockTargetHandler — melee_block_target: Excavate cue → 인접 face 블록 피해
// ============================================================

using IsoTilemap;

public sealed class MeleeBlockTargetHandler : IActionHandler
{
    public string LogicId => ActionHandlerIds.MeleeBlockTarget;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        CharacterActionHost host = CharacterBodyResolve.GetInBody<CharacterActionHost>(attacker);
        CharacterDigPipeline pipeline = host != null ? host.DigPipeline : null;
        if (pipeline == null || !pipeline.IsActive)
            return;

        int damage = attacker.ResolveMeleeBlockBreakDamage(
            in context,
            pipeline.ActiveDefinition);
        if (damage <= 0)
            return;

        pipeline.ApplyDamage(damage);
    }
}
