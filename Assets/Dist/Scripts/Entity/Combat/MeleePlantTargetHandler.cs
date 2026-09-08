// ============================================================
// MeleePlantTargetHandler — melee_plant_target: Chop cue → 나무 작물 피해
// ============================================================

public sealed class MeleePlantTargetHandler : IActionHandler
{
    public string LogicId => ActionHandlerIds.MeleePlantTarget;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        CharacterActionHost host = CharacterBodyResolve.GetInBody<CharacterActionHost>(attacker);
        CharacterChopPipeline pipeline = host != null ? host.ChopPipeline : null;
        if (pipeline == null || !pipeline.IsActive)
            return;

        int damage = attacker.ResolveMeleePlantChopDamage(in context);
        if (damage <= 0)
            return;

        pipeline.ApplyDamage(damage);
    }
}
