// ============================================================
// DiveTankContextContributor — 인벤 DIVE_TANK 토글
// ============================================================

using System.Collections.Generic;
using IsoTilemap;

public sealed class DiveTankContextContributor : IContextMenuContributor
{
    public void Contribute(
        ItemStack stack,
        InventoryContainer container,
        InventorySession session,
        List<ContextMenuEntry> roots)
    {
        if (MoodGameplayGate.IsBlocked || roots == null)
            return;
        if (!DiveTankService.IsDiveTankItem(stack?.Item))
            return;

        CharacterBreathHost breath = ResolveBreath();
        if (breath == null)
            return;

        bool active = breath.IsActiveTank(stack);
        roots.Add(ContextMenuEntry.Leaf(
            active ? "dive-tank-off" : "dive-tank-on",
            active ? DiveTankContextLabels.Deactivate : DiveTankContextLabels.Activate,
            new DiveTankContextAction(stack, breath)));
    }

    static CharacterBreathHost ResolveBreath()
    {
        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear != null)
        {
            CharacterSwimHost swim = gear.GetBodyComponent<CharacterSwimHost>();
            if (swim?.Breath != null)
                return swim.Breath;
        }

        CharacterSessionHub session = CharacterSessionHub.Player;
        if (session != null)
        {
            CharacterSwimHost swim = session.GetBodyComponent<CharacterSwimHost>();
            if (swim?.Breath != null)
                return swim.Breath;
        }

        return null;
    }
}

public static class DiveTankContextLabels
{
    public const string Activate = "잠수 탱크 켜기";
    public const string Deactivate = "잠수 탱크 끄기";
}

public sealed class DiveTankContextAction : IContextMenuAction
{
    readonly ItemStack _stack;
    readonly CharacterBreathHost _breath;

    public DiveTankContextAction(ItemStack stack, CharacterBreathHost breath)
    {
        _stack = stack;
        _breath = breath;
    }

    public string GetDisabledReason()
    {
        if (_breath == null || _stack == null)
            return "불가";
        if (_breath.IsActiveTank(_stack))
            return null;
        if (_stack.Instance == null || _stack.Instance.ToolCharges <= 0)
            return "충전 없음";
        return null;
    }

    public void Execute()
    {
        _breath?.TryToggleDiveTank(_stack);
    }
}
