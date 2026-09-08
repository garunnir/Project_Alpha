// ============================================================
// ArmAnimSlotCatalog — Action/Impact 연출 Pipeline (클립+VFX 한 행)
// ============================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "ArmAnimSlotCatalog",
    menuName = "Dist/Combat/Arm Anim Pipeline")]
public sealed class ArmAnimSlotCatalog : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Dist/SOData/Combat/Fallbacks/ArmAnimSlotCatalog.asset";

    const string TabVerbs = "동사 기본";
    const string TabImpact = "Impact 반응";
    const string TabThin = "thin SM";

    [Serializable]
    [InlineProperty]
    [DrawWithUnity]
    public sealed class HandClips
    {
        public AnimationClip leftBase;
        public AnimationClip rightBase;
        public AnimationClip twoHandBase;
    }

    [Serializable]
    public sealed class ActionLibraryEntry
    {
        [ReadOnly]
        [LabelText("Leaf")]
        [FormerlySerializedAs("action")]
        public CombatLeaf leaf;

        [ShowInInspector, ReadOnly, LabelText("표시")]
        string InspectorLabel => CombatLeafUtil.DropdownPath(leaf);

        [FoldoutGroup("자세 Hold Aim", expanded: true)]
        [LabelText("Hold")]
        public HandClips hold = new HandClips();

        [FoldoutGroup("자세 Hold Aim")]
        [LabelText("Aim")]
        public HandClips aim = new HandClips();

        [FoldoutGroup("타격 Attack", expanded: true)]
        [LabelText("Attack")]
        public HandClips attack = new HandClips();

        [FoldoutGroup("VFX", expanded: true)]
        [HideLabel]
        public CombatLeafVfx vfx = new CombatLeafVfx();
    }

    [Serializable]
    public sealed class ImpactLibraryEntry
    {
        [ReadOnly]
        [LabelText("Kind")]
        public ArmImpactKind kind;

        [FoldoutGroup("클립", expanded: true)]
        [HideLabel]
        public HandClips clips = new HandClips();

        [Tooltip("Impact SM thin 키 클립 (없으면 catalog Impact thin 폴백).")]
        [FoldoutGroup("클립")]
        [LabelText("Thin")]
        public AnimationClip thin;

        [FoldoutGroup("VFX", expanded: true)]
        [HideLabel]
        public CombatLeafVfx vfx = new CombatLeafVfx();
    }

    [InfoBox(
        "기본 동사 폴백: Leaf마다 행이 있어야 한다 (Swing/Thrust/Semi/Burst/Auto/Raise).\n" +
        "무기 동작 줄 클립이 비면 여기 클립·VFX로 채운다. Recoil/Blocked도 동작 줄 비면 Impact 행.\n" +
        "표시는 Melee/Trigger/Etc 묶음. 컨트롤러에는 동작 이름을 넣지 않는다.\n" +
        "Leaf 추가: CombatLeafUtil.All(+Mask) → Dist/MCP/Ensure Arm Anim Pipeline (행만; 클립 비어도 됨).\n" +
        "docs/equipment/GEAR.md · WEAPON_VISUAL.md · .cursor/rules/arm-anim-layers.mdc",
        InfoMessageType.None)]
    [SerializeField, HideInInspector] int _inspectorPad;

    [TabGroup(TabVerbs)]
    [ListDrawerSettings(
        DraggableItems = false,
        HideAddButton = true,
        HideRemoveButton = true,
        ListElementLabelName = "InspectorLabel",
        ShowFoldout = true)]
    [FormerlySerializedAs("_actions")]
    [SerializeField] ActionLibraryEntry[] _verbs = Array.Empty<ActionLibraryEntry>();

    [TabGroup(TabImpact)]
    [ListDrawerSettings(
        DraggableItems = false,
        HideAddButton = true,
        HideRemoveButton = true,
        ListElementLabelName = "kind",
        ShowFoldout = true)]
    [SerializeField] ImpactLibraryEntry[] _impacts = Array.Empty<ImpactLibraryEntry>();

    [TabGroup(TabThin)]
    [Title("Action thin", "SM 키 — 일상 편집 아님", horizontalLine: false)]
    [LabelText("Hold")]
    [FormerlySerializedAs("_hold")]
    [SerializeField] HandClips _holdThin = new HandClips();

    [TabGroup(TabThin)]
    [LabelText("Aim")]
    [SerializeField] HandClips _aimThin = new HandClips();

    [TabGroup(TabThin)]
    [LabelText("Attack")]
    [SerializeField] HandClips _attackThin = new HandClips();

    [TabGroup(TabThin)]
    [Title("Impact thin 폴백", "행 thin 비면 여기 (Recoil/Blocked)", horizontalLine: false)]
    [LabelText("Recoil")]
    [SerializeField] AnimationClip _impactRecoilThin;

    [TabGroup(TabThin)]
    [LabelText("Blocked")]
    [SerializeField] AnimationClip _impactBlockedThin;

    [SerializeField, HideInInspector] WeaponAnimClipSpeeds _clipSpeeds;

    /// <summary>Thin SM Hold 슬롯. 라이브러리 파지는 <see cref="ActionLibraryEntry.hold"/>.</summary>
    public HandClips HoldThin => _holdThin;
    public HandClips Hold => _holdThin;
    public HandClips AimThin => _aimThin;
    public HandClips AttackThin => _attackThin;
    public AnimationClip ImpactRecoilThin => _impactRecoilThin;
    public AnimationClip ImpactBlockedThin => _impactBlockedThin;
    public WeaponAnimClipSpeeds ClipSpeeds => _clipSpeeds;
    public ActionLibraryEntry[] Actions => _verbs;
    public ActionLibraryEntry[] Verbs => _verbs;
    public ImpactLibraryEntry[] Impacts => _impacts;

    public void SetHoldThin(HandClips holdThin) => _holdThin = holdThin ?? new HandClips();
    public void SetHold(HandClips hold) => SetHoldThin(hold);
    public void SetAimThin(HandClips aimThin) => _aimThin = aimThin ?? new HandClips();
    public void SetAttackThin(HandClips attackThin) => _attackThin = attackThin ?? new HandClips();
    public void SetClipSpeeds(WeaponAnimClipSpeeds speeds) => _clipSpeeds = speeds;

    public void SetImpactThin(AnimationClip recoil, AnimationClip blocked)
    {
        _impactRecoilThin = recoil;
        _impactBlockedThin = blocked;
    }

    public void SetActions(ActionLibraryEntry[] actions) =>
        _verbs = actions ?? Array.Empty<ActionLibraryEntry>();

    public void SetVerbs(ActionLibraryEntry[] verbs) => SetActions(verbs);

    public void SetImpacts(ImpactLibraryEntry[] impacts) =>
        _impacts = impacts ?? Array.Empty<ImpactLibraryEntry>();

    public ActionLibraryEntry FindAction(CombatLeaf leaf)
    {
        if (_verbs == null)
            return null;
        CombatLeaf want = CombatLeafUtil.Normalize(leaf);
        for (int i = 0; i < _verbs.Length; i++)
        {
            if (_verbs[i] == null)
                continue;
            if (CombatLeafUtil.Normalize(_verbs[i].leaf) == want)
                return _verbs[i];
        }

        return null;
    }

    public bool TryGetVerbVfx(CombatLeaf leaf, out CombatLeafVfx vfx)
    {
        ActionLibraryEntry entry = FindAction(leaf);
        vfx = entry != null ? entry.vfx : null;
        return vfx != null;
    }

    public ImpactLibraryEntry FindImpact(ArmImpactKind kind)
    {
        if (_impacts == null)
            return null;
        for (int i = 0; i < _impacts.Length; i++)
        {
            if (_impacts[i] != null && _impacts[i].kind == kind)
                return _impacts[i];
        }

        return null;
    }

    public bool TryGetImpactVfx(ArmImpactKind kind, out CombatLeafVfx vfx)
    {
        ImpactLibraryEntry entry = FindImpact(kind);
        vfx = entry != null ? entry.vfx : null;
        return vfx != null;
    }

    public AnimationClip ImpactThinClip(ArmImpactKind kind)
    {
        ImpactLibraryEntry entry = FindImpact(kind);
        if (entry != null && entry.thin != null)
            return entry.thin;
        if (kind == ArmImpactKind.Blocked)
            return _impactBlockedThin;
        return _impactRecoilThin;
    }
}
