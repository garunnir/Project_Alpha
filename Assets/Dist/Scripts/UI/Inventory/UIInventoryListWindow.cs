// ============================================================
// UIInventoryListWindow — 인벤 리스트+사이드바 단일 View
// ============================================================

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventoryWindowMode
{
    PlayerOnly,
    NearbyOnly
}

public sealed class UIInventoryListWindow : MonoBehaviour
{
    [Required, SerializeField] UIItemListView _listView;
    [Required, SerializeField] UIContainerSidebar _sidebar;
    [SerializeField] RectTransform _listArea;
    [SerializeField] RectTransform _sidebarArea;
    [SerializeField] UIWindowDragHandler _windowDragHandler;
    [SerializeField] TMP_Text _headerTitle;
    [SerializeField] TMP_Text _weightText;
    [SerializeField] TMP_Text _volumeText;

    InventorySession _session;
    InventoryContainer _selectedContainer;
    InventoryWindowMode _mode = InventoryWindowMode.NearbyOnly;
    IInventoryItemDragHost _dragHost;
    UIItemDragGhostService _dragGhost;
    bool _dragConfigured;
    Color _weightTextDefaultColor = Color.white;
    bool _weightTextColorCached;
    Action _onChromeClose;

    readonly List<InventoryContainer> _filteredSidebar = new();
    readonly List<InventoryContainer> _nearbyWorldLootScratch = new();
    readonly List<InventoryContainer> _floorLootGroupScratch = new();
    readonly FixedContainerCapacityPolicy _nestedContainerPolicy = new();

    public bool IsVisible => gameObject.activeSelf;
    public RectTransform WindowRect => transform as RectTransform;
    public InventorySession Session => _session;
    public InventoryContainer SelectedContainer => _selectedContainer;
    public InventoryWindowMode Mode => _mode;
    public UIItemListView ListView => _listView;

    public void SetHeaderTitle(string title)
    {
        if (_headerTitle != null)
            _headerTitle.text = title;
    }

    public void BindChromeClose(Action onClose) => _onChromeClose = onClose;

    public void ConfigureWindowChrome(Canvas rootCanvas, Vector2 minSize, Vector2 maxSize)
    {
        if (_windowDragHandler == null)
            Debug.LogError("[UIInventoryListWindow] Window drag handler not assigned.", this);

        _windowDragHandler?.Initialize(WindowRect, rootCanvas);

        UIWindowResizeHandles resizeHandles = GetComponent<UIWindowResizeHandles>();
        if (resizeHandles == null)
            Debug.LogError(
                "[UIInventoryListWindow] UIWindowResizeHandles missing on window root.",
                this);
        else
            resizeHandles.Initialize(WindowRect, rootCanvas, minSize, maxSize);

        if (WindowRect != null && rootCanvas != null)
            WindowRect.sizeDelta = InventoryWindowLayout.ClampSize(
                WindowRect.sizeDelta,
                rootCanvas,
                minSize.x);

        if (!TryGetComponent(out UIOverlayWindow _))
            Debug.LogError("[UIInventoryListWindow] UIOverlayWindow missing on window prefab root.", this);

        UIWindowChromeBar.BindCloseOnWindow(this, _onChromeClose);
    }

    public void ConfigureWindowDrag(Canvas rootCanvas) =>
        ConfigureWindowChrome(
            rootCanvas,
            ResolveMinSize(),
            InventoryWindowLayout.GetMaxSize(rootCanvas));

    public Vector2 ResolveMinSize()
    {
        float listLeft = _listArea != null ? Mathf.Max(0f, _listArea.offsetMin.x) : 0f;
        float listRight = ResolveListRightInset(showSidebar: true);
        float scrollbar = ResolveVerticalScrollbarWidth();
        float rowMin = InventoryListColumnLayout.MeasureMinRowWidth(
            _listView != null ? _listView.RowPrefab : null);
        return new Vector2(
            InventoryWindowLayout.ComputeMinWidth(listLeft, listRight, scrollbar, rowMin),
            InventoryWindowLayout.MinHeight);
    }

    float ResolveVerticalScrollbarWidth()
    {
        ScrollRect scroll = _listView != null
            ? _listView.GetComponent<ScrollRect>()
            : null;
        if (scroll == null && _listArea != null)
            _listArea.TryGetComponent(out scroll);

        if (scroll == null || scroll.verticalScrollbar == null)
            return 0f;

        RectTransform scrollbarRect = scroll.verticalScrollbar.transform as RectTransform;
        return scrollbarRect != null ? Mathf.Max(0f, scrollbarRect.sizeDelta.x) : 0f;
    }

    /// <summary>
    /// 드래그 호스트·뷰포트 DnD 배선만 담당. 리스트/사이드바 바인딩은 Initialize·Refresh*가 SSOT.
    /// Open 경로에서는 ConfigureWindow → Initialize 순으로 호출해 Bind가 1회로 끝나게 한다.
    /// </summary>
    public void ConfigureDragAndDrop(
        IInventoryItemDragHost dragHost,
        Canvas rootCanvas,
        UIItemDragGhostService dragGhost)
    {
        if (_listView == null)
            return;

        _dragHost = dragHost;
        _dragGhost = dragGhost;
        EnsureSidebarRaycastTarget();

        if (_dragConfigured)
            return;

        ScrollRect scroll = _listView.GetComponentInChildren<ScrollRect>();
        RectTransform viewport = scroll != null ? scroll.viewport : null;
        if (viewport == null)
        {
            Debug.LogError("[UIInventoryListWindow] ScrollRect viewport missing on inventory window prefab.", this);
            return;
        }

        if (!viewport.TryGetComponent(out UIInventoryListDropZone dropZone))
        {
            Debug.LogError("[UIInventoryListWindow] UIInventoryListDropZone missing on viewport prefab.", viewport);
        }
        else
        {
            dropZone.Bind(this);
        }

        if (!viewport.TryGetComponent(out InventoryListMarqueeSelector marquee))
        {
            Debug.LogError("[UIInventoryListWindow] InventoryListMarqueeSelector missing on viewport prefab.", viewport);
        }
        else
        {
            RectTransform selectionRect = FindMarqueeRect(viewport);
            if (selectionRect == null)
            {
                Debug.LogError("[UIInventoryListWindow] MarqueeSelection child missing under viewport prefab.", viewport);
            }
            else
            {
                marquee.Bind(_listView, rootCanvas, selectionRect);
            }
        }

        if (!viewport.TryGetComponent(out Image _))
        {
            Debug.LogError("[UIInventoryListWindow] Image missing on viewport prefab.", viewport);
        }

        _dragConfigured = true;
    }

    void EnsureSidebarRaycastTarget()
    {
        if (_sidebarArea == null)
            return;

        if (!_sidebarArea.TryGetComponent(out Image image))
        {
            Debug.LogError("[UIInventoryListWindow] Image missing on sidebar area prefab.", _sidebarArea);
            return;
        }

        if (!image.raycastTarget)
            Debug.LogError("[UIInventoryListWindow] Sidebar Image.raycastTarget must be true on prefab.", _sidebarArea);
    }

    static RectTransform FindMarqueeRect(RectTransform parent)
    {
        Transform existing = parent.Find("MarqueeSelection");
        return existing != null ? existing as RectTransform : null;
    }

    public void Initialize(
        InventorySession session,
        InventoryWindowMode mode,
        InventoryContainer initialFocus)
    {
        _session = session;
        _mode = mode;
        ApplyModeLayout();

        if (_mode == InventoryWindowMode.PlayerOnly && initialFocus != null)
            SetActiveContainer(initialFocus, refreshList: false);
        else if (_mode == InventoryWindowMode.NearbyOnly)
        {
            InventoryContainer preferred = initialFocus;
            if (preferred == null)
            {
                InventoryContainer aggregate = ResolveLootAggregateContainer();
                if (aggregate != null && HasNearbyLootSources())
                    preferred = aggregate;
            }

            SyncNearbySelectionFromCoordinator(preferred, refreshList: false);
        }
        else
            SelectContainer(initialFocus, refreshList: false);

        if (_dragHost != null)
            _listView?.Configure(_session, _dragHost, _dragGhost);

        WireLootAggregateHostForMode();
        RefreshAll();
    }

    void WireLootAggregateHostForMode()
    {
        if (_listView == null)
            return;

        _listView.SetLootAggregateHost(
            _mode == InventoryWindowMode.NearbyOnly ? ResolveLootAggregateHost() : null);
    }

    public void OnSidebarChanged()
    {
        if (_session == null)
            return;

        // Sync/layout can destroy or deactivate the drag-source slot — defer until EndDrag.
        if (InventoryDragState.IsDragging)
            return;

        EnsureSelectedContainerForSidebar();
        RefreshSidebarAndSelection();
    }

    public void ApplyActiveLootContainer(InventoryContainer container)
    {
        if (_mode != InventoryWindowMode.NearbyOnly)
            return;

        SetActiveContainer(container);
        if (!InventoryDragState.IsDragging)
            RefreshSidebarAndSelection();
    }

    public void OnStacksChanged() => SyncFromChangeSet(InventoryStacksChangeSet.Full);

    public void SyncFromChangeSet(InventoryStacksChangeSet changeSet)
    {
        if (_session == null || changeSet == null)
            return;

        if (!changeSet.FullRefresh && !changeSet.SidebarAffected && !AffectsThisWindow(changeSet))
            return;

        bool deferSidebarChrome = InventoryDragState.IsDragging;

        if (_mode == InventoryWindowMode.PlayerOnly && _selectedContainer == null)
        {
            InventoryContainer resolved = ResolvePlayerContainer();
            if (resolved != null &&
                (changeSet.FullRefresh || changeSet.Contains(resolved)))
                SetActiveContainer(resolved, refreshList: false);
        }

        if (_mode == InventoryWindowMode.NearbyOnly && ShouldSyncLootAggregate(changeSet))
        {
            SyncLootAggregateSources();
            if (_selectedContainer != null &&
                LootAggregateHost.IsAggregateContainer(_selectedContainer))
                SetActiveContainer(_selectedContainer);
        }

        bool refreshSidebar = changeSet.FullRefresh ||
                              changeSet.SidebarAffected ||
                              SidebarSourcesChanged(changeSet);

        if (!deferSidebarChrome && refreshSidebar)
        {
            if (_mode == InventoryWindowMode.PlayerOnly)
                ApplyModeLayout();

            EnsureSelectedContainerForSidebar();
            RefreshSidebarAndSelection();
        }

        bool refreshList = changeSet.FullRefresh ||
                           (_selectedContainer != null && changeSet.Contains(_selectedContainer));

        if (refreshList && _selectedContainer != null)
            SetActiveContainer(_selectedContainer);
        else if (changeSet.FullRefresh ||
                 (_selectedContainer != null && changeSet.Contains(_selectedContainer)))
            RefreshCapacityInfo();
    }

    public void SyncDeferredAfterDrag()
    {
        if (_session == null || InventoryDragState.IsDragging)
            return;

        if (_mode == InventoryWindowMode.PlayerOnly)
            ApplyModeLayout();

        EnsureSelectedContainerForSidebar();
        RefreshSidebarAndSelection();

        // MoveStacks fires during OnDrop while IsDragging — list Bind was skipped; flush here.
        if (_selectedContainer != null)
            SetActiveContainer(_selectedContainer);
    }

    bool AffectsThisWindow(InventoryStacksChangeSet changeSet)
    {
        if (changeSet.FullRefresh)
            return true;

        if (_selectedContainer != null && changeSet.Contains(_selectedContainer))
            return true;

        return SidebarSourcesChanged(changeSet);
    }

    bool SidebarSourcesChanged(InventoryStacksChangeSet changeSet)
    {
        if (changeSet.FullRefresh || changeSet.SidebarAffected)
            return true;

        if (_mode == InventoryWindowMode.PlayerOnly)
        {
            InventoryContainer body = GetPlayerBodyContainer();
            return body != null && changeSet.Contains(body);
        }

        for (int i = 0; i < changeSet.ChangedContainers.Count; i++)
        {
            InventoryContainer container = changeSet.ChangedContainers[i];
            if (container == null)
                continue;

            if (container.InstanceId == FloorLootHost.DefaultInstanceId)
                return true;

            if (container.InstanceId == LootAggregateHost.DefaultInstanceId)
                return true;

            PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
            if (runtime != null && runtime.IsWorldLootContainer(container.InstanceId))
                return true;
        }

        return false;
    }

    public void OnSessionChanged()
    {
        OnSidebarChanged();
        SyncFromChangeSet(InventoryStacksChangeSet.Full);
    }

    public bool AddContainer(InventoryContainer container) =>
        _session != null && _session.TryAddSidebarContainer(container);

    public bool RemoveContainer(string instanceId) =>
        _session != null && _session.TryRemoveSidebarContainer(instanceId);

    public void SelectContainer(InventoryContainer container) =>
        SelectContainer(container, refreshList: true);

    void SelectContainer(InventoryContainer container, bool refreshList)
    {
        if (container == null)
            return;

        if (_mode == InventoryWindowMode.NearbyOnly &&
            container.InstanceId == PlayerInventoryHost.DefaultInstanceId)
            return;

        if (_mode == InventoryWindowMode.NearbyOnly)
        {
            if (!ContainsSidebarContainer(GetSidebarContainersForMode(), container.InstanceId))
                return;

            if (!LootAggregateHost.IsAggregateContainer(container))
                PlayerInventoryRuntime.Active?.LootProximity?.RequestActiveContainer(container);

            SetActiveContainer(container, refreshList);
            return;
        }

        SetActiveContainer(container, refreshList);
    }

    public void SelectContainer(string instanceId)
    {
        if (_session == null || string.IsNullOrEmpty(instanceId))
            return;

        IReadOnlyList<InventoryContainer> sidebar = GetSidebarContainersForMode();
        for (int i = 0; i < sidebar.Count; i++)
        {
            if (sidebar[i].InstanceId == instanceId)
            {
                SelectContainer(sidebar[i]);
                return;
            }
        }
    }

    public void RefreshAll()
    {
        RefreshSidebarAndSelection();
        if (_selectedContainer != null)
            SetActiveContainer(_selectedContainer);
    }

    public void RefreshListOnly()
    {
        if (_selectedContainer == null)
            return;

        SetActiveContainer(_selectedContainer);
    }

    public void ClearSidebarDropHovers()
    {
        if (!ShouldShowSidebar())
            return;

        _sidebar.ClearDropHovers();
    }

    void SetActiveContainer(InventoryContainer container, bool refreshList = true)
    {
        _selectedContainer = container;
        ApplySidebarSelectionHighlight();
        RefreshCapacityInfo();

        if (!refreshList || InventoryDragState.IsDragging)
            return;

        _listView?.Bind(container);
    }

    void RefreshCapacityInfo()
    {
        CacheWeightTextDefaultColor();

        if (_selectedContainer == null)
        {
            if (_weightText != null)
            {
                _weightText.text = InventoryWindowLabels.EmptyWeight;
                _weightText.color = _weightTextDefaultColor;
            }
            if (_volumeText != null)
                _volumeText.text = InventoryWindowLabels.EmptyVolume;
            return;
        }

        float usedWeight = _selectedContainer.GetTotalWeight();
        float usedVolume = _selectedContainer.GetTotalVolume();
        float maxWeight = _selectedContainer.CapacityPolicy.GetMaxWeight(_selectedContainer);
        float maxVolume = _selectedContainer.CapacityPolicy.GetMaxVolume(_selectedContainer);

        if (_weightText != null)
        {
            _weightText.text = InventoryWindowLabels.FormatWeightCapacity(usedWeight, maxWeight);
            bool overweight = usedWeight > maxWeight + FixedContainerCapacityPolicy.Epsilon;
            _weightText.color = overweight
                ? InventoryCapacityVisuals.OverweightColor
                : _weightTextDefaultColor;
        }
        if (_volumeText != null)
            _volumeText.text = InventoryWindowLabels.FormatVolumeCapacity(usedVolume, maxVolume);
    }

    void CacheWeightTextDefaultColor()
    {
        if (_weightTextColorCached || _weightText == null)
            return;

        _weightTextDefaultColor = _weightText.color;
        _weightTextColorCached = true;
    }

    public void RefreshSidebarAndSelection()
    {
        if (ShouldShowSidebar())
        {
            IReadOnlyList<InventoryContainer> sidebar = GetSidebarContainersForMode();
            string selectedId = _selectedContainer != null ? _selectedContainer.InstanceId : string.Empty;
            _sidebar.Sync(sidebar, selectedId, SelectContainer, _dragHost, _dragGhost, this, _session);
        }
        else
        {
            _sidebar.ClearSlots();
        }
    }

    void ApplySidebarSelectionHighlight()
    {
        if (!ShouldShowSidebar())
            return;

        string selectedId = _selectedContainer != null ? _selectedContainer.InstanceId : string.Empty;
        _sidebar.UpdateSelection(selectedId);
    }

    void SyncNearbySelectionFromCoordinator(InventoryContainer preferred, bool refreshList)
    {
        if (preferred != null &&
            ContainsSidebarContainer(GetSidebarContainersForMode(), preferred.InstanceId))
        {
            if (!LootAggregateHost.IsAggregateContainer(preferred))
                PlayerInventoryRuntime.Active?.LootProximity?.RequestActiveContainer(preferred);

            SetActiveContainer(preferred, refreshList);
            return;
        }

        LootProximityCoordinator coordinator = PlayerInventoryRuntime.Active?.LootProximity;
        if (coordinator == null)
            return;

        SetActiveContainer(coordinator.ActiveContainer, refreshList);
    }

    void EnsureSelectedContainerForSidebar()
    {
        IReadOnlyList<InventoryContainer> sidebar = GetSidebarContainersForMode();

        if (_selectedContainer != null && ContainsSidebarContainer(sidebar, _selectedContainer.InstanceId))
            return;

        InventoryContainer fallback = null;

        if (_mode == InventoryWindowMode.PlayerOnly)
            fallback = GetPlayerBodyContainer() ?? ResolvePlayerContainer();
        else
        {
            InventoryContainer aggregate = ResolveLootAggregateContainer();
            if (aggregate != null && HasNearbyLootSources())
                fallback = aggregate;
            else
            {
                LootProximityCoordinator coordinator = PlayerInventoryRuntime.Active?.LootProximity;
                InventoryContainer active = coordinator?.ActiveContainer;

                if (active != null && ContainsSidebarContainer(sidebar, active.InstanceId))
                    fallback = active;
                else if (sidebar.Count > 0)
                    fallback = sidebar[0];
            }
        }

        if (fallback != null)
            SetActiveContainer(fallback);
        else
            _selectedContainer = null;
    }

    InventoryContainer ResolvePlayerContainer()
    {
        if (_session == null)
            return GetPlayerBodyContainer();

        IReadOnlyList<InventoryContainer> all = _session.GetSidebarContainers();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].InstanceId == PlayerInventoryHost.DefaultInstanceId)
                return all[i];
        }

        return GetPlayerBodyContainer();
    }

    void ApplyModeLayout()
    {
        bool showSidebar = ShouldShowSidebar();

        if (_sidebarArea != null)
            _sidebarArea.gameObject.SetActive(showSidebar);
        else if (_sidebar != null)
            _sidebar.gameObject.SetActive(showSidebar);

        if (_listArea != null)
        {
            // Prefer prefab sidebar geometry over hardcoded 120 — loot chrome is narrower.
            Vector2 offsetMax = _listArea.offsetMax;
            offsetMax.x = -ResolveListRightInset(showSidebar);
            _listArea.offsetMax = offsetMax;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listArea);
        }
    }

    float ResolveListRightInset(bool showSidebar)
    {
        if (_sidebarArea == null)
        {
            Debug.LogError("[UIInventoryListWindow] Sidebar area missing; cannot resolve list inset from prefab.", this);
            return 0f;
        }

        // Right-anchored sidebar: offsetMin.x = -width, offsetMax.x = -chromeMargin.
        return showSidebar
            ? -_sidebarArea.offsetMin.x
            : -_sidebarArea.offsetMax.x;
    }

    bool ShouldShowSidebar() =>
        _mode == InventoryWindowMode.NearbyOnly ||
        (_mode == InventoryWindowMode.PlayerOnly && HasPlayerBagTabs());

    bool HasPlayerBagTabs()
    {
        if (HasWornPocketTabs())
            return true;

        InventoryContainer body = GetPlayerBodyContainer();
        if (body == null)
            return false;

        for (int i = 0; i < body.Stacks.Count; i++)
        {
            ItemStack stack = body.Stacks[i];
            if (stack != null && stack.CanHaveNested)
                return true;
        }

        return false;
    }

    bool HasWornPocketTabs()
    {
        EquipmentWearState wear = PlayerGearHost.Active?.Wear;
        if (wear == null)
            return false;

        IReadOnlyList<ItemStack> worn = wear.Worn;
        for (int i = 0; i < worn.Count; i++)
        {
            if (WornPocketRules.HasStorageCapacity(worn[i]?.Item))
                return true;
        }

        return false;
    }

    InventoryContainer GetPlayerBodyContainer()
    {
        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        return runtime?.Host?.Container;
    }

    IReadOnlyList<InventoryContainer> GetSidebarContainersForMode()
    {
        _filteredSidebar.Clear();

        if (_session == null)
            return _filteredSidebar;

        if (_mode == InventoryWindowMode.PlayerOnly)
        {
            InventoryContainer body = GetPlayerBodyContainer();
            if (body == null || !HasPlayerBagTabs())
                return _filteredSidebar;

            _filteredSidebar.Add(body);
            AppendNestedContainerTabs(body);
            AppendWornPocketTabs();
            return _filteredSidebar;
        }

        IReadOnlyList<InventoryContainer> all = _session.GetSidebarContainers();
        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        InventoryContainer floorLoot = null;
        _nearbyWorldLootScratch.Clear();
        _floorLootGroupScratch.Clear();

        for (int i = 0; i < all.Count; i++)
        {
            InventoryContainer container = all[i];
            if (container == null)
                continue;

            if (container.InstanceId == PlayerInventoryHost.DefaultInstanceId)
                continue;

            if (LootAggregateHost.IsAggregateContainer(container))
                continue;

            if (runtime != null && !runtime.IsWorldLootContainer(container.InstanceId))
                continue;

            if (container.InstanceId == FloorLootHost.DefaultInstanceId)
            {
                floorLoot = container;
                continue;
            }

            _nearbyWorldLootScratch.Add(container);
        }

        InventoryContainer aggregate = ResolveLootAggregateContainer();
        if (aggregate != null && HasNearbyLootSources())
            _filteredSidebar.Add(aggregate);

        for (int i = 0; i < _nearbyWorldLootScratch.Count; i++)
            _filteredSidebar.Add(_nearbyWorldLootScratch[i]);

        if (floorLoot != null)
        {
            _floorLootGroupScratch.Add(floorLoot);
            AppendFloorNestedLootTabs(floorLoot, runtime, _floorLootGroupScratch);
            for (int i = 0; i < _floorLootGroupScratch.Count; i++)
                _filteredSidebar.Add(_floorLootGroupScratch[i]);
        }

        return _filteredSidebar;
    }

    bool HasNearbyLootSources()
    {
        if (_session == null)
            return false;

        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        IReadOnlyList<InventoryContainer> all = _session.GetSidebarContainers();
        for (int i = 0; i < all.Count; i++)
        {
            InventoryContainer container = all[i];
            if (container == null)
                continue;

            if (container.InstanceId == PlayerInventoryHost.DefaultInstanceId)
                continue;

            if (LootAggregateHost.IsAggregateContainer(container))
                continue;

            if (runtime != null && runtime.IsWorldLootContainer(container.InstanceId))
                return true;
        }

        return false;
    }

    InventoryContainer ResolveLootAggregateContainer()
    {
        NearbyContainerDetector detector = PlayerPossessSession.SessionDetector;
        return detector?.LootAggregateContainer;
    }

    LootAggregateHost ResolveLootAggregateHost()
    {
        NearbyContainerDetector detector = PlayerPossessSession.SessionDetector;
        return detector?.ActiveLootAggregateHost;
    }

    bool ShouldSyncLootAggregate(InventoryStacksChangeSet changeSet)
    {
        if (changeSet.FullRefresh || changeSet.SidebarAffected)
            return true;

        if (changeSet.ContainsInstanceId(LootAggregateHost.DefaultInstanceId))
            return true;

        return SidebarSourcesChanged(changeSet);
    }

    void SyncLootAggregateSources()
    {
        PlayerPossessSession.SessionDetector?.SyncLootAggregateSources();
    }

    void AppendNestedContainerTabs(InventoryContainer parent)
    {
        if (parent == null)
            return;

        for (int i = 0; i < parent.Stacks.Count; i++)
        {
            ItemStack stack = parent.Stacks[i];
            if (stack?.Item == null || !stack.CanHaveNested)
                continue;

            if (!stack.TryEnsureNested(_nestedContainerPolicy) || stack.Nested == null)
                continue;

            string nestedId = stack.Nested.InstanceId;
            if (string.IsNullOrEmpty(nestedId) || ContainsSidebarContainer(_filteredSidebar, nestedId))
                continue;

            _filteredSidebar.Add(stack.Nested);
        }
    }

    void AppendWornPocketTabs()
    {
        EquipmentWearState wear = PlayerGearHost.Active?.Wear;
        if (wear == null)
            return;

        WornPocketRules.EnsureWornPockets(wear, _nestedContainerPolicy);
        IReadOnlyList<ItemStack> worn = wear.Worn;
        for (int i = 0; i < worn.Count; i++)
        {
            ItemStack stack = worn[i];
            if (stack?.Nested == null || !WornPocketRules.HasStorageCapacity(stack.Item))
                continue;

            string nestedId = stack.Nested.InstanceId;
            if (string.IsNullOrEmpty(nestedId) || ContainsSidebarContainer(_filteredSidebar, nestedId))
                continue;

            _filteredSidebar.Add(stack.Nested);
        }
    }

    void AppendFloorNestedLootTabs(
        InventoryContainer floorLoot,
        PlayerInventoryRuntime runtime,
        List<InventoryContainer> destination)
    {
        if (floorLoot == null || destination == null)
            return;

        for (int i = 0; i < floorLoot.Stacks.Count; i++)
        {
            ItemStack stack = floorLoot.Stacks[i];
            if (stack?.Item == null || !stack.CanHaveNested)
                continue;

            if (!stack.TryEnsureNested(_nestedContainerPolicy) || stack.Nested == null)
                continue;

            string nestedId = stack.Nested.InstanceId;
            if (string.IsNullOrEmpty(nestedId) || ContainsSidebarContainer(destination, nestedId))
                continue;

            if (runtime != null && !runtime.IsWorldLootContainer(nestedId))
                continue;

            destination.Add(stack.Nested);
        }
    }

    static bool ContainsSidebarContainer(IReadOnlyList<InventoryContainer> sidebar, string instanceId)
    {
        for (int i = 0; i < sidebar.Count; i++)
        {
            if (sidebar[i].InstanceId == instanceId)
                return true;
        }

        return false;
    }
}
