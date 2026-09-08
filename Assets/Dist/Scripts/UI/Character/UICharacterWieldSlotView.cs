// ============================================================
// UICharacterWieldSlotView — L/R 들기 슬롯 (아이콘·액션·탄약·쿨·호버·해제)
// ============================================================

using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UICharacterWieldSlotView :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    const string AmmoObjectName = "Ammo";
    const string LegacyAmmoObjectName = "tmp";
    public const string CooldownFillObjectName = "CoolTime_Radial_Fill";

    [SerializeField] Image _itemIcon;
    [SerializeField] Image _actionIcon;
    [SerializeField] TMP_Text _actionLabel;
    [SerializeField] TMP_Text _ammoLabel;
    [SerializeField] TMP_Text _label;
    [SerializeField] Image _cooldownFill;

    ItemNameStatusBar _nameBar;
    CharacterGearService _gear;
    WieldSlotId _slot;
    int _strength;
    Action<string, RectTransform> _onHover;
    Action _onExit;
    Action<WieldSlotId, bool> _onUnequip;
    UIItemDragGhostService _dragGhost;
    bool _dragging;

    public void EnsureChrome()
    {
        EnsureDropRaycast();
        EnsureItemIcon();
        EnsureActionIcon();
        EnsureAmmoLabel();
        EnsureLabelForProgressBar();
        EnsureCooldownFill();
    }

    void EnsureDropRaycast()
    {
        if (!TryGetComponent(out Image bg))
        {
            WarnRuntimeAddComponentFallback("Image");
            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
        }

        bg.raycastTarget = true;
    }

    void EnsureItemIcon()
    {
        if (_itemIcon != null)
            return;

        Transform t = transform.Find("Icon");
        if (t != null)
            _itemIcon = t.GetComponent<Image>();
        if (_itemIcon != null)
        {
            _itemIcon.raycastTarget = false;
            return;
        }

        WarnRuntimeAddComponentFallback("Icon");
        GameObject go = new("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(GearConstants.WieldIconSize, GearConstants.WieldIconSize);
        _itemIcon = go.GetComponent<Image>();
        _itemIcon.preserveAspect = true;
        _itemIcon.raycastTarget = false;
    }

    void EnsureActionIcon()
    {
        if (_actionIcon != null && _actionLabel != null)
        {
            DistUiFont.Apply(_actionLabel);
            return;
        }

        Transform t = transform.Find("ActionIcon");
        if (t != null)
        {
            if (_actionIcon == null)
                _actionIcon = t.GetComponent<Image>();
            Transform labelTf = t.Find("Label");
            if (labelTf != null && _actionLabel == null)
                _actionLabel = labelTf.GetComponent<TMP_Text>();
        }

        if (_actionIcon != null)
        {
            _actionIcon.raycastTarget = false;
            DistUiFont.Apply(_actionLabel);
            return;
        }

        WarnRuntimeAddComponentFallback("ActionIcon");
        GameObject go = new("ActionIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        // Prefab SSOT preferred; fallback = top-left (shared HUD/Character chrome).
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(2f, -2f);
        rt.sizeDelta = new Vector2(GearConstants.WieldActionIconSize, GearConstants.WieldActionIconSize);
        _actionIcon = go.GetComponent<Image>();
        _actionIcon.color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        _actionIcon.raycastTarget = false;

        GameObject labelGo = new("Label", typeof(RectTransform), typeof(CanvasRenderer));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        _actionLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _actionLabel.fontSize = GearConstants.UiFontSizeActionIcon;
        _actionLabel.alignment = TextAlignmentOptions.Center;
        _actionLabel.raycastTarget = false;
        DistUiFont.Apply(_actionLabel);
    }

    void EnsureAmmoLabel()
    {
        if (_ammoLabel != null)
        {
            DistUiFont.Apply(_ammoLabel);
            _ammoLabel.raycastTarget = false;
            return;
        }

        Transform ammoTf = transform.Find(AmmoObjectName);
        if (ammoTf == null)
            ammoTf = transform.Find(LegacyAmmoObjectName);
        if (ammoTf != null)
            _ammoLabel = ammoTf.GetComponent<TMP_Text>();

        if (_ammoLabel != null)
        {
            if (ammoTf != null && ammoTf.name != AmmoObjectName)
                ammoTf.name = AmmoObjectName;
            DistUiFont.Apply(_ammoLabel);
            _ammoLabel.raycastTarget = false;
            return;
        }

        WarnRuntimeAddComponentFallback(AmmoObjectName);
        GameObject go = new(AmmoObjectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-2f, -2f);
        rt.sizeDelta = new Vector2(48f, 14f);
        _ammoLabel = go.AddComponent<TextMeshProUGUI>();
        _ammoLabel.fontSize = GearConstants.UiFontSizeActionIcon;
        _ammoLabel.alignment = TextAlignmentOptions.TopRight;
        _ammoLabel.raycastTarget = false;
        DistUiFont.Apply(_ammoLabel);
    }

    void EnsureLabelForProgressBar()
    {
        if (_label != null)
        {
            DistUiFont.Apply(_label);
            _nameBar = ItemNameStatusBar.Ensure(ref _label);
            return;
        }

        Transform labelChild = transform.Find(ItemNameStatusBar.LabelObjectName);
        if (labelChild != null)
            _label = labelChild.GetComponent<TMP_Text>();
        if (_label == null)
        {
            WarnRuntimeAddComponentFallback(ItemNameStatusBar.LabelObjectName);
            GameObject labelGo = new(ItemNameStatusBar.LabelObjectName, typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.transform.SetParent(transform, false);
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
        }

        // Plan: no always-on name on wield — keep for name-overlay bar only.
        _label.text = string.Empty;
        _label.fontSize = 1f;
        _label.color = new Color(1f, 1f, 1f, 0f);
        _label.raycastTarget = false;
        DistUiFont.Apply(_label);
        _nameBar = ItemNameStatusBar.Ensure(ref _label);
    }

    void EnsureCooldownFill()
    {
        if (_cooldownFill == null)
        {
            Transform t = transform.Find(CooldownFillObjectName);
            if (t != null)
                t.TryGetComponent(out _cooldownFill);
        }

        if (_cooldownFill == null)
            return;

        _cooldownFill.raycastTarget = false;
        _cooldownFill.fillAmount = 0f;
    }

    void WarnRuntimeAddComponentFallback(string chromeName)
    {
        Debug.LogWarning(
            $"[UICharacterWieldSlotView] runtime AddComponent fallback: '{chromeName}'. Prefab should already have it.",
            this);
    }

    public void RefreshCooldownFill(CharacterAttacker attacker)
    {
        if (_cooldownFill == null)
            return;

        ItemStack stack = _gear?.Wield?.Get(_slot);
        if (attacker == null || stack?.Item == null)
        {
            _cooldownFill.fillAmount = 0f;
            return;
        }

        WieldHand hand = CharacterAttacker.AnimHandFrom(_gear.Wield, _slot);
        _cooldownFill.fillAmount = attacker.GetCooldownOverlay01(hand);
    }

    public void Bind(
        CharacterGearService gear,
        WieldSlotId slot,
        int strength,
        Action<string, RectTransform> onHover,
        Action onExit,
        Action<WieldSlotId, bool> onUnequip)
    {
        EnsureChrome();
        _gear = gear;
        _slot = slot;
        _strength = strength;
        _onHover = onHover;
        _onExit = onExit;
        _onUnequip = onUnequip;

        ItemStack stack = gear?.Wield?.Get(slot);
        if (stack?.Item == null)
        {
            if (_itemIcon != null)
            {
                _itemIcon.enabled = true;
                _itemIcon.sprite = ItemVisualPresenter.GetDefaultIcon();
                _itemIcon.color = new Color(1f, 1f, 1f, 0.25f);
            }

            SetActionVisual(null);
            SetAmmoVisual(null);
            _nameBar?.Clear();
            if (_cooldownFill != null)
                _cooldownFill.fillAmount = 0f;
            return;
        }

        if (_itemIcon != null)
        {
            _itemIcon.enabled = true;
            _itemIcon.sprite = ItemVisualPresenter.GetDisplayIcon(stack.ItemId);
            _itemIcon.color = Color.white;
        }

        WeaponPresentation presentation = CombatLeafRows.Resolve(gear.PresentationCatalog, stack);
        CombatLeaf action = CombatLeafRows.ResolveSelected(stack.Instance, presentation);
        SetActionVisual(action);
        SetAmmoVisual(stack);
        RefreshNameBar();
    }

    void SetActionVisual(CombatLeaf? action)
    {
        if (_actionLabel == null)
            return;

        if (action == null)
            _actionLabel.text = "—";
        else
            _actionLabel.text = CharacterGearLabels.ActionLabel(action.Value);
    }

    void SetAmmoVisual(ItemStack stack)
    {
        if (_ammoLabel == null)
            return;

        string text = ItemAmmoLabels.FormatWieldGunRounds(stack);
        _ammoLabel.text = text;
        _ammoLabel.enabled = !string.IsNullOrEmpty(text);
    }

    public void RefreshNameBar()
    {
        if (_nameBar == null)
            return;

        ItemStack stack = _gear?.Wield?.Get(_slot);
        ItemTimedNameProgress.Apply(_nameBar, stack);
    }

    public void RefreshAmmo()
    {
        ItemStack stack = _gear?.Wield?.Get(_slot);
        SetAmmoVisual(stack?.Item != null ? stack : null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemStack stack = _gear?.Wield?.Get(_slot);
        if (stack?.Item == null)
            return;

        bool twoHand = _gear.Wield.IsTwoHand;
        int required = GearHandleRules.RequiredStr(stack.Item, twoHand);
        bool strain = GearHandleRules.HasLiftStrain(_strength, stack.Item, twoHand);
        var sb = new StringBuilder(160);
        sb.Append(ItemAmmoLabels.AppendState(UITextPresenter.GetItemName(stack.Item), stack))
            .Append('\n');
        sb.Append(CharacterGearLabels.FormatRequiredStr(required, _strength, strain));
        _onHover?.Invoke(sb.ToString(), transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData) => _onExit?.Invoke();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_gear == null)
            return;

        ItemStack stack = _gear.Wield.Get(_slot);
        if (stack?.Item == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!WieldSlotContextMenuBuilder.TryShow(
                    _gear,
                    stack.ItemId,
                    _slot,
                    eventData.position,
                    () => Bind(_gear, _slot, _strength, _onHover, _onExit, _onUnequip)))
            {
                Debug.LogError(
                    "[UICharacterWieldSlotView] UIContextMenuHost failed to show wield slot menu.",
                    this);
            }

            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            _onUnequip?.Invoke(_slot, false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        ItemStack target = _gear?.Wield?.Get(_slot);
        InventorySession session = PlayerInventoryRuntime.Active?.Session;
        if (WeaponAmmoDrop.TryApplyTo(target, session))
            return;

        GearInventoryDrop.TryWieldFromActiveDrag(_slot);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        ItemStack stack = _gear?.Wield?.Get(_slot);
        if (stack?.Item == null)
            return;
        _dragging = true;
        EnsureDragGhost()?.Show(
            ItemVisualPresenter.GetDisplayIcon(stack.ItemId),
            1,
            eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;

        _dragGhost?.SetScreenPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _dragGhost?.Hide();

        ItemStack stack = _gear?.Wield?.Get(_slot);
        if (stack?.Item == null || _onUnequip == null)
            return;

        if (ShouldUnequipToFloor(eventData.position))
            _onUnequip.Invoke(_slot, true);
    }

    bool ShouldUnequipToFloor(Vector2 screenPosition)
    {
        UICharacterWindow window = GetComponentInParent<UICharacterWindow>();
        if (window != null)
        {
            RectTransform windowRt = window.WindowRect;
            if (windowRt == null)
                return false;

            Canvas canvas = window.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return !RectTransformUtility.RectangleContainsScreenPoint(windowRt, screenPosition, cam);
        }

        Canvas hudCanvas = GetComponentInParent<Canvas>();
        Camera hudCam = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? hudCanvas.worldCamera
            : null;
        return !UIOverlayWindowHitTest.ContainsScreenPoint(screenPosition, hudCam);
    }

    UIItemDragGhostService EnsureDragGhost()
    {
        if (_dragGhost != null)
            return _dragGhost;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (!UIItemDragGhostService.TryGet(canvas, out _dragGhost) || _dragGhost == null)
        {
            Debug.LogError(
                "[UICharacterWieldSlotView] UIItemDragGhostService missing on UICanvas. Run Dist/MCP/Inventory/Setup Canvas Overlays In Open Scene.",
                this);
            return null;
        }

        _dragGhost.EnsureReady();
        return _dragGhost;
    }
}
