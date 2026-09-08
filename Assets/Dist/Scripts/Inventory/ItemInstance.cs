// ============================================================
// ItemInstance — 스택과 별개의 아이템 개체 (uid·선택 액션·약실·공구 충전)
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// 아이템 실체. 합칠지는 ItemMergePolicy. SelectedLeaf·Chamber는 여기만.
/// Unset(null)이면 WeaponPresentation 기본 행. 약실은 탄 SSOT (메거진이 아님).
/// </summary>
public sealed class ItemInstance
{
    public ItemData Item { get; }
    public int DamageLevel { get; }

    /// <summary>생성 시 부여. 런타임 식별은 개체 참조. 세이브용 예약.</summary>
    public Guid Uid { get; }

    /// <summary>null = unset → SO default row. 해제(unwield) 후에도 이 인스턴스에 유지.</summary>
    public CombatLeaf? SelectedLeaf { get; set; }

    /// <summary>약실 잔여. 발사 SSOT. 메거진 보급과 별개.</summary>
    public int ChamberRounds { get; private set; }

    /// <summary>약실에 들어간 탄 ItemData.id. 잔여 0이면 비움.</summary>
    public string ChamberAmmoId { get; private set; }

    /// <summary>메거진 아이템의 보급 잔여. 약실이 아님.</summary>
    public int SupplyRounds { get; private set; }

    /// <summary>보급 탄 ItemData.id. 잔여 0이면 비움.</summary>
    public string SupplyAmmoId { get; private set; }

    /// <summary>공구 충전 잔여. tool이 없으면 0.</summary>
    public int ToolCharges { get; private set; }

    public const int UnsetCreatedWorldMinute = -1;

    /// <summary>부패 식품 생성 월드 분. <see cref="UnsetCreatedWorldMinute"/> = 미각인.</summary>
    public int CreatedWorldMinute { get; private set; }

    /// <summary>Host 스캔이 갱신. 신선/썩음 병합 키.</summary>
    public bool IsRotten { get; private set; }

    /// <summary>조리 결과 — RAW 칼로리 페널티 무시.</summary>
    public bool IsCooked { get; private set; }

    /// <summary>hot_result 직후. HotUntilWorldMinute 이후 상온.</summary>
    public bool IsHot { get; private set; }

    /// <summary>Hot 만료 월드 절대 분. UnsetCreatedWorldMinute이면 Hot 아님.</summary>
    public int HotUntilWorldMinute { get; private set; }

    public ItemInstance(ItemData item, int damageLevel = 0)
        : this(item, damageLevel, Guid.NewGuid())
    {
    }

    public ItemInstance(ItemData item, int damageLevel, Guid uid)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        DamageLevel = Math.Max(0, damageLevel);
        Uid = uid == Guid.Empty ? Guid.NewGuid() : uid;
        SelectedLeaf = null;
        ChamberRounds = 0;
        ChamberAmmoId = null;
        SupplyRounds = 0;
        SupplyAmmoId = null;
        ToolCharges = item.tool != null ? Math.Max(0, item.tool.initial_charges) : 0;
        CreatedWorldMinute = UnsetCreatedWorldMinute;
        IsRotten = false;
        IsCooked = false;
        IsHot = false;
        HotUntilWorldMinute = UnsetCreatedWorldMinute;
    }

    public static ItemInstance FromSave(ItemData item, int damageLevel, Guid uid, ItemInstanceSaveDto dto)
    {
        var instance = new ItemInstance(item, damageLevel, uid);
        instance.RestoreFromSave(dto);
        return instance;
    }

    public void RestoreFromSave(ItemInstanceSaveDto dto)
    {
        if (dto == null)
            return;

        SelectedLeaf = dto.hasSelectedAction ? (CombatLeaf)dto.selectedAction : null;
        RestoreChamber(dto.chamberRounds, dto.chamberAmmoId);
        RestoreSupply(dto.supplyRounds, dto.supplyAmmoId);
        SetToolCharges(dto.toolCharges);
        CreatedWorldMinute = dto.createdWorldMinute;
        SetRotten(dto.isRotten);
        StampCooked(dto.isCooked);
        if (dto.isHot)
            StampHot(dto.hotUntilWorldMinute);
        else
            ClearHot();
    }

    void RestoreChamber(int rounds, string ammoId)
    {
        ChamberRounds = Math.Max(0, rounds);
        ChamberAmmoId = ChamberRounds > 0 ? ammoId : null;
    }

    void RestoreSupply(int rounds, string ammoId)
    {
        SupplyRounds = Math.Max(0, rounds);
        SupplyAmmoId = SupplyRounds > 0 ? ammoId : null;
    }

    public void SetCreatedWorldMinute(int worldMinute)
    {
        if (CreatedWorldMinute != UnsetCreatedWorldMinute)
            return;
        CreatedWorldMinute = worldMinute;
    }

    public void SetRotten(bool rotten) => IsRotten = rotten;

    public void StampCooked(bool cooked) => IsCooked = cooked;

    public void StampHot(int untilWorldMinute)
    {
        IsHot = true;
        HotUntilWorldMinute = untilWorldMinute;
    }

    public void ClearHot()
    {
        IsHot = false;
        HotUntilWorldMinute = UnsetCreatedWorldMinute;
    }

    public void TickHotAt(int worldMinute)
    {
        if (!IsHot)
            return;
        if (HotUntilWorldMinute == UnsetCreatedWorldMinute)
            return;
        if (worldMinute >= HotUntilWorldMinute)
            ClearHot();
    }

    public void SetChamberRounds(int rounds)
    {
        ChamberRounds = Math.Max(0, rounds);
        if (ChamberRounds <= 0)
            ChamberAmmoId = null;
    }

    public void SetSupplyRounds(int rounds)
    {
        SupplyRounds = Math.Max(0, rounds);
        if (SupplyRounds <= 0)
            SupplyAmmoId = null;
    }

    public int TryAddSupplyRounds(int count, string ammoId, int capacity)
    {
        if (count <= 0 || capacity <= 0 || string.IsNullOrEmpty(ammoId))
            return 0;
        if (SupplyRounds > 0 &&
            !string.Equals(SupplyAmmoId, ammoId, StringComparison.Ordinal))
            return 0;

        int room = capacity - SupplyRounds;
        if (room <= 0)
            return 0;

        int added = count < room ? count : room;
        SupplyRounds += added;
        SupplyAmmoId = ammoId;
        return added;
    }

    public int TryTakeSupplyRounds(int count, out string ammoId)
    {
        ammoId = SupplyAmmoId;
        if (count <= 0 || SupplyRounds <= 0)
        {
            ammoId = null;
            return 0;
        }

        int taken = count < SupplyRounds ? count : SupplyRounds;
        SupplyRounds -= taken;
        if (SupplyRounds <= 0)
        {
            SupplyRounds = 0;
            SupplyAmmoId = null;
        }

        return taken;
    }

    public bool TryAddChamberRound(int capacity, string ammoId = null)
    {
        if (capacity <= 0 || ChamberRounds >= capacity)
            return false;
        ChamberRounds++;
        if (!string.IsNullOrEmpty(ammoId))
            ChamberAmmoId = ammoId;
        return true;
    }

    public bool TryConsumeChamberRound()
    {
        if (ChamberRounds <= 0)
            return false;
        ChamberRounds--;
        if (ChamberRounds <= 0)
            ChamberAmmoId = null;
        return true;
    }

    public bool TryTakeSupplyRound()
    {
        if (TryTakeSupplyRounds(1, out _) <= 0)
            return false;
        return true;
    }

    public void SetToolCharges(int charges) =>
        ToolCharges = Math.Max(0, charges);

    public bool TryConsumeToolCharges(int amount)
    {
        if (amount <= 0 || ToolCharges < amount)
            return false;
        ToolCharges -= amount;
        return true;
    }
}
