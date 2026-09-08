// ============================================================
// DistMcpMenus — Unity MCP / agent Editor 메뉴 경로 SSOT
// ============================================================
// Dist/MCP/* 는 MCP·에이전트 자동화용 (디자이너 일상 UX 아님).
// 신규 MenuItem 추가: editor-automation-hygiene.mdc — 사용자 승인 없으면 금지.
// CreateAssetMenu(Dist/...) 와 혼동하지 말 것.

#if UNITY_EDITOR
internal static class DistMcpMenus
{
    public const string Root = "Dist/MCP";

    public const string EnsureSampleScriptableObjects =
        Root + "/Ensure Sample ScriptableObject Assets";

    public const string MoodEnsureCatalog =
        Root + "/Mood/Ensure Thought Catalog Rows";

    public const string InventorySyncListColumnLayout =
        Root + "/Inventory/Sync List Column Layout";
    public const string InventoryPatchWindowScrollbars =
        Root + "/Inventory/Patch Window Scrollbars";
    public const string InventoryPatchWindowColumnHeader =
        Root + "/Inventory/Patch Window Column Header";
    public const string InventoryPatchWindowResizeHandlers =
        Root + "/Inventory/Patch Window Resize Handlers";
    public const string InventoryPatchRowNameStatusBar =
        Root + "/Inventory/Patch Row Name Status Bar";
    public const string InventorySetupCanvasOverlays =
        Root + "/Inventory/Setup Canvas Overlays In Open Scene";
    public const string ContextMenuPatchRowIcons =
        Root + "/ContextMenu/Patch Row Icons";

    public const string PlayerStatusEnsureMoodAssets =
        Root + "/PlayerStatus/Ensure Mood Assets";
    public const string PlayerStatusPatchWindowResizeHandlers =
        Root + "/PlayerStatus/Patch Window Resize Handlers";
    public const string PlayerStatusPatchWindowBodyDiagramChibi =
        Root + "/PlayerStatus/Patch Window Body Diagram Chibi";
    public const string PlayerStatusPatchWindowBodyChibiColliderHits =
        Root + "/PlayerStatus/Patch Window Body Chibi Collider Hits";
    public const string PlayerStatusPatchSummaryBodyHits =
        Root + "/PlayerStatus/Patch Summary Body Hits";
    public const string PlayerStatusPatchBodyBandageOverlays =
        Root + "/PlayerStatus/Patch Body Bandage Overlays";
    public const string PlayerStatusPatchCharacterTabsAndGearPanel =
        Root + "/PlayerStatus/Patch Character Tabs And Gear Panel";
    public const string PlayerStatusSetupCanvas =
        Root + "/PlayerStatus/Setup Canvas In Open Scene";
    public const string PlayerStatusMergeLocalizationKeys =
        Root + "/PlayerStatus/Merge Localization Keys Into UI_ko";

    public const string TimeEnsureWorldClockSettings =
        Root + "/Time/Ensure World Clock Settings Asset";
    public const string TimeEnsureWorldWeatherSettings =
        Root + "/Time/Ensure World Weather Settings Asset";
    public const string TimeEnsureWorldWeatherInOpenScene =
        Root + "/Time/Ensure World Weather In Open Scene";
    public const string TimePatchDisplayResizeHandles =
        Root + "/Time/Patch Display Resize Handles";
    public const string TimeSetupCanvas =
        Root + "/Time/Setup Canvas In Open Scene";

    public const string MessageLogCreatePrefabIfMissing =
        Root + "/MessageLog/Create Hud_MessageLog Prefab If Missing";
    public const string MessageLogSetupHud =
        Root + "/MessageLog/Setup Message Log HUD In Open Scene";
    public const string MessageLogMergeLocalizationKeys =
        Root + "/MessageLog/Merge Localization Keys Into UI_ko";

    public const string CombatSetupActionHud =
        Root + "/Combat/Setup Combat Action HUD In Open Scene";
    public const string CombatCreateAimPointerPrefabIfMissing =
        Root + "/Combat/Create Aim Pointer Prefab If Missing";
    public const string CombatPatchAimPointerCircle =
        Root + "/Combat/Patch Aim Pointer Circle";
    public const string CombatSetupAimPointerInOpenScene =
        Root + "/Combat/Setup Aim Pointer In Open Scene";

    public const string CharacterEnsurePlayerGearComponents =
        Root + "/Character/Ensure Player Gear Components";
    public const string CharacterPatchActionGaugeOnPlayer =
        Root + "/Character/Patch Action Gauge On Player";
    public const string CharacterEnsureHitStop =
        Root + "/Character/Ensure Combat Hit Stop";
    public const string CharacterOrganizeNpcSampleBody =
        Root + "/Character/Organize NpcSample Body Hierarchy";
    public const string CharacterEnsureNpcSampleBodyRefs =
        Root + "/Character/Ensure NpcSample Body Refs";
    public const string CharacterEnsureEmote =
        Root + "/Character/Ensure Character Emote";

    public const string LocalizationSelectOrCreateUiKo =
        Root + "/Localization/Select Or Create UI_ko Table";

    public const string CraftingSetupCanvas =
        Root + "/Crafting/Setup Canvas In Open Scene";
    public const string CraftingMergeLocalizationKeys =
        Root + "/Crafting/Merge Localization Keys Into UI_ko";
    public const string CraftingPatchDetailFooter =
        Root + "/Crafting/Patch Detail Outputs And Footer";
    public const string CraftingPatchIngredientGrid =
        Root + "/Crafting/Patch Ingredient Grid";

    public const string ConstructionCreatePrefabIfMissing =
        Root + "/Construction/Create Construction Window Prefab If Missing";
    public const string ConstructionSetupCanvas =
        Root + "/Construction/Setup Canvas In Open Scene";
    public const string ConstructionFixRuntimeEditor =
        Root + "/Construction/Fix Runtime Editor TileMap Refs In Open Scene";
    public const string ConstructionToggleRuntimeEditor =
        Root + "/Construction/Toggle Runtime Tile Editor In Play Mode";

    public const string BnEnsureHouseFurnitureTiles =
        Root + "/BN/Ensure House Furniture Tile Definitions";

    public const string WindowChromePatchFoldCloseButtons =
        Root + "/WindowChrome/Patch Fold Close Buttons";

    public const string SettingsCreatePrefabIfMissing =
        Root + "/Settings/Create Settings Window Prefab If Missing";
    public const string SettingsSetupCanvas =
        Root + "/Settings/Setup Settings In Open Scene";
    public const string SettingsMergeLocalizationKeys =
        Root + "/Settings/Merge Localization Keys Into UI_ko";
    public const string SettingsPatchHudPopupToggles =
        Root + "/Settings/Patch HUD Popup Toggles";
    public const string SettingsPatchGameSaveButtons =
        Root + "/Settings/Patch Game Save Buttons";
    public const string SettingsCreateGameSaveSlotPopupPrefabIfMissing =
        Root + "/Settings/Create Game Save Slot Popup Prefab If Missing";

    public const string HudLayoutPatchParticipants =
        Root + "/HudLayout/Patch HUD Layout Participants";

    public const string HudPatchQuickSlotWield =
        Root + "/Hud/Patch QuickSlot Wield";
    public const string HudCreateLifeThreatPrefabIfMissing =
        Root + "/Hud/Create Life Threat Prefab If Missing";
    public const string HudSetupLifeThreatOverlayInOpenScene =
        Root + "/Hud/Setup Life Threat Overlay In Open Scene";

    public const string TimeScaleCreateHudPrefabIfMissing =
        Root + "/Time/Create TimeScale HUD Prefab If Missing";
    public const string TimeScaleSetupHudInOpenScene =
        Root + "/Time/Setup TimeScale HUD In Open Scene";
}
#endif
