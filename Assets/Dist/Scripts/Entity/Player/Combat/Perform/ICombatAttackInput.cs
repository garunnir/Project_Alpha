// ============================================================
// ICombatAttackInput — 시전 조작 모양 (Layer2a). Leaf payload와 분리
// ============================================================

/// <summary>
/// 조준+공격 입력 게이트. Click / Hold 구현을 드라이버가 조합한다.
/// <see cref="IAimSightProvider"/>와 대칭 — 해석/조작만, TryPerform 없음.
/// </summary>
public interface ICombatAttackInput
{
    /// <summary>
    /// Click: LMB performed에서 호출 — true면 시전 1회.
    /// Hold: Tick에서 호출 — true면 홀드 중 시전 시도 유지.
    /// </summary>
    bool ShouldFire(in CombatPerformContext ctx);
}

/// <summary>RMB 조준 + LMB click(performed).</summary>
public sealed class AimClickAttackInput : ICombatAttackInput
{
    public static readonly AimClickAttackInput Shared = new();

    public bool ShouldFire(in CombatPerformContext ctx)
    {
        if (!CombatAttackInputGates.PassCommon(in ctx))
            return false;
        return ctx.CharacterState.IsAiming;
    }
}

/// <summary>RMB 조준 + LMB hold. 셀 타겟 UI·메뉴 중이면 false.</summary>
public sealed class AimHoldAttackInput : ICombatAttackInput
{
    public static readonly AimHoldAttackInput Shared = new();

    public bool ShouldFire(in CombatPerformContext ctx)
    {
        if (!CombatAttackInputGates.PassCommon(in ctx))
            return false;
        if (!ctx.CharacterState.IsAiming)
            return false;
        if (CombatAttackInputGates.IsCellTargetOrMenuSuppressed())
            return false;

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerHeld(out bool held) || !held)
            return false;

        return true;
    }
}

/// <summary>Click/Hold 공통 게이트 SSOT.</summary>
public static class CombatAttackInputGates
{
    public static bool PassCommon(in CombatPerformContext ctx)
    {
        if (!ctx.InputEnabled || ctx.Attacker == null || ctx.CharacterState == null)
            return false;
        return ctx.Host == null || !ctx.Host.IsAttackBlockedByUi();
    }

    public static bool IsCellTargetOrMenuSuppressed()
    {
        if (FarmCellTargetSession.IsActive ||
            ConstructionCellTargetSession.IsActive ||
            FishCellTargetSession.IsActive ||
            UIConstruction.IsOpen)
        {
            return true;
        }

        InputManager input = InputManager.Instance;
        return input == null || input.IsUiMenuInputActive;
    }
}
