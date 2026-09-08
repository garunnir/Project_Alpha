// ============================================================
// WeaponPresentation — 동작 목록(가용·Attack·동작 줄 클립·연출) + Impact Override
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "WeaponPresentation",
    menuName = "Dist/Combat/Weapon Presentation")]
public sealed class WeaponPresentation : ScriptableObject
{
    [Serializable]
    public sealed class EffectSeed
    {
        public string effectId = BodyPartEffectIds.Bleed;
        public int intensity = 1;
        public float remainingSeconds = 8f;
    }

    [Serializable]
    public sealed class Entry
    {
        [LabelText("Leaf")]
        [ValueDropdown(nameof(LeafDropdown))]
        [Tooltip("선택·시전 슬롯. 동작 SSOT = Attack.logicId. Family는 UI 묶음.")]
        [FormerlySerializedAs("action")]
        public CombatLeaf leaf = CombatLeaf.Strike;

        [ShowInInspector, ReadOnly, HideLabel]
        public string InspectorLeafLabel => CombatLeafUtil.DropdownPath(leaf);

        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [Tooltip(
            "이 동작의 레시피(핸들러·cue·발사체 등). Catalog가 아니라 이 Presentation 줄에 꽂습니다.")]
        [LabelText("Attack")]
        public WeaponAttack attack;

        [LabelText("Hold(아이들)")]
        [Tooltip(
            "끄면 비조준·비Attack일 때 해당 손 팔 overlay weight 0 (몸 Locomotion Idle만). " +
            "Aim/Attack 재생 중에는 overlay를 켠다.")]
        public bool useHold = true;

        [LabelText("동작 쿨(초)")]
        [Tooltip("시전 시작부터. 같은 손 다음 시전을 막음. 0이면 cue pending만. 무기 쿨은 ItemData.")]
        [Min(0f)]
        public float actionCooldownSeconds;

        string HoldClipLabel => useHold ? "Hold" : "Idle";

        [FoldoutGroup("애니", expanded: true)]
        [HideLabel]
        [LabelText("$HoldClipLabel")]
        [Tooltip("비면 Catalog 같은 동작 Hold.")]
        public ArmAnimSlotCatalog.HandClips holdClips = new ArmAnimSlotCatalog.HandClips();

        [FoldoutGroup("애니")]
        [HideLabel]
        [LabelText("Aim")]
        [Tooltip("비면 Catalog 같은 동작 Aim.")]
        public ArmAnimSlotCatalog.HandClips aimClips = new ArmAnimSlotCatalog.HandClips();

        [FoldoutGroup("애니")]
        [HideLabel]
        [LabelText("Attack")]
        [Tooltip("비면 Catalog 같은 동작 Attack.")]
        public ArmAnimSlotCatalog.HandClips attackClips = new ArmAnimSlotCatalog.HandClips();

        [FoldoutGroup("애니")]
        [HideLabel]
        [LabelText("기습 Attack")]
        [ShowIf(nameof(ShowsSurpriseAttackClips))]
        [Tooltip("기습 시전 시 Attack thin. 비면 위 Attack → Catalog. Melee(Strike/Pierce)만.")]
        public ArmAnimSlotCatalog.HandClips surpriseAttackClips = new ArmAnimSlotCatalog.HandClips();

        [FoldoutGroup("애니")]
        [HideLabel]
        [LabelText("Recoil")]
        [Tooltip("비면 Catalog Impact Recoil.")]
        public ArmAnimSlotCatalog.HandClips recoilClips = new ArmAnimSlotCatalog.HandClips();

        [FoldoutGroup("애니")]
        [HideLabel]
        [LabelText("Blocked")]
        [Tooltip("비면 Catalog Impact Blocked.")]
        public ArmAnimSlotCatalog.HandClips blockedClips = new ArmAnimSlotCatalog.HandClips();

        public EffectSeed[] effectSeeds;
        public CombatLeafVfx vfx = new();

        public float ActionCooldownSeconds => Mathf.Max(0f, actionCooldownSeconds);

        bool ShowsSurpriseAttackClips()
        {
            CombatLeaf normalized = CombatLeafUtil.Normalize(leaf);
            return normalized == CombatLeaf.Strike || normalized == CombatLeaf.Pierce;
        }

        static IEnumerable<ValueDropdownItem<CombatLeaf>> LeafDropdown()
        {
            CombatLeaf[] all = CombatLeafUtil.All;
            for (int i = 0; i < all.Length; i++)
            {
                CombatLeaf leaf = all[i];
                yield return new ValueDropdownItem<CombatLeaf>(
                    CombatLeafUtil.DropdownPath(leaf),
                    leaf);
            }
        }
    }

    [InfoBox(
        "Leaf = 선택·시전 슬롯. 동작 SSOT = 각 줄 Attack.logicId → handler.\n" +
        "Family(Melee/Trigger/Etc)는 UI 묶음. AnimVerb(Swing/Dig/…)는 연출 슬롯만.\n" +
        "기본 동사 폴백은 ArmAnimSlotCatalog에 Leaf마다 행. Entry 애니·VFX 비면 그 행 사용.\n" +
        "Hold/Aim/Attack/Recoil/Blocked 클립은 이 줄. 비면 Catalog. 클립 옆 Speed.\n" +
        "동작 쿨은 이 줄(Leaf). 무기 쿨은 ItemData→CombatMath.",
        InfoMessageType.None)]
    [LabelText("동작 줄")]
    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "InspectorLeafLabel")]
    [SerializeField] Entry[] _entries = Array.Empty<Entry>();

    [ReadOnly]
    [LabelText("Supported (자동)")]
    [SerializeField] CombatLeafMask _supportedActions;

    [Tooltip("기본 선택 행. 범위 밖이거나 빈 행이면 첫 유효 행.")]
    [LabelText("기본 줄 인덱스")]
    [SerializeField] int _defaultEntryIndex;

    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [Tooltip(
        "클립 배속 테이블(WeaponAnimClipSpeeds). Hold/Aim/Attack/Recoil/Blocked는 동작 줄.")]
    [LabelText("Animator Override")]
    [SerializeField] AnimatorOverrideController _animatorOverride;

    [HideInInspector]
    [SerializeField] WeaponAnimClipSpeeds _animClipSpeeds;

    public CombatLeafMask SupportedActions => _supportedActions;
    public Entry[] Entries => _entries;
    public AnimatorOverrideController AnimatorOverride => _animatorOverride;
    public WeaponAnimClipSpeeds AnimClipSpeeds => _animClipSpeeds;

    public void SetAnimClipSpeeds(WeaponAnimClipSpeeds speeds) => _animClipSpeeds = speeds;

    public int DefaultEntryIndex
    {
        get
        {
            int first = FirstValidEntryIndex();
            if (first < 0)
                return 0;
            if (_defaultEntryIndex < 0 ||
                _entries == null ||
                _defaultEntryIndex >= _entries.Length ||
                _entries[_defaultEntryIndex] == null)
                return first;
            return _defaultEntryIndex;
        }
    }

    void OnValidate()
    {
        MigrateLegacyTriggerLeaves();
        RebuildSupportedActions();
        int first = FirstValidEntryIndex();
        if (first < 0)
            _defaultEntryIndex = 0;
        else if (_defaultEntryIndex < 0 ||
                 _defaultEntryIndex >= _entries.Length ||
                 _entries[_defaultEntryIndex] == null)
            _defaultEntryIndex = first;
#if UNITY_EDITOR
        TryWireClipSpeedsFromOverride();
#endif
    }

#if UNITY_EDITOR
    void TryWireClipSpeedsFromOverride()
    {
        if (_animClipSpeeds != null || _animatorOverride == null)
            return;
        string path = UnityEditor.AssetDatabase.GetAssetPath(_animatorOverride);
        if (string.IsNullOrEmpty(path))
            return;
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is WeaponAnimClipSpeeds speeds)
            {
                _animClipSpeeds = speeds;
                return;
            }
        }
    }
#endif

    void MigrateLegacyTriggerLeaves()
    {
        if (_entries == null)
            return;
        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            if (entry == null)
                continue;
            if (entry.leaf == CombatLeaf.Trigger)
                entry.leaf = CombatLeaf.Semi;
        }
    }

    int FirstValidEntryIndex()
    {
        if (_entries == null)
            return -1;
        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] != null)
                return i;
        }

        return -1;
    }

    public void RebuildSupportedActions()
    {
        CombatLeafMask mask = CombatLeafMask.None;
        if (_entries == null)
        {
            _supportedActions = mask;
            return;
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            if (entry == null)
                continue;
            mask |= CombatLeafUtil.ToMask(entry.leaf);
        }

        _supportedActions = mask;
    }

    public void SetEntries(Entry[] entries)
    {
        _entries = entries ?? System.Array.Empty<Entry>();
        RebuildSupportedActions();
    }

    /// <summary>
    /// 런타임 합산본용. 베이스의 Override·기본 줄·클립 스피드를 복사한다 (에셋 디스크에 쓰지 않음).
    /// </summary>
    public void CopyRuntimeChromeFrom(WeaponPresentation source)
    {
        if (source == null)
            return;
        _animatorOverride = source._animatorOverride;
        _animClipSpeeds = source._animClipSpeeds;
        _defaultEntryIndex = source._defaultEntryIndex;
    }

    /// <summary>
    /// 베이스 Entry + overlay에만 있는 Leaf 행을 합친 런타임 Presentation.
    /// overlay가 없거나 추가 Leaf가 없으면 <paramref name="baseline"/> 그대로.
    /// </summary>
    public static WeaponPresentation CreateRuntimeMerged(
        WeaponPresentation baseline,
        System.Collections.Generic.List<WeaponPresentation> overlays)
    {
        if (baseline == null)
            return null;
        if (overlays == null || overlays.Count == 0)
            return baseline;

        CombatLeafMask have = CombatLeafMask.None;
        Entry[] baseEntries = baseline._entries;
        int baseLen = baseEntries != null ? baseEntries.Length : 0;
        var list = new System.Collections.Generic.List<Entry>(baseLen + 4);
        for (int i = 0; i < baseLen; i++)
        {
            Entry e = baseEntries[i];
            if (e == null)
                continue;
            list.Add(e);
            have |= CombatLeafUtil.ToMask(e.leaf);
        }

        bool added = false;
        for (int o = 0; o < overlays.Count; o++)
        {
            WeaponPresentation overlay = overlays[o];
            if (overlay == null || overlay._entries == null)
                continue;
            for (int i = 0; i < overlay._entries.Length; i++)
            {
                Entry e = overlay._entries[i];
                if (e == null)
                    continue;
                CombatLeafMask bit = CombatLeafUtil.ToMask(e.leaf);
                if (bit == CombatLeafMask.None || (have & bit) != 0)
                    continue;
                list.Add(e);
                have |= bit;
                added = true;
            }
        }

        if (!added)
            return baseline;

        WeaponPresentation merged = CreateInstance<WeaponPresentation>();
        merged.hideFlags = HideFlags.HideAndDontSave;
        merged.name = baseline.name + "+quality";
        merged.CopyRuntimeChromeFrom(baseline);
        merged.SetEntries(list.ToArray());
        return merged;
    }

    public bool TryGetEntry(CombatLeaf action, out Entry entry)
    {
        entry = null;
        if (_entries == null)
            return false;

        CombatLeaf want = CombatLeafUtil.Normalize(action);
        for (int i = 0; i < _entries.Length; i++)
        {
            Entry candidate = _entries[i];
            if (candidate == null ||
                CombatLeafUtil.Normalize(candidate.leaf) != want)
                continue;
            entry = candidate;
            return true;
        }

        return false;
    }

    /// <summary>Entry 없거나 useHold면 true. 비조준·비Attack 팔 overlay 게이트.</summary>
    public bool UsesHold(CombatLeaf action)
    {
        if (!TryGetEntry(action, out Entry entry) || entry == null)
            return true;
        return entry.useHold;
    }
}
