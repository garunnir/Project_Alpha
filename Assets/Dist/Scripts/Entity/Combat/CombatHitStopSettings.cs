// ============================================================
// CombatHitStopSettings — 근접 타격/피격 로컬 시간경직 지속시간 SSOT
// ============================================================

using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatHitStopSettings",
    menuName = "Dist/Combat/Hit Stop Settings")]
public sealed class CombatHitStopSettings : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Dist/SOData/Combat/Fallbacks/CombatHitStopSettings.asset";

    public const float DefaultHitSeconds = 0.05f;
    public const float DefaultBlockedSeconds = 0.08f;
    public const float DefaultSeverSeconds = 0.12f;
    public const float DefaultMaxSeconds = 0.15f;

    [SerializeField, Min(0f)]
    [Tooltip("근접 히트 경직 (Realtime 초).")]
    float _hitSeconds = DefaultHitSeconds;

    [SerializeField, Min(0f)]
    [Tooltip("근접 Obstructed(Blocked) 경직 (Realtime 초).")]
    float _blockedSeconds = DefaultBlockedSeconds;

    [SerializeField, Min(0f)]
    [Tooltip("절단 성공 경직 (Realtime 초). 히트보다 길면 이걸 씀.")]
    float _severSeconds = DefaultSeverSeconds;

    [SerializeField, Min(0f)]
    [Tooltip("연사 겹침 포함 상한 (Realtime 초).")]
    float _maxSeconds = DefaultMaxSeconds;

    public float HitSeconds => Mathf.Max(0f, _hitSeconds);
    public float BlockedSeconds => Mathf.Max(0f, _blockedSeconds);
    public float SeverSeconds => Mathf.Max(0f, _severSeconds);
    public float MaxSeconds => Mathf.Max(0f, _maxSeconds);

    public float ResolveDuration(in AttackOutcome outcome)
    {
        if (CombatLeafUtil.IsRanged(outcome.Action))
            return 0f;

        float duration;
        if (outcome.Result == AttackPerformResult.Obstructed)
            duration = BlockedSeconds;
        else if (outcome.DidHit)
            duration = outcome.DidSeverPart ? SeverSeconds : HitSeconds;
        else
            return 0f;

        float cap = MaxSeconds;
        if (cap > 0f && duration > cap)
            duration = cap;
        return duration;
    }
}
