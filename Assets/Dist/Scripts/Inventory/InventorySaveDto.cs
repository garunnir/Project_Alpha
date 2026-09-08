// ============================================================
// InventorySaveDto — 몸통·착용·들기 인벤/장비 JSON DTO
// ============================================================

using System;

[Serializable]
public sealed class ItemInstanceSaveDto
{
    public string uid;
    public int damageLevel;
    /// <summary>JSON 키 유지 — C#은 <see cref="ItemInstance.SelectedLeaf"/>.</summary>
    public bool hasSelectedAction;
    public int selectedAction;
    public int chamberRounds;
    public string chamberAmmoId;
    public int supplyRounds;
    public string supplyAmmoId;
    public int toolCharges;
    public int createdWorldMinute;
    public bool isRotten;
    public bool isCooked;
    public bool isHot;
    public int hotUntilWorldMinute;
}

[Serializable]
public sealed class ItemStackSaveDto
{
    public string uid;
    public string itemId;
    public int count;
    public ItemInstanceSaveDto instance;
    public string loadedMagazineUid;
    public string[] nestedStackUids;
}

[Serializable]
public sealed class InventoryGearSaveDto
{
    public ItemStackSaveDto[] stacks;
    public string[] bodyStackUids;
    public string[] wornStackUids;
    public string wieldLeftUid;
    public string wieldRightUid;
    public bool wieldTwoHand;
}
