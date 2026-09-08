// ============================================================
// RaiseGuardHandler — raise_guard: 조준 중 가드, 조준 해제 시 해제
// ============================================================

public sealed class RaiseGuardHandler : IActionHandler
{
    public string LogicId => ActionHandlerIds.RaiseGuard;

    public void Execute(CharacterAttacker attacker, in ActionHandlerContext context)
    {
        if (attacker == null)
            return;

        bool raise = attacker.CanPerform(CombatLeaf.Raise)
            && attacker.SelectedLeaf == CombatLeaf.Raise
            && attacker.IsAiming;
        attacker.ApplyRaiseFromHandler(raise);
    }
}
