// ============================================================
// UIHudQuickSlotController — HUD L/R 들기 슬롯 (장비창 Wield와 동일 데이터)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using UnityEngine;

public sealed class UIHudQuickSlotController : MonoBehaviour
{
    [SerializeField] UICharacterWieldSlotView _leftSlot;
    [SerializeField] UICharacterWieldSlotView _rightSlot;

    CharacterGearService _gear;
    PlayerGearHost _gearHost;
    CharacterAttacker _attacker;
    Canvas _rootCanvas;
    bool _bound;

    void Awake()
    {
        EnsureSlots();
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        TryBind();
        Refresh();
    }

    void OnDisable()
    {
        Unbind();
        HideHover();
    }

    void Update()
    {
        if (!_bound)
            TryBind();

        // Rule 6: fillAmount·name bar만. 할당 없음.
        _leftSlot?.RefreshCooldownFill(_attacker);
        _rightSlot?.RefreshCooldownFill(_attacker);

        if (_gear == null || !_gear.IsBusy)
            return;

        _leftSlot?.RefreshNameBar();
        _rightSlot?.RefreshNameBar();
    }

    void EnsureSlots()
    {
        if (_leftSlot == null)
        {
            Transform left = transform.Find("Scale/Mask/Slot_L")
                ?? transform.Find("Mask/Slot_L")
                ?? transform.Find("Slot_L");
            if (left != null)
                _leftSlot = left.GetComponent<UICharacterWieldSlotView>();
        }

        if (_rightSlot == null)
        {
            Transform right = transform.Find("Scale/Mask/Slot_R")
                ?? transform.Find("Mask/Slot_R")
                ?? transform.Find("Slot_R");
            if (right != null)
                _rightSlot = right.GetComponent<UICharacterWieldSlotView>();
        }

        _leftSlot?.EnsureChrome();
        _rightSlot?.EnsureChrome();
    }

    void TryBind()
    {
        PlayerGearHost host = PlayerGearHost.Active;
        CharacterGearService gear = host?.Service;
        if (gear == null)
            return;

        if (_bound && ReferenceEquals(_gear, gear) && ReferenceEquals(_gearHost, host))
            return;

        Unbind();
        _gear = gear;
        _gearHost = host;
        _attacker = null;
        if (host != null)
        {
            CharacterBodyRefs refs = host.GetBodyRefs();
            _attacker = refs != null ? refs.Attacker : CharacterBodyResolve.GetInBody<CharacterAttacker>(host);
        }
        _gear.Changed += OnGearChanged;
        if (_gearHost != null)
            _gearHost.Changed += OnGearChanged;
        _bound = true;
        Refresh();
    }

    void Unbind()
    {
        if (_gear != null)
            _gear.Changed -= OnGearChanged;
        _gear = null;
        if (_gearHost != null)
            _gearHost.Changed -= OnGearChanged;
        _gearHost = null;
        _attacker = null;
        _bound = false;
    }

    void OnGearChanged() => Refresh();

    void Refresh()
    {
        EnsureSlots();
        if (_gear == null)
            return;

        int strength = ResolveStrength();
        _leftSlot?.Bind(_gear, WieldSlotId.Left, strength, ShowHover, HideHover, OnSlotUnequip);
        _rightSlot?.Bind(_gear, WieldSlotId.Right, strength, ShowHover, HideHover, OnSlotUnequip);
        _leftSlot?.RefreshCooldownFill(_attacker);
        _rightSlot?.RefreshCooldownFill(_attacker);
    }

    static int ResolveStrength()
    {
        if (GameplayData.Stats != null)
            return GameplayData.Stats.GetSkillLevel(AttributeIds.Str);
        if (GameplayData.CharacterSkills != null)
            return GameplayData.CharacterSkills.Level(AttributeIds.Str);
        return 0;
    }

    void ShowHover(string text, RectTransform anchor)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        RectTransform a = anchor != null ? anchor : transform as RectTransform;
        if (!UITextHoverService.TryShowNearAnchor(_rootCanvas, text, a))
        {
            Debug.LogError(
                "[UIHudQuickSlotController] UITextHoverService missing on UICanvas. " +
                "Run Dist/MCP/Inventory/Setup Canvas Overlays In Open Scene.",
                this);
        }
    }

    void HideHover() => UITextHoverService.HideOn(_rootCanvas);

    void OnSlotUnequip(WieldSlotId slot, bool toFloor)
    {
        _gear?.TryBeginUnwieldSlot(slot, toFloor);
    }
}
