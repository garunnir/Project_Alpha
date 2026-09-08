// ============================================================
// DataDefinitionsWindow — Odin MenuTree 기반 게임 데이터 허브
// ============================================================

using System.Collections.Generic;
using IsoTilemap;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

public sealed class DataDefinitionsWindow : OdinMenuEditorWindow
{
    const string TileFolder = "Assets/Dist/SOData/Tile";
    const string TilePrefabDbPath = "Assets/Dist/SOData/Tile/Tile Prefab DB.asset";
    const string CombatCatalogFolder = "Assets/Dist/SOData/Combat/Catalog";
    const string CombatPresentationsFolder = "Assets/Dist/SOData/Combat/Presentations";
    const string CombatAttacksFolder = "Assets/Dist/SOData/Combat/Attacks";
    const string CombatFallbacksFolder = "Assets/Dist/SOData/Combat/Fallbacks";
    const string LocomotionFolder = "Assets/Dist/SOData/Locomotion";
    const string CharacterFolder = CharacterDefinitionCatalog.AssetFolder;

    const float MenuPaneWidth = 300f;

    static readonly Color CatalogTint = new Color(0.35f, 0.65f, 1f);
    static readonly Color CharactersTint = new Color(0.72f, 0.45f, 0.95f);
    static readonly Color WorldTint = new Color(0.25f, 0.82f, 0.72f);
    static readonly Color CombatTint = new Color(1f, 0.42f, 0.35f);
    static readonly Color LocomotionTint = new Color(1f, 0.78f, 0.28f);
    static readonly Color MapTint = new Color(0.4f, 0.85f, 0.4f);

    CatalogItemBrowser _itemsReference;
    CatalogItemBrowser _itemsCustom;
    CatalogRecipeBrowser _recipesReference;
    CatalogRecipeBrowser _recipesCustom;
    CatalogLocaleHub _localeHub;
    CatalogTraitBrowser _traitBrowser;

    [MenuItem("Tools/Data Definitions")]
    static void Open()
    {
        var window = GetWindow<DataDefinitionsWindow>();
        window.titleContent = new GUIContent("Data Definitions", EditorIcons.FileCabinet.Active);
        window.minSize = new Vector2(1100f, 640f);
        window.MenuWidth = MenuPaneWidth;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        MenuWidth = MenuPaneWidth;
        CatalogDataSession.Instance.Reload();
        EnsureBrowsers();
    }

    void EnsureBrowsers()
    {
        _itemsReference ??= new CatalogItemBrowser(CatalogSource.Reference);
        _itemsCustom ??= new CatalogItemBrowser(CatalogSource.Custom);
        _recipesReference ??= new CatalogRecipeBrowser(CatalogSource.Reference);
        _recipesCustom ??= new CatalogRecipeBrowser(CatalogSource.Custom);
        _localeHub ??= new CatalogLocaleHub();
        _traitBrowser ??= new CatalogTraitBrowser();
    }

    protected override OdinMenuTree BuildMenuTree()
    {
        EnsureBrowsers();
        CatalogDataSession.Instance.Reload();

        var tree = new OdinMenuTree(supportsMultiSelect: false);
        tree.Config.DrawSearchToolbar = true;
        tree.Config.AutoHandleKeyboardNavigation = true;
        tree.DefaultMenuStyle = CreateBaseStyle(new Color(0.28f, 0.42f, 0.62f));

        OdinMenuItem catalog = AddRoot(
            tree, "Catalog", EditorIcons.FileCabinet, CatalogTint,
            "BN / Custom 아이템·레시피·이름 로케일.\nSave Changes는 Catalog 선택 시에만 표시됩니다.");
        AddLeaf(tree, "Catalog/Items/Reference (BN)", _itemsReference, EditorIcons.LockLocked, CatalogTint * 0.85f);
        AddLeaf(tree, "Catalog/Items/Custom", _itemsCustom, EditorIcons.Pen, new Color(0.35f, 0.9f, 0.5f));
        AddLeaf(tree, "Catalog/Recipes/Reference (BN)", _recipesReference, EditorIcons.LockLocked, CatalogTint * 0.85f);
        AddLeaf(tree, "Catalog/Recipes/Custom", _recipesCustom, EditorIcons.Pen, new Color(0.35f, 0.9f, 0.5f));
        AddLeaf(tree, "Catalog/Item Names", _localeHub, EditorIcons.Letter, new Color(1f, 0.85f, 0.35f));
        StyleSubtree(catalog, CatalogTint);

        OdinMenuItem characters = AddRoot(
            tree, "Characters", EditorIcons.MultiUser, CharactersTint,
            "CharacterDefinition · Faction · Emote.\n저장은 Unity 에셋(Ctrl+S).");
        AddLeaf(tree, "Characters/+ Create Definition", new CharacterDefinitionCreateAction(), EditorIcons.Plus, new Color(0.45f, 1f, 0.55f));
        AddTypedAssets(tree, "Characters/Definitions", CharacterFolder, typeof(CharacterDefinition), false, EditorIcons.SingleUser);
        AddLeaf(tree, "Characters/+ Create Faction", new CharacterFactionCreateAction(), EditorIcons.Plus, new Color(0.45f, 1f, 0.55f));
        AddTypedAssets(tree, "Characters/Factions", CharacterFolder, typeof(CharacterFaction), false, EditorIcons.Flag);
        AddAssetLeaf(tree, "Characters/Faction Catalog", CharacterFactionCatalog.DefaultAssetPath, EditorIcons.Tag);
        AddAssetLeaf(tree, "Characters/Emote Catalog", CharacterEmoteCatalog.DefaultAssetPath, EditorIcons.SpeechBubbleSquare);
        AddLeaf(tree, "Characters/Trait Icons", _traitBrowser, EditorIcons.StarPointer, new Color(0.85f, 0.55f, 1f));
        StyleSubtree(characters, CharactersTint);

        OdinMenuItem world = AddRoot(
            tree, "World", EditorIcons.Globe, WorldTint,
            "Clock · Weather · Needs · Mood 설정 SO.");
        AddAssetLeaf(tree, "World/Clock", WorldClockSettings.DefaultAssetPath, EditorIcons.Clock);
        AddAssetLeaf(tree, "World/Weather", WorldWeatherSettings.DefaultAssetPath, EditorIcons.Clouds);
        AddAssetLeaf(tree, "World/Needs", PlayerNeedsSettings.DefaultAssetPath, EditorIcons.House);
        AddAssetLeaf(tree, "World/Mood", MoodSettings.DefaultAssetPath, EditorIcons.StarPointer);
        StyleSubtree(world, WorldTint);

        OdinMenuItem combat = AddWithIcon(
            tree, "Combat", new DataDefinitionsCombatHub(), EditorIcons.Crosshair);
        AddTypedAssets(
            tree,
            DataDefinitionsCombatHub.MenuPathCatalog,
            CombatCatalogFolder,
            typeof(WeaponPresentationCatalog),
            false,
            EditorIcons.SettingsCog);
        AddLeaf(
            tree,
            "Combat/+ Create Presentation",
            new WeaponPresentationCreateAction(),
            EditorIcons.Plus,
            new Color(0.45f, 1f, 0.55f));
        AddCombatPresentations(tree);
        AddLeaf(
            tree,
            "Combat/+ Create Attack",
            new WeaponAttackCreateAction(),
            EditorIcons.Plus,
            new Color(0.45f, 1f, 0.55f));
        AddTypedAssets(
            tree,
            DataDefinitionsCombatHub.MenuPathAttacks,
            CombatAttacksFolder,
            typeof(WeaponAttack),
            false,
            EditorIcons.PacmanGhost);
        AddTypedAssets(
            tree,
            DataDefinitionsCombatHub.MenuPathFallbacks,
            CombatFallbacksFolder,
            typeof(ScriptableObject),
            false,
            EditorIcons.Folder);
        StyleSubtree(combat, CombatTint);

        OdinMenuItem locomotion = AddRoot(
            tree, "Locomotion", EditorIcons.Move, LocomotionTint,
            "NPC MovementStyle 프로파일.");
        AddTypedAssets(tree, "Locomotion/Styles", LocomotionFolder, typeof(MovementStyle), false, EditorIcons.ArrowRight);
        StyleSubtree(locomotion, LocomotionTint);

        OdinMenuItem map = AddRoot(
            tree, "Map", EditorIcons.GridBlocks, MapTint,
            "TileDefinition · Prefab DB · Farming · Fishing.");
        AddLeaf(tree, "Map/+ Create Tile", new TileDefinitionCreateAction(), EditorIcons.Plus, new Color(0.45f, 1f, 0.55f));
        AddTypedAssets(tree, "Map/Tiles", TileFolder, typeof(TileDefinition), true, EditorIcons.GridLayout);
        AddAssetLeaf(tree, "Map/Tile Prefab DB", TilePrefabDbPath, EditorIcons.Table);
        AddAssetLeaf(tree, "Map/Farming/Work Clips", FarmWorkClipCatalog.DefaultAssetPath, EditorIcons.Tree);
        AddAssetLeaf(tree, "Map/Farming/Plant Overlay", PlantOverlaySpriteCatalog.DefaultAssetPath, EditorIcons.Image);
        AddAssetLeaf(tree, "Map/Fishing/Loot", FishingLootCatalog.DefaultAssetPath, EditorIcons.ShoppingBasket);
        AddAssetLeaf(tree, "Map/Fishing/Work Clips", FishWorkClipCatalog.DefaultAssetPath, EditorIcons.Timer);
        StyleSubtree(map, MapTint);

        ExpandTopLevel(tree);
        return tree;
    }

    static OdinMenuStyle CreateBaseStyle(Color selected)
    {
        return new OdinMenuStyle
        {
            Height = 26,
            IconSize = 18f,
            Offset = 16f,
            Borders = false,
            SelectedColorDarkSkin = selected,
            SelectedColorLightSkin = selected,
        };
    }

    static void StyleSubtree(OdinMenuItem root, Color tint)
    {
        if (root == null)
            return;
        OdinMenuStyle style = CreateBaseStyle(tint);
        ApplyStyleRecursive(root, style);
    }

    static void ApplyStyleRecursive(OdinMenuItem item, OdinMenuStyle style)
    {
        item.Style = style;
        if (item.ChildMenuItems == null)
            return;
        for (int i = 0; i < item.ChildMenuItems.Count; i++)
            ApplyStyleRecursive(item.ChildMenuItems[i], style);
    }

    static OdinMenuItem AddWithIcon(OdinMenuTree tree, string path, object value, EditorIcon icon)
    {
        OdinMenuItem last = null;
        foreach (OdinMenuItem created in tree.Add(path, value))
            last = created;
        if (last != null)
            last.Icon = icon.Active;
        return last;
    }

    static OdinMenuItem AddRoot(
        OdinMenuTree tree,
        string path,
        EditorIcon icon,
        Color tint,
        string helpBody)
    {
        return AddWithIcon(tree, path, new DataDefinitionsRootHelp(path, helpBody, tint), icon);
    }

    static void AddLeaf(OdinMenuTree tree, string path, object value, EditorIcon icon, Color _)
    {
        AddWithIcon(tree, path, value, icon);
    }

    static void AddAssetLeaf(OdinMenuTree tree, string menuPath, string assetPath, EditorIcon icon)
    {
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (asset != null)
            AddWithIcon(tree, menuPath, asset, icon);
        else
            AddWithIcon(
                tree,
                menuPath,
                new DataDefinitionsRootHelp(menuPath, $"Missing asset:\n{assetPath}", new Color(1f, 0.35f, 0.35f)),
                EditorIcons.X);
    }

    static void AddCombatPresentations(OdinMenuTree tree)
    {
        Texture baselineIcon = EditorIcons.Play.Active;
        Texture qualityIcon = EditorIcons.Tag.Active;
        string[] guids = AssetDatabase.FindAssets(
            "t:WeaponPresentation",
            new[] { CombatPresentationsFolder });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentation>(path);
            if (presentation == null)
                continue;

            bool quality = presentation.name.StartsWith(
                WeaponPresentationCatalog.QualityPresentationNamePrefix,
                System.StringComparison.Ordinal);
            string menuPath = quality
                ? DataDefinitionsCombatHub.MenuPathQuality + "/" + presentation.name
                : DataDefinitionsCombatHub.MenuPathBaselines + "/" + presentation.name;
            OdinMenuItem item = AddWithIcon(
                tree,
                menuPath,
                presentation,
                quality ? EditorIcons.Tag : EditorIcons.Play);
            if (item != null)
                item.Icon = quality ? qualityIcon : baselineIcon;
        }

        OdinMenuItem baselines = FindMenuItemByPath(
            tree, DataDefinitionsCombatHub.MenuPathBaselines);
        if (baselines != null)
            baselines.Icon = baselineIcon;
        OdinMenuItem qualityFolder = FindMenuItemByPath(
            tree, DataDefinitionsCombatHub.MenuPathQuality);
        if (qualityFolder != null)
            qualityFolder.Icon = qualityIcon;
    }

    /// <summary>Combat Hub 점프 등 — 메뉴 경로로 선택.</summary>
    public static void TrySelectMenuPath(string menuPath)
    {
        DataDefinitionsWindow window = GetWindow<DataDefinitionsWindow>();
        if (window == null || string.IsNullOrEmpty(menuPath))
            return;

        if (window.MenuTree == null)
            window.ForceMenuTreeRebuild();

        OdinMenuItem item = FindMenuItemByPath(window.MenuTree, menuPath);
        if (item == null)
        {
            window.ForceMenuTreeRebuild();
            item = FindMenuItemByPath(window.MenuTree, menuPath);
        }

        if (item == null)
        {
            Debug.LogWarning("[DataDefinitions] Menu path not found: " + menuPath);
            return;
        }

        window.MenuTree.Selection.Clear();
        item.Select(true);
        window.Repaint();
    }

    static void AddTypedAssets(
        OdinMenuTree tree,
        string menuPath,
        string folder,
        System.Type type,
        bool includeSubDirectories,
        EditorIcon icon)
    {
        tree.AddAllAssetsAtPath(menuPath, folder, type, includeSubDirectories);
        OdinMenuItem folderItem = FindMenuItemByPath(tree, menuPath);
        if (folderItem == null)
            return;
        Texture tex = icon.Active;
        folderItem.Icon = tex;
        ApplyIconRecursive(folderItem, tex);
    }

    static void ApplyIconRecursive(OdinMenuItem item, Texture icon)
    {
        if (item.ChildMenuItems == null)
            return;
        for (int i = 0; i < item.ChildMenuItems.Count; i++)
        {
            OdinMenuItem child = item.ChildMenuItems[i];
            if (child.Icon == null)
                child.Icon = icon;
            ApplyIconRecursive(child, icon);
        }
    }

    static OdinMenuItem FindMenuItemByPath(OdinMenuTree tree, string path)
    {
        if (tree?.MenuItems == null || string.IsNullOrEmpty(path))
            return null;

        string[] parts = path.Split('/');
        OdinMenuItem current = null;
        IList<OdinMenuItem> children = tree.MenuItems;
        for (int p = 0; p < parts.Length; p++)
        {
            current = null;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].Name == parts[p])
                {
                    current = children[i];
                    break;
                }
            }

            if (current == null)
                return null;
            children = current.ChildMenuItems;
        }

        return current;
    }

    static void ExpandTopLevel(OdinMenuTree tree)
    {
        for (int i = 0; i < tree.MenuItems.Count; i++)
            tree.MenuItems[i].Toggled = true;
    }

    protected override void OnBeginDrawEditors()
    {
        OdinMenuTreeSelection selection = MenuTree?.Selection;
        object selected = selection != null && selection.Count > 0
            ? selection.SelectedValue
            : null;

        string domain = ResolveDomainLabel(selected, selection);
        Color tint = DomainTint(domain);
        DrawOdinDomainBanner(domain, selected, tint);
        DrawDeletableSoToolbar(selected);

        bool catalogSelected =
            selected is CatalogItemBrowser
            || selected is CatalogRecipeBrowser
            || selected is CatalogLocaleHub;

        if (!catalogSelected)
            return;

        CatalogDataSession session = CatalogDataSession.Instance;
        SirenixEditorGUI.BeginHorizontalToolbar();
        GUIHelper.PushColor(CatalogTint);
        GUILayout.Label("Catalog JSON", SirenixGUIStyles.BoldTitle);
        GUIHelper.PopColor();

        GUILayout.FlexibleSpace();

        if (session.HasUnsavedChanges)
        {
            GUIHelper.PushColor(new Color(1f, 0.75f, 0.2f));
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Save Changes", EditorIcons.Download.Active)))
                session.SaveAll();
            GUIHelper.PopColor();
            GUIHelper.PushColor(new Color(1f, 0.85f, 0.3f));
            GUILayout.Label("● unsaved", SirenixGUIStyles.BoldLabel);
            GUIHelper.PopColor();
        }

        if (SirenixEditorGUI.ToolbarButton(new GUIContent("Reload", EditorIcons.Refresh.Active)))
        {
            session.Reload();
            ForceMenuTreeRebuild();
        }

        if (SirenixEditorGUI.ToolbarButton(new GUIContent("Loc Bundle", EditorIcons.Letter.Active)))
            session.PingLocalizationBundle();

        SirenixEditorGUI.EndHorizontalToolbar();
    }

    static void DrawDeletableSoToolbar(object selected)
    {
        if (selected is not UnityEngine.Object asset || !DataDefinitionsSoCatalogs.CanDelete(asset))
            return;

        SirenixEditorGUI.BeginHorizontalToolbar();
        GUILayout.FlexibleSpace();
        GUIHelper.PushColor(new Color(1f, 0.45f, 0.4f));
        if (SirenixEditorGUI.ToolbarButton(new GUIContent("Delete Asset", EditorIcons.X.Active)))
            DataDefinitionsSoCatalogs.TryDelete(asset);
        GUIHelper.PopColor();
        SirenixEditorGUI.EndHorizontalToolbar();
    }

    static void DrawOdinDomainBanner(string domain, object selected, Color tint)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 32f);
        SirenixEditorGUI.DrawSolidRect(rect, tint * 0.28f);
        SirenixEditorGUI.DrawSolidRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), tint);

        string detail = FormatSelectionDetail(selected);
        var labelStyle = new GUIStyle(SirenixGUIStyles.BoldTitle)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white },
        };
        GUI.Label(
            new Rect(rect.x + 12f, rect.y, rect.width - 24f, rect.height),
            $"{domain}   ·   {detail}",
            labelStyle);
    }

    static string FormatSelectionDetail(object selected)
    {
        if (selected is UnityEngine.Object uo && uo != null)
            return uo.name;
        if (selected is CatalogItemBrowser itemBrowser)
            return itemBrowser.Source == CatalogSource.Custom ? "Items · Custom" : "Items · BN Reference";
        if (selected is CatalogRecipeBrowser recipeBrowser)
            return recipeBrowser.Source == CatalogSource.Custom ? "Recipes · Custom" : "Recipes · BN Reference";
        if (selected is DataDefinitionsCombatHub)
            return "Pipeline hub";
        if (selected is DataDefinitionsRootHelp)
            return "하위 leaf를 선택하세요";
        if (selected is CatalogLocaleHub)
            return "Item Names · Locale";
        if (selected is CatalogTraitBrowser)
            return "Trait Icons";
        if (selected is CharacterDefinitionCreateAction)
            return "Create Definition";
        if (selected is CharacterFactionCreateAction)
            return "Create Faction";
        if (selected is TileDefinitionCreateAction)
            return "Create Tile";
        if (selected is WeaponPresentationCreateAction)
            return "Create Presentation";
        if (selected is WeaponAttackCreateAction)
            return "Create Attack";
        return selected != null ? selected.GetType().Name : "—";
    }

    static string ResolveDomainLabel(object selected, OdinMenuTreeSelection selection)
    {
        if (selection != null && selection.Count > 0)
        {
            OdinMenuItem item = selection[0];
            if (item != null)
            {
                string path = BuildMenuPath(item);
                if (!string.IsNullOrEmpty(path))
                {
                    int slash = path.IndexOf('/');
                    return slash > 0 ? path.Substring(0, slash) : path;
                }
            }
        }

        if (selected is CatalogItemBrowser || selected is CatalogRecipeBrowser || selected is CatalogLocaleHub)
            return "Catalog";
        if (selected is CharacterDefinition || selected is CharacterFaction || selected is CharacterFactionCatalog
            || selected is CharacterEmoteCatalog || selected is CharacterDefinitionCreateAction
            || selected is CharacterFactionCreateAction
            || selected is CatalogTraitBrowser || selected is TraitIconCatalog)
            return "Characters";
        if (selected is WorldClockSettings || selected is WorldWeatherSettings
            || selected is PlayerNeedsSettings || selected is MoodSettings)
            return "World";
        if (selected is WeaponPresentationCatalog || selected is WeaponPresentation
            || selected is WeaponAttack || selected is ArmAnimSlotCatalog
            || selected is WeaponCombatFallbacks || selected is WeaponImpactVfxDefaults
            || selected is CombatHitStopSettings
            || selected is WeaponPresentationCreateAction || selected is WeaponAttackCreateAction
            || selected is DataDefinitionsCombatHub)
            return "Combat";
        if (selected is MovementStyle)
            return "Locomotion";
        if (selected is TileDefinition || selected is TilePrefabDB
            || selected is FarmWorkClipCatalog || selected is PlantOverlaySpriteCatalog
            || selected is FishingLootCatalog || selected is FishWorkClipCatalog
            || selected is TileDefinitionCreateAction)
            return "Map";

        return "Data Definitions";
    }

    static string BuildMenuPath(OdinMenuItem item)
    {
        if (item == null)
            return string.Empty;

        var parts = new List<string>(8);
        OdinMenuItem walk = item;
        while (walk != null)
        {
            if (!string.IsNullOrEmpty(walk.Name))
                parts.Add(walk.Name);
            walk = walk.Parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    static Color DomainTint(string domain)
    {
        switch (domain)
        {
            case "Catalog": return CatalogTint;
            case "Characters": return CharactersTint;
            case "World": return WorldTint;
            case "Combat": return CombatTint;
            case "Locomotion": return LocomotionTint;
            case "Map": return MapTint;
            default: return new Color(0.5f, 0.5f, 0.55f);
        }
    }
}
