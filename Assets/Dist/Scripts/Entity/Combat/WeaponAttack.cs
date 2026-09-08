// ============================================================
// WeaponAttack — Attack 튜닝 (logicId·cue·캐리어 VFX·탄·근접 히트박스·Impact 반응)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "WeaponAttack",
    menuName = "Dist/Combat/Weapon Attack")]
public sealed class WeaponAttack : ScriptableObject
{
    public const float DefaultCueNormalizedTime = 0.35f;
    public const float DefaultHitboxHalfWidth = 0.35f;
    public const float DefaultHitboxHalfHeight = 0.45f;

    const string MeleeHandlerPath =
        "Assets/Dist/Scripts/Entity/Combat/MeleeHitHandler.cs";
    const string ProjectileHandlerPath =
        "Assets/Dist/Scripts/Entity/Combat/SpawnProjectileHandler.cs";
    const string GuardHandlerPath =
        "Assets/Dist/Scripts/Entity/Combat/RaiseGuardHandler.cs";
    const string MeleeBlockHandlerPath =
        "Assets/Dist/Scripts/Entity/Combat/MeleeBlockTargetHandler.cs";
    const string MeleePlantHandlerPath =
        "Assets/Dist/Scripts/Entity/Combat/MeleePlantTargetHandler.cs";

    [Serializable]
    public sealed class EffectSeed
    {
        public string effectId = BodyPartEffectIds.Bleed;
        public int intensity = 1;
        public float remainingSeconds = 8f;
    }

    [InfoBox(
        "Logic Id = 실행할 전투 핸들러를 고르는 스위치입니다.\n" +
        "비우면(동작 기본) 런타임 동작에 따름: Trigger→사격, Raise→가드, Dig→굴착, 그 외→근접.",
        InfoMessageType.None)]
    [ValueDropdown(nameof(LogicIdChoices))]
    [Tooltip("전투 로직 핸들러. 비우면 CombatLeaf 기본 매핑.")]
    [LabelText("Logic Id")]
    [SerializeField] string _logicId = ActionHandlerIds.MeleeHit;

    [ShowInInspector]
    [ReadOnly]
    [LabelText("실행 핸들러")]
    string HandlerSummary =>
        string.Equals(_logicId, ActionHandlerIds.SpawnProjectile, StringComparison.Ordinal)
            ? UsesFlightProjectile(this)
                ? "SpawnProjectileHandler (비행)"
                : "SpawnProjectileHandler (히트스캔)"
            : DescribeHandler(_logicId);

    [SerializeField] EffectSeed[] _effectSeeds = Array.Empty<EffectSeed>();
    [SerializeField] CombatLeafVfx _attackVfx = new CombatLeafVfx();
    [SerializeField, Range(0f, 1f)] float _cueNormalizedTime = DefaultCueNormalizedTime;
    [Tooltip("켜면 발사 큐에서 약실이 비었을 때 메거진 1발을 올린 뒤 소모. 끄면 펌프/수동(빈 약실=NoAmmo).")]
    [SerializeField] bool _feedsChamberOnFire = true;

    [ShowIf(nameof(ShowProjectilePrefab))]
    [Title(
        "비행 탄환",
        "할당하면 cue에서 DistProjectile 스폰(비관통). 비우면 히트스캔. tracerVfx는 히트스캔 연출.",
        horizontalLine: false)]
    [Tooltip("비우면 히트스캔. 있으면 비행 엔티티(첫 충돌 소멸). 연출용 tracerVfx와 별개.")]
    [LabelText("Projectile")]
    [SerializeField] DistProjectile _projectilePrefab;

    [Title("근접 히트박스", "cue 시 Overlap. 길이 = CombatMath.RangeMeters. 원거리는 무시.", horizontalLine: false)]
    [Tooltip("조준 축 좌우 반폭(미터). 근접 cue Overlap만.")]
    [LabelText("Half Width")]
    [SerializeField, Min(0.05f)] float _hitboxHalfWidth = DefaultHitboxHalfWidth;
    [Tooltip("조준 축 상하 반높이(미터). 근접 cue Overlap만.")]
    [LabelText("Half Height")]
    [SerializeField, Min(0.05f)] float _hitboxHalfHeight = DefaultHitboxHalfHeight;

    [Title("Impact 반응", "팔 Recoil/Blocked + Reaction VFX. 애니 파이프라인과 별개로 Attack이 on/off.", horizontalLine: false)]
    [Tooltip("공격 클립 cue 도달 시 Recoil 애니·Reaction VFX. 끄면 둘 다 생략.")]
    [LabelText("Recoil")]
    [SerializeField] bool _playRecoilImpact = true;
    [Tooltip("Obstructed 시 Blocked 애니·Reaction VFX. 끄면 둘 다 생략.")]
    [LabelText("Blocked")]
    [SerializeField] bool _playBlockedImpact = true;

    /// <summary>비우면 레지스트리가 CombatLeaf 기본 핸들러를 씁니다.</summary>
    public string LogicId => _logicId ?? string.Empty;

    public EffectSeed[] EffectSeeds => _effectSeeds;

    public CombatLeafVfx AttackVfx => _attackVfx;

    public float CueNormalizedTime => Mathf.Clamp01(_cueNormalizedTime);

    /// <summary>null Attack은 spawn_projectile 기본(자동 보급)으로 본다.</summary>
    public bool FeedsChamberOnFire => _feedsChamberOnFire;

    public DistProjectile ProjectilePrefab => _projectilePrefab;

    public static bool UsesFlightProjectile(WeaponAttack attack) =>
        attack != null && attack._projectilePrefab != null;

    public float HitboxHalfWidth =>
        _hitboxHalfWidth > 0f ? _hitboxHalfWidth : DefaultHitboxHalfWidth;

    public float HitboxHalfHeight =>
        _hitboxHalfHeight > 0f ? _hitboxHalfHeight : DefaultHitboxHalfHeight;

    public static float HitboxHalfWidthOf(WeaponAttack attack) =>
        attack != null ? attack.HitboxHalfWidth : DefaultHitboxHalfWidth;

    public static float HitboxHalfHeightOf(WeaponAttack attack) =>
        attack != null ? attack.HitboxHalfHeight : DefaultHitboxHalfHeight;

    public bool PlayRecoilImpact => _playRecoilImpact;

    public bool PlayBlockedImpact => _playBlockedImpact;

    /// <summary>null Attack은 기존과 같이 반응 on (동작 유지).</summary>
    public static bool AllowsImpactReaction(WeaponAttack attack, ArmImpactKind kind)
    {
        if (attack == null)
            return true;
        return kind == ArmImpactKind.Blocked
            ? attack._playBlockedImpact
            : attack._playRecoilImpact;
    }

    static IEnumerable<ValueDropdownItem<string>> LogicIdChoices()
    {
        yield return new ValueDropdownItem<string>(
            "(동작 기본 — Trigger→사격 / Raise→가드 / Dig→굴착 / 그 외→근접)",
            string.Empty);
        yield return new ValueDropdownItem<string>(
            "근접 타격 → MeleeHitHandler (melee_hit)",
            ActionHandlerIds.MeleeHit);
        yield return new ValueDropdownItem<string>(
            "사격 → SpawnProjectileHandler (프리팹 없으면 히트스캔)",
            ActionHandlerIds.SpawnProjectile);
        yield return new ValueDropdownItem<string>(
            "가드 → RaiseGuardHandler (raise_guard)",
            ActionHandlerIds.RaiseGuard);
        yield return new ValueDropdownItem<string>(
            "블록 타겟 → MeleeBlockTargetHandler (melee_block_target)",
            ActionHandlerIds.MeleeBlockTarget);
        yield return new ValueDropdownItem<string>(
            "작물 타겟 → MeleePlantTargetHandler (melee_plant_target)",
            ActionHandlerIds.MeleePlantTarget);
    }

    static string DescribeHandler(string logicId)
    {
        if (string.IsNullOrEmpty(logicId))
            return "동작 기본 (런타임에 CombatLeaf으로 선택)";
        if (string.Equals(logicId, ActionHandlerIds.MeleeHit, StringComparison.Ordinal))
            return "MeleeHitHandler";
        if (string.Equals(logicId, ActionHandlerIds.SpawnProjectile, StringComparison.Ordinal))
            return "SpawnProjectileHandler";
        if (string.Equals(logicId, ActionHandlerIds.RaiseGuard, StringComparison.Ordinal))
            return "RaiseGuardHandler";
        if (string.Equals(logicId, ActionHandlerIds.MeleeBlockTarget, StringComparison.Ordinal)
            || string.Equals(logicId, ActionHandlerIds.MapDigBreak, StringComparison.Ordinal))
            return "MeleeBlockTargetHandler";
        if (string.Equals(logicId, ActionHandlerIds.MeleePlantTarget, StringComparison.Ordinal))
            return "MeleePlantTargetHandler";
        return "알 수 없음 — 레지스트리에 없음";
    }

    [Button("핸들러 스크립트 선택", ButtonSizes.Medium)]
    [EnableIf(nameof(CanSelectHandlerScript))]
    void SelectHandlerScript()
    {
#if UNITY_EDITOR
        string path = HandlerScriptPath(_logicId);
        if (string.IsNullOrEmpty(path))
            return;
        UnityEngine.Object script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        if (script != null)
            Selection.activeObject = script;
#endif
    }

    bool ShowProjectilePrefab =>
        _projectilePrefab != null
        || string.Equals(_logicId, ActionHandlerIds.SpawnProjectile, StringComparison.Ordinal);

    bool CanSelectHandlerScript() =>
        !string.IsNullOrEmpty(HandlerScriptPath(_logicId));

    static string HandlerScriptPath(string logicId)
    {
        if (string.Equals(logicId, ActionHandlerIds.MeleeHit, StringComparison.Ordinal))
            return MeleeHandlerPath;
        if (string.Equals(logicId, ActionHandlerIds.SpawnProjectile, StringComparison.Ordinal))
            return ProjectileHandlerPath;
        if (string.Equals(logicId, ActionHandlerIds.RaiseGuard, StringComparison.Ordinal))
            return GuardHandlerPath;
        if (string.Equals(logicId, ActionHandlerIds.MeleeBlockTarget, StringComparison.Ordinal)
            || string.Equals(logicId, ActionHandlerIds.MapDigBreak, StringComparison.Ordinal))
            return MeleeBlockHandlerPath;
        if (string.Equals(logicId, ActionHandlerIds.MeleePlantTarget, StringComparison.Ordinal))
            return MeleePlantHandlerPath;
        return null;
    }
}
