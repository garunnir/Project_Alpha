// ============================================================
// CharacterBreathHost — BloodOxygen01·합산 O2·DIVE_TANK (plain, SwimHost 소유)
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class CharacterBreathHost
{
    readonly CharacterBodyHost _bodyHost;
    readonly CharacterPainHost _pain;
    ItemStack _activeTank;
    float _o2SecondsRemaining = -1f;
    float _tankChargeAccum;

    public CharacterBreathHost(CharacterBodyHost bodyHost, CharacterPainHost pain)
    {
        _bodyHost = bodyHost;
        _pain = pain;
    }

    public bool IsDiveTankActive => _activeTank?.Instance != null
        && _activeTank.Instance.ToolCharges > 0
        && DiveTankService.IsDiveTankItem(_activeTank.Item);

    public event Action Changed;

    public void TickSwim(float dt, MapSwimImmersion immersion)
    {
        if (dt <= 0f)
            return;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null || body.IsDeadState)
        {
            ResetO2Runtime(body);
            return;
        }

        float maxSeconds = ComputeMaxO2Seconds(body);
        if (maxSeconds <= 0f)
            maxSeconds = MapSwimConsts.BaseBreathHoldSeconds;

        if (_o2SecondsRemaining < 0f)
            _o2SecondsRemaining = maxSeconds;
        else if (_o2SecondsRemaining > maxSeconds)
            _o2SecondsRemaining = maxSeconds;

        float lungEff = BodyCapacity.LungEff(body);
        if (immersion.HeadSubmerged)
        {
            _o2SecondsRemaining = Mathf.Max(0f, _o2SecondsRemaining - dt);
            if (IsDiveTankActive)
                TickTankCharges(dt);
        }
        else
        {
            _o2SecondsRemaining = Mathf.Min(
                maxSeconds,
                _o2SecondsRemaining + MapSwimConsts.BloodOxygenRecoverPerSecond * lungEff * dt);
        }

        body.SetBloodOxygen01(_o2SecondsRemaining / maxSeconds);
        Changed?.Invoke();
        _pain?.Refresh();
    }

    public bool TryToggleDiveTank(ItemStack stack)
    {
        if (!DiveTankService.IsDiveTankItem(stack?.Item))
            return false;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;

        if (_activeTank == stack)
        {
            _activeTank = null;
            SyncO2ToMax(body);
            Changed?.Invoke();
            return true;
        }

        if (stack.Instance == null || stack.Instance.ToolCharges <= 0)
            return false;

        float oldMax = body != null ? ComputeMaxO2Seconds(body) : 0f;
        _activeTank = stack;
        if (body != null)
        {
            float newMax = ComputeMaxO2Seconds(body);
            if (_o2SecondsRemaining < 0f)
                _o2SecondsRemaining = newMax;
            else
                _o2SecondsRemaining = Mathf.Min(newMax, _o2SecondsRemaining + Mathf.Max(0f, newMax - oldMax));
            body.SetBloodOxygen01(_o2SecondsRemaining / newMax);
        }

        Changed?.Invoke();
        return true;
    }

    public void ClearActiveDiveTank()
    {
        if (_activeTank == null)
            return;

        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        _activeTank = null;
        SyncO2ToMax(body);
        Changed?.Invoke();
    }

    public bool IsActiveTank(ItemStack stack) =>
        stack != null && _activeTank == stack;

    float ComputeMaxO2Seconds(ICharacterBody body)
    {
        if (body == null)
            return MapSwimConsts.BaseBreathHoldSeconds;

        float internalMax = MapSwimConsts.BaseBreathHoldSeconds * BodyCapacity.LungEff(body);
        float tankMax = 0f;
        if (IsDiveTankActive && _activeTank.Instance != null)
            tankMax = _activeTank.Instance.ToolCharges * MapSwimConsts.DiveTankSecondsPerCharge;

        return internalMax + tankMax;
    }

    void SyncO2ToMax(ICharacterBody body)
    {
        if (body == null)
        {
            _o2SecondsRemaining = -1f;
            return;
        }

        float maxSeconds = ComputeMaxO2Seconds(body);
        if (maxSeconds <= 0f)
            maxSeconds = MapSwimConsts.BaseBreathHoldSeconds;

        if (_o2SecondsRemaining < 0f || _o2SecondsRemaining > maxSeconds)
            _o2SecondsRemaining = maxSeconds;

        body.SetBloodOxygen01(_o2SecondsRemaining / maxSeconds);
    }

    void ResetO2Runtime(ICharacterBody body)
    {
        _o2SecondsRemaining = -1f;
        body?.SetBloodOxygen01(1f);
    }

    void TickTankCharges(float dt)
    {
        if (_activeTank?.Instance == null)
            return;

        _tankChargeAccum += dt;
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        while (_tankChargeAccum >= MapSwimConsts.DiveTankChargeIntervalSeconds)
        {
            _tankChargeAccum -= MapSwimConsts.DiveTankChargeIntervalSeconds;
            if (!_activeTank.Instance.TryConsumeToolCharges(MapSwimConsts.DiveTankChargePerInterval))
            {
                _activeTank = null;
                SyncO2ToMax(body);
                break;
            }

            if (body != null && _o2SecondsRemaining > 0f)
            {
                float maxSeconds = ComputeMaxO2Seconds(body);
                if (maxSeconds > 0f)
                    body.SetBloodOxygen01(_o2SecondsRemaining / maxSeconds);
            }
        }
    }
}
