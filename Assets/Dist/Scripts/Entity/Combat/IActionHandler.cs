// ============================================================
// IActionHandler — Attack 로직 (명중·피해·탄·사거리). SO가 아님
// ============================================================

public interface IActionHandler
{
    string LogicId { get; }

    void Execute(CharacterAttacker attacker, in ActionHandlerContext context);
}

public readonly struct ActionHandlerContext
{
    public readonly CombatLeaf Leaf;
    public readonly WieldHand Hand;
    public readonly WeaponAttack Attack;
    public readonly CharacterBodyHost Target;
    public readonly float OffenseFactor;
    public readonly string ItemId;
    public readonly ItemInstance Instance;
    public readonly ItemStack Stack;

    public ActionHandlerContext(
        CombatLeaf leaf,
        WieldHand hand,
        WeaponAttack attack,
        CharacterBodyHost target,
        float offenseFactor,
        string itemId,
        ItemInstance instance,
        ItemStack stack)
    {
        Leaf = leaf;
        Hand = hand;
        Attack = attack;
        Target = target;
        OffenseFactor = offenseFactor;
        ItemId = itemId ?? string.Empty;
        Instance = instance;
        Stack = stack;
    }
}
