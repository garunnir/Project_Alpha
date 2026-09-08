// ============================================================
// CharacterStructureTargetPipeline — Dig/Chop 공통 HP begin/apply/clear 골격
// ============================================================

using IsoTilemap;
using UnityEngine;

/// <summary>
/// 구조물 조준 hold cue용 remaining HP 세션. 타겟 타입·HP 키·break 싱크는 파생.
/// </summary>
public abstract class CharacterStructureTargetPipeline<TTarget>
{
    TTarget _target;
    int _remainingHp;
    int _maxHp;
    bool _active;

    public bool IsActive => _active;

    public float Progress01 =>
        !_active || _maxHp <= 0
            ? 0f
            : Mathf.Clamp01(1f - (_remainingHp / (float)_maxHp));

    protected TTarget Target => _target;

    public TileDefinition ActiveDefinition =>
        _active ? ResolveDefinition(in _target) : null;

    protected abstract TileDefinition ResolveDefinition(in TTarget target);

    protected abstract bool IsBlocked(in TTarget target);

    protected abstract bool IsSameTarget(in TTarget current, in TTarget next);

    protected abstract int ResolveMaxHp(in TTarget target);

    /// <summary>맵 SSOT에 HP를 심고 현재 remaining을 반환. 호스트 없으면 maxHp.</summary>
    protected abstract int InitRemainingHp(in TTarget target, int maxHp);

    /// <summary>
    /// 맵 호스트에 피해. true면 remaining ≤0.
    /// 호출부는 host != null일 때만 호출.
    /// </summary>
    protected abstract bool TryApplyMapDamage(
        MapDigColumnHost host,
        in TTarget target,
        int damage,
        out int remaining);

    protected abstract void BreakTarget(in TTarget target);

    protected virtual void OnTargetBegun(in TTarget target) { }

    protected virtual void OnCleared() { }

    public bool TryBeginTarget(in TTarget target)
    {
        if (IsBlocked(in target))
        {
            Clear();
            return false;
        }

        if (_active && IsSameTarget(in _target, in target))
            return true;

        _target = target;
        _maxHp = Mathf.Max(1, ResolveMaxHp(in target));
        _remainingHp = InitRemainingHp(in target, _maxHp);
        _active = true;
        OnTargetBegun(in target);
        return true;
    }

    public void Clear()
    {
        bool wasActive = _active;
        _active = false;
        _remainingHp = 0;
        _maxHp = 0;
        _target = default;
        if (wasActive)
            OnCleared();
    }

    public void ApplyDamage(int damage)
    {
        if (!_active || damage <= 0)
            return;

        if (IsBlocked(in _target))
        {
            Clear();
            return;
        }

        MapDigColumnHost host = MapDigColumnHost.Runtime;
        bool broken;
        if (host != null)
        {
            broken = TryApplyMapDamage(host, in _target, damage, out _remainingHp);
        }
        else
        {
            _remainingHp -= damage;
            broken = _remainingHp <= 0;
        }

        if (!broken)
            return;

        TTarget brokenTarget = _target;
        Clear();
        BreakTarget(in brokenTarget);
    }
}
