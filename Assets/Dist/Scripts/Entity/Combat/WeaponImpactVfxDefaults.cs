// ============================================================
// WeaponImpactVfxDefaults — Hit 특성 키(bash/cut/bullet) hit/miss/tracer
// ============================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponImpactVfxDefaults",
    menuName = "Dist/Combat/Weapon Impact Vfx Defaults")]
public sealed class WeaponImpactVfxDefaults : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Dist/SOData/Combat/Fallbacks/WeaponImpactVfxDefaults.asset";

    [Serializable]
    public sealed class Entry
    {
        [HorizontalGroup("Row", Width = 120)]
        [LabelText("Hit")]
        public string impactTag = AttackImpactTags.Fallback;

        [HorizontalGroup("Row")]
        [HideLabel]
        public CombatLeafVfx vfx = new CombatLeafVfx();
    }

    [InfoBox(
        "Hit 특성 키(bash/cut/bullet) → hit/miss/tracer. 없으면 fallback. Recoil/Blocked(Reaction)과 다름.",
        InfoMessageType.None)]
    [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = "impactTag")]
    [SerializeField] Entry[] _entries = Array.Empty<Entry>();

    [Title("Wound overlay", "일반 hitVfx 위에 추가. 자상·절단 결과로만 스폰.", horizontalLine: false)]
    [Tooltip("이번 히트가 유기 부위에 cut 부상을 남겼을 때 Impact에 추가 스폰.")]
    [SerializeField] GameObject _cutBleedVfx;

    [Tooltip("이번 히트에서 severable 부위를 제거했을 때 Impact에 추가 스폰. 자상 오버레이보다 우선.")]
    [SerializeField] GameObject _severBleedVfx;

    public Entry[] Entries => _entries;

    public GameObject CutBleedVfx => _cutBleedVfx;

    public GameObject SeverBleedVfx => _severBleedVfx;

    public bool TryGetVfx(string impactTag, out CombatLeafVfx vfx)
    {
        vfx = null;
        if (_entries == null || string.IsNullOrEmpty(impactTag))
            return false;

        for (int i = 0; i < _entries.Length; i++)
        {
            Entry entry = _entries[i];
            if (entry == null ||
                entry.vfx == null ||
                !string.Equals(entry.impactTag, impactTag, StringComparison.Ordinal))
                continue;
            vfx = entry.vfx;
            return true;
        }

        return false;
    }
}
