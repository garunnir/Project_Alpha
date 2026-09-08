// ============================================================
// WeaponPresentationCatalog — 진입점 바인딩 + quality Leaf 합산
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "WeaponPresentationCatalog",
    menuName = "Dist/Combat/Weapon Presentation Catalog")]
public sealed class WeaponPresentationCatalog : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Dist/SOData/Combat/Catalog/WeaponPresentationCatalog.asset";

    const string FallbacksAssetPath = WeaponCombatFallbacks.DefaultAssetPath;

    /// <summary>Quality 템플릿 Presentation 파일명 접두 (메뉴 Quality/ 그룹).</summary>
    public const string QualityPresentationNamePrefix = "Weapon_Quality_";

    [Serializable]
    public sealed class Binding
    {
        /// <summary>Odin 리스트 줄 라벨 (id · Leaf 요약).</summary>
        public string InspectorLabel
        {
            get
            {
                string leaves = FormatLeafSummary(presentation);
                if (string.IsNullOrEmpty(id))
                    return string.IsNullOrEmpty(leaves) ? "(empty)" : leaves;
                if (string.IsNullOrEmpty(leaves))
                    return id;
                return id + " · " + leaves;
            }
        }

        [Tooltip("아이템 id, gun.skill, weapon_category, 또는 quality id (DIG/AXE…).")]
        [LabelText("Id")]
        public string id;

        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [Tooltip("이 id가 쓸 동작 목록. 여러 줄이 같은 Presentation 파일을 가리킬 수 있습니다.")]
        [LabelText("Presentation")]
        public WeaponPresentation presentation;

        [ShowInInspector, ReadOnly, HideLabel]
        [PropertyOrder(10)]
        string LeafSummary => FormatLeafSummary(presentation);

        static string FormatLeafSummary(WeaponPresentation presentation)
        {
            if (presentation?.Entries == null || presentation.Entries.Length == 0)
                return "—";

            var parts = new List<string>(4);
            for (int i = 0; i < presentation.Entries.Length; i++)
            {
                WeaponPresentation.Entry e = presentation.Entries[i];
                if (e == null)
                    continue;
                parts.Add(CombatLeafUtil.DropdownPath(e.leaf));
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "—";
        }
    }

    [InfoBox(
        "【진입점】 베이스: 아이템 → gun.skill → weapon_category → 맨손.\n" +
        "그 다음 By Quality Id로 Leaf 합산(베이스에 없는 Leaf만 추가). level은 바인딩에 안 씀.\n" +
        "Fallbacks = AnimVerb Pipeline·Hit VFX·발사체 공용.",
        InfoMessageType.None)]
    [SerializeField, HideInInspector] int _inspectorPad;

    [Title("Unarmed", "아이템·숙련·카테고리 모두 없을 때 (마지막 진입점)", horizontalLine: false)]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [Tooltip("아이템·gun.skill·카테고리 연결이 없을 때 쓰는 맨손 동작 목록입니다.")]
    [LabelText("맨손 Presentation")]
    [SerializeField] WeaponPresentation _unarmed;

    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "InspectorLabel")]
    [Tooltip("특정 아이템 id에만 쓰는 동작 목록입니다. 찾을 때 이걸 먼저 봅니다.")]
    [LabelText("By Item Id")]
    [SerializeField] Binding[] _byItemId = Array.Empty<Binding>();

    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "InspectorLabel")]
    [Tooltip("ItemData.gun.skill에 맞춰 쓰는 동작 목록입니다. 아이템 전용이 없을 때 사용합니다.")]
    [LabelText("By Skill Id")]
    [SerializeField] Binding[] _bySkillId = Array.Empty<Binding>();

    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "InspectorLabel")]
    [Tooltip("아이템의 weapon_category에 맞춰 쓰는 동작 목록입니다. 아이템·숙련이 없을 때 사용합니다.")]
    [LabelText("By Category Id")]
    [SerializeField] Binding[] _byCategoryId = Array.Empty<Binding>();

    [InfoBox(
        "qualities[].id (DIG/AXE…) → 이 표의 Presentation을 베이스에 합산.\n" +
        "베이스에 없는 Leaf만 추가(덮어쓰기 없음). level은 무시.\n" +
        "템플릿은 Excavate/Chop 등 보강 행만. 조합별 최종 SO bake 금지(런타임 캐시).",
        InfoMessageType.None)]
    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "InspectorLabel")]
    [Tooltip(
        "qualities[].id (DIG/AXE…). 베이스 Presentation에 없는 Leaf만 합산. " +
        "템플릿은 Excavate/Chop 등 보강 행만 두는 것이 안전합니다.")]
    [LabelText("By Quality Id")]
    [SerializeField] Binding[] _byQualityId = Array.Empty<Binding>();

    [FoldoutGroup("폴백 (거의 안 건드림)", Expanded = false)]
    [InfoBox(
        "AnimVerb Pipeline·Hit VFX·발사체 공용. Leaf(fire-mode) 행은 여기 없음. 평소 접어두세요.",
        InfoMessageType.None)]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [LabelText("Fallbacks")]
    [SerializeField] WeaponCombatFallbacks _fallbacks;

    [FoldoutGroup("폴백 (거의 안 건드림)")]
    [Button("Fallbacks 에셋만 선택", ButtonSizes.Medium)]
    [EnableIf(nameof(_fallbacks))]
    void SelectFallbacksAsset()
    {
#if UNITY_EDITOR
        if (_fallbacks != null)
            Selection.activeObject = _fallbacks;
#endif
    }

    readonly Dictionary<string, WeaponPresentation> _mergeCache = new();
    readonly List<WeaponPresentation> _overlayScratch = new(4);
    readonly StringBuilder _keyScratch = new(64);

    public WeaponPresentation Unarmed => _unarmed;
    public WeaponCombatFallbacks Fallbacks => _fallbacks;
    public Binding[] ByItemId => _byItemId;
    public Binding[] BySkillId => _bySkillId;
    public Binding[] ByCategoryId => _byCategoryId;
    public Binding[] ByQualityId => _byQualityId;
    public ArmAnimSlotCatalog AnimPipeline =>
        _fallbacks != null ? _fallbacks.AnimPipeline : null;
    public WeaponImpactVfxDefaults ImpactVfxDefaults =>
        _fallbacks != null ? _fallbacks.ImpactVfxDefaults : null;
    public DistProjectile DefaultProjectile =>
        _fallbacks != null ? _fallbacks.DefaultProjectile : null;

    public void SetFallbacks(WeaponCombatFallbacks fallbacks) => _fallbacks = fallbacks;

    public void SetAnimPipeline(ArmAnimSlotCatalog pipeline)
    {
        if (_fallbacks == null)
        {
#if UNITY_EDITOR
            _fallbacks = AssetDatabase.LoadAssetAtPath<WeaponCombatFallbacks>(FallbacksAssetPath);
#endif
            if (_fallbacks == null)
                return;
        }

        _fallbacks.SetAnimPipeline(pipeline);
    }

    /// <summary>
    /// 베이스 Resolve 후 quality 템플릿 Leaf 합산(캐시). 디스크 최종본 SO 없음.
    /// </summary>
    public WeaponPresentation Resolve(string itemId, ItemData item)
    {
        WeaponPresentation baseline = ResolveBaseline(itemId, item);
        CollectQualityOverlays(item, _overlayScratch);
        if (_overlayScratch.Count == 0)
            return baseline;

        string key = BuildMergeCacheKey(baseline, _overlayScratch);
        if (_mergeCache.TryGetValue(key, out WeaponPresentation cached) && cached != null)
            return cached;

        WeaponPresentation merged = WeaponPresentation.CreateRuntimeMerged(
            baseline,
            _overlayScratch);
        _mergeCache[key] = merged;
        return merged;
    }

    /// <summary>item → skill → category → Unarmed. quality 합산 없음.</summary>
    public WeaponPresentation ResolveBaseline(string itemId, ItemData item)
    {
        if (!string.IsNullOrEmpty(itemId) &&
            TryFind(_byItemId, itemId, out WeaponPresentation byItem))
            return byItem;

        string skillId = item?.gun != null ? item.gun.skill : null;
        if (!string.IsNullOrEmpty(skillId) &&
            TryFind(_bySkillId, skillId, out WeaponPresentation bySkill))
            return bySkill;

        if (item?.weapon_category != null)
        {
            for (int i = 0; i < item.weapon_category.Count; i++)
            {
                string categoryId = item.weapon_category[i];
                if (string.IsNullOrEmpty(categoryId))
                    continue;
                if (TryFind(_byCategoryId, categoryId, out WeaponPresentation byCategory))
                    return byCategory;
            }
        }

        return _unarmed;
    }

    public bool TryGetByItemId(string itemId, out WeaponPresentation presentation) =>
        TryFind(_byItemId, itemId, out presentation);

    public void EnsureItemBinding(string itemId, WeaponPresentation presentation)
    {
        if (string.IsNullOrEmpty(itemId) || presentation == null)
            return;

        if (_byItemId != null)
        {
            for (int i = 0; i < _byItemId.Length; i++)
            {
                Binding binding = _byItemId[i];
                if (binding == null ||
                    !string.Equals(binding.id, itemId, StringComparison.Ordinal))
                    continue;
                binding.presentation = presentation;
                return;
            }
        }

        int len = _byItemId?.Length ?? 0;
        var next = new Binding[len + 1];
        if (_byItemId != null && len > 0)
            Array.Copy(_byItemId, next, len);
        next[len] = new Binding { id = itemId, presentation = presentation };
        _byItemId = next;
    }

    public void UnlinkItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || _byItemId == null || _byItemId.Length == 0)
            return;

        int keep = 0;
        for (int i = 0; i < _byItemId.Length; i++)
        {
            Binding binding = _byItemId[i];
            if (binding == null ||
                string.Equals(binding.id, itemId, StringComparison.Ordinal))
                continue;
            _byItemId[keep++] = binding;
        }

        if (keep == _byItemId.Length)
            return;

        Array.Resize(ref _byItemId, keep);
    }

    void CollectQualityOverlays(ItemData item, List<WeaponPresentation> into)
    {
        into.Clear();
        if (item?.qualities == null || _byQualityId == null || _byQualityId.Length == 0)
            return;

        for (int i = 0; i < item.qualities.Count; i++)
        {
            QualityEntry quality = item.qualities[i];
            if (quality == null || string.IsNullOrEmpty(quality.id))
                continue;
            if (quality.level < 1)
                continue;
            if (!TryFindIgnoreCase(_byQualityId, quality.id, out WeaponPresentation overlay) ||
                overlay == null)
                continue;

            bool dup = false;
            for (int j = 0; j < into.Count; j++)
            {
                if (into[j] == overlay)
                {
                    dup = true;
                    break;
                }
            }

            if (!dup)
                into.Add(overlay);
        }
    }

    string BuildMergeCacheKey(WeaponPresentation baseline, List<WeaponPresentation> overlays)
    {
        _keyScratch.Clear();
        _keyScratch.Append(baseline != null ? baseline.GetInstanceID() : 0);
        for (int i = 0; i < overlays.Count; i++)
        {
            _keyScratch.Append('|');
            _keyScratch.Append(overlays[i] != null ? overlays[i].GetInstanceID() : 0);
        }

        return _keyScratch.ToString();
    }

    static bool TryFind(Binding[] bindings, string id, out WeaponPresentation presentation) =>
        TryFindCore(bindings, id, StringComparison.Ordinal, out presentation);

    static bool TryFindIgnoreCase(
        Binding[] bindings,
        string id,
        out WeaponPresentation presentation) =>
        TryFindCore(bindings, id, StringComparison.OrdinalIgnoreCase, out presentation);

    static bool TryFindCore(
        Binding[] bindings,
        string id,
        StringComparison comparison,
        out WeaponPresentation presentation)
    {
        presentation = null;
        if (bindings == null || string.IsNullOrEmpty(id))
            return false;

        for (int i = 0; i < bindings.Length; i++)
        {
            Binding binding = bindings[i];
            if (binding == null ||
                binding.presentation == null ||
                !string.Equals(binding.id, id, comparison))
                continue;
            presentation = binding.presentation;
            return true;
        }

        return false;
    }

    void OnDisable() => _mergeCache.Clear();
}
