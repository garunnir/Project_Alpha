// ============================================================
// DataDefinitionsCombatHub — Combat 파이프라인 랜딩 (Data Definitions)
// ============================================================

using System.Text;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

[HideReferenceObjectPicker]
public sealed class DataDefinitionsCombatHub
{
    public const string MenuPathCatalog = "Combat/① Catalog";
    public const string MenuPathCatalogAsset = "Combat/① Catalog/WeaponPresentationCatalog";
    public const string MenuPathBaselines = "Combat/② Presentations/Baselines";
    public const string MenuPathQuality = "Combat/② Presentations/Quality";
    public const string MenuPathAttacks = "Combat/③ Attacks";
    public const string MenuPathFallbacks = "Combat/④ Fallbacks";

    const string CatalogAssetPath = WeaponPresentationCatalog.DefaultAssetPath;

    bool AlwaysShow => true;

    [Title("Combat Pipeline", "Data Definitions · 편집 순서", TitleAlignments.Split)]
    [InfoBox(
        "Item → Catalog(베이스+Quality) → Presentation Entry → Attack(logicId) → Fallbacks(빈 클립).\n" +
        "Quality 템플릿은 보강 Leaf만. 조합별 최종 SO bake 금지.",
        SdfIconType.Diagram3Fill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-20)]
    string Flow =>
        "<color=#ffaa88><b>① Catalog</b></color> 진입점\n"
        + "        ↓ 베이스 하나 + Quality Leaf 합산\n"
        + "<color=#ffaa88><b>② Presentation</b></color>  Leaf 줄 (Baseline / Quality)\n"
        + "        ↓ Entry.Attack\n"
        + "<color=#ffaa88><b>③ Attack</b></color>  logicId → Handler\n"
        + "        ↓ 클립·VFX 비면\n"
        + "<color=#ffaa88><b>④ Fallbacks</b></color>  Pipeline / Hit VFX";

    [HorizontalGroup("Jump", Width = 0.2f)]
    [Button(SdfIconType.GearFill, "① Catalog"), GUIColor(1f, 0.55f, 0.45f)]
    void JumpCatalog() => DataDefinitionsWindow.TrySelectMenuPath(MenuPathCatalogAsset);

    [HorizontalGroup("Jump")]
    [Button(SdfIconType.PlayFill, "② Baseline"), GUIColor(1f, 0.65f, 0.4f)]
    void JumpBaselines() => DataDefinitionsWindow.TrySelectMenuPath(MenuPathBaselines);

    [HorizontalGroup("Jump")]
    [Button(SdfIconType.Tools, "② Quality"), GUIColor(1f, 0.7f, 0.35f)]
    void JumpQuality() => DataDefinitionsWindow.TrySelectMenuPath(MenuPathQuality);

    [HorizontalGroup("Jump")]
    [Button(SdfIconType.LightningFill, "③ Attacks"), GUIColor(1f, 0.55f, 0.4f)]
    void JumpAttacks() => DataDefinitionsWindow.TrySelectMenuPath(MenuPathAttacks);

    [HorizontalGroup("Jump")]
    [Button(SdfIconType.FolderFill, "④ Fallbacks"), GUIColor(0.85f, 0.5f, 0.4f)]
    void JumpFallbacks() => DataDefinitionsWindow.TrySelectMenuPath(MenuPathFallbacks);

    [Title("Resolve 미리보기", "아이템 id → 베이스 · Quality · 최종 Leaf", TitleAlignments.Split)]
    [LabelText("Item id")]
    [PropertyOrder(10)]
    public string PreviewItemId;

    [ShowInInspector, MultiLineProperty(8), HideLabel, ReadOnly]
    [PropertyOrder(11)]
    string PreviewReport => WeaponPresentationResolvePreview.BuildReport(PreviewItemId);

    [PropertyOrder(12)]
    [Button(SdfIconType.BoxArrowUpRight, "Catalog 에셋 선택")]
    void PingCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            Debug.LogWarning("[CombatHub] Catalog missing: " + CatalogAssetPath);
            return;
        }

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
        DataDefinitionsWindow.TrySelectMenuPath(MenuPathCatalog);
    }
}

/// <summary>Catalog Resolve를 에디터에서 읽어 보여 주는 보고서.</summary>
public static class WeaponPresentationResolvePreview
{
    public static string BuildReport(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return "Item id를 입력하세요 (예: shovel_plastic, knife_combat).";

        itemId = itemId.Trim();
        var catalog = AssetDatabase.LoadAssetAtPath<WeaponPresentationCatalog>(
            WeaponPresentationCatalog.DefaultAssetPath);
        if (catalog == null)
            return "Catalog 없음: " + WeaponPresentationCatalog.DefaultAssetPath;

        CatalogDataSession.Instance.Reload();
        ItemData item = FindItem(itemId);
        if (item == null)
            return "ItemData 없음 (BN+Custom): " + itemId;

        WeaponPresentation baseline = catalog.ResolveBaseline(itemId, item);
        WeaponPresentation resolved = catalog.Resolve(itemId, item);

        var sb = new StringBuilder(256);
        sb.Append("item: ").Append(itemId).Append('\n');
        AppendGunSkill(sb, item);
        AppendQualities(sb, item);
        sb.Append("baseline: ").Append(FormatPresentation(baseline)).Append('\n');
        AppendOverlays(sb, catalog, item);
        sb.Append("merged:  ").Append(FormatPresentation(resolved)).Append('\n');
        sb.Append("leaves:  ").Append(FormatLeaves(resolved));
        return sb.ToString();
    }

    static ItemData FindItem(string itemId)
    {
        CatalogDataSession session = CatalogDataSession.Instance;
        ItemData custom = session.CustomDb != null ? session.CustomDb.GetItem(itemId) : null;
        if (custom != null)
            return custom;
        return session.BnDb != null ? session.BnDb.GetItem(itemId) : null;
    }

    static void AppendGunSkill(StringBuilder sb, ItemData item)
    {
        string skill = item?.gun != null ? item.gun.skill : null;
        if (!string.IsNullOrEmpty(skill))
            sb.Append("gun.skill: ").Append(skill).Append('\n');
        if (item?.weapon_category != null && item.weapon_category.Count > 0)
        {
            sb.Append("weapon_category: ");
            for (int i = 0; i < item.weapon_category.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(item.weapon_category[i]);
            }

            sb.Append('\n');
        }
    }

    static void AppendQualities(StringBuilder sb, ItemData item)
    {
        if (item?.qualities == null || item.qualities.Count == 0)
        {
            sb.Append("qualities: (none)\n");
            return;
        }

        sb.Append("qualities: ");
        for (int i = 0; i < item.qualities.Count; i++)
        {
            QualityEntry q = item.qualities[i];
            if (q == null)
                continue;
            if (i > 0)
                sb.Append(", ");
            sb.Append(q.id).Append('=').Append(q.level);
        }

        sb.Append('\n');
    }

    static void AppendOverlays(
        StringBuilder sb,
        WeaponPresentationCatalog catalog,
        ItemData item)
    {
        if (item?.qualities == null || catalog.ByQualityId == null)
        {
            sb.Append("overlays: (none)\n");
            return;
        }

        bool any = false;
        for (int i = 0; i < item.qualities.Count; i++)
        {
            QualityEntry q = item.qualities[i];
            if (q == null || string.IsNullOrEmpty(q.id) || q.level < 1)
                continue;
            WeaponPresentation overlay = FindQualityPresentation(catalog, q.id);
            if (overlay == null)
                continue;
            if (!any)
            {
                sb.Append("overlays:\n");
                any = true;
            }

            sb.Append("  + ").Append(q.id).Append(" → ")
                .Append(FormatPresentation(overlay))
                .Append(" [").Append(FormatLeaves(overlay)).Append("]\n");
        }

        if (!any)
            sb.Append("overlays: (none matched)\n");
    }

    static WeaponPresentation FindQualityPresentation(
        WeaponPresentationCatalog catalog,
        string qualityId)
    {
        WeaponPresentationCatalog.Binding[] bindings = catalog.ByQualityId;
        if (bindings == null)
            return null;
        for (int i = 0; i < bindings.Length; i++)
        {
            WeaponPresentationCatalog.Binding b = bindings[i];
            if (b == null || b.presentation == null)
                continue;
            if (string.Equals(b.id, qualityId, System.StringComparison.OrdinalIgnoreCase))
                return b.presentation;
        }

        return null;
    }

    static string FormatPresentation(WeaponPresentation presentation)
    {
        if (presentation == null)
            return "(null)";
        return presentation.name;
    }

    static string FormatLeaves(WeaponPresentation presentation)
    {
        if (presentation?.Entries == null || presentation.Entries.Length == 0)
            return "(empty)";

        var sb = new StringBuilder();
        for (int i = 0; i < presentation.Entries.Length; i++)
        {
            WeaponPresentation.Entry e = presentation.Entries[i];
            if (e == null)
                continue;
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(CombatLeafUtil.DropdownPath(e.leaf));
        }

        return sb.Length > 0 ? sb.ToString() : "(empty)";
    }
}
