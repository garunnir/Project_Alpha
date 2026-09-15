// ============================================================
// BodyEffectTicker — 부상 tend·출혈→혈액·맵 drip, 감염 onset/레이스, 독소 감쇠
// ============================================================
// 밸런스 상수: BodyIllness (docs/body/TUNING.md)

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class BodyEffectTicker
{
    TimeScaleChannel _timeChannel = TimeScaleChannel.World;

    CharacterBodyRefs _refs;
    CharacterBodyHost _bodyHost;
    readonly Dictionary<string, float> _bleedAgeByPart = new();
    readonly Dictionary<string, BodyInjuryTend.Accum> _injuryHealAccum = new();
    readonly Dictionary<string, float> _bandageDirtyAccumByPart = new();
    float _dripAccum;

    public CharacterBodyRefs BodyRefs => _refs;

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        _bodyHost = refs != null ? refs.BodyHost : null;
        if (refs != null)
            _timeChannel = refs.BodyEffectTimeChannel;
    }

    /// <summary>부상 tend·출혈·감염·독소. heap 없음 (Dictionary 재사용).</summary>
    public void Tick()
    {
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null)
            return;

        float dt = TimeScaleService.Delta(_timeChannel);
        if (dt <= 0f)
            return;

        // Hot path: 트리 워크. Dictionary 재사용. 정수 HP일 때만 SetCondition/Reduce (Changed).
        // EnsureBleedFromOpenCut는 베임 잔여 부위의 Bleed만 min 유지(이미 맞으면 no-op).
        BodyInjuryTend.Tick(body, dt, _injuryHealAccum);
        body.TickEffectDurations(dt);
        TickBleedBlood(body, dt);
        TickInfectionOnset(body, dt);
        TickInfectionRace(body, dt);
        TickToxinClear(body, dt);
    }

    void TickBleedBlood(ICharacterBody body, float dt)
    {
        float openDrain = 0f;
        IReadOnlyList<BodyPartNode> roots = body.Roots;
        for (int r = 0; r < roots.Count; r++)
            SumBleedDrainOrganic(body, roots[r], dt, ref openDrain);

        if (openDrain <= 0f)
        {
            _dripAccum = 0f;
            return;
        }

        body.SetBlood01(body.Blood01 - openDrain);
        TryDripBlood(openDrain);
    }

    void SumBleedDrainOrganic(ICharacterBody body, BodyPartNode node, float dt, ref float openDrain)
    {
        if (node == null || node.Kind == BodyPartKind.Prosthetic)
            return;

        BodyInjury.EnsureBleedFromOpenCut(body, node.PartId);
        if (!body.TryGet(node.PartId, out node) || node == null)
            return;

        int woundBleed = BleedIntensityOn(node, BodyPartEffectIds.Bleed);
        int organBleed = BleedIntensityOn(node, BodyPartEffectIds.OrganBleed);
        int bleed = woundBleed + organBleed;
        if (bleed > 0)
        {
            float drain = (
                              woundBleed * BodyIllness.BleedBloodPerIntensityPerSecond
                              + organBleed * BodyIllness.OrganBleedBloodPerIntensityPerSecond)
                          * dt;
            if (HasEffect(node, BodyPartEffectIds.Bandaged))
                AbsorbIntoBandage(body, node, drain);
            else
                openDrain += drain;
        }

        IReadOnlyList<BodyPartNode> children = node.Children;
        for (int i = 0; i < children.Count; i++)
            SumBleedDrainOrganic(body, children[i], dt, ref openDrain);
    }

    void AbsorbIntoBandage(ICharacterBody body, BodyPartNode node, float absorbedBlood01)
    {
        if (absorbedBlood01 <= 0f || body == null || node == null)
            return;

        string partId = node.PartId;
        _bandageDirtyAccumByPart.TryGetValue(partId, out float accum);
        accum += absorbedBlood01;

        float perPoint = BodyIllness.BandageDirtyBloodPerPoint;
        if (perPoint <= 0f)
        {
            _bandageDirtyAccumByPart[partId] = accum;
            return;
        }

        int points = (int)(accum / perPoint);
        if (points < 1)
        {
            _bandageDirtyAccumByPart[partId] = accum;
            return;
        }

        accum -= points * perPoint;
        _bandageDirtyAccumByPart[partId] = accum;

        int current = EffectIntensity(node, BodyPartEffectIds.BandageDirty);
        int next = current + points;
        if (next > BodyIllness.BandageDirtyMax)
            next = BodyIllness.BandageDirtyMax;
        if (next > current)
            body.EnsureEffectMinIntensity(partId, BodyPartEffectIds.BandageDirty, next, -1f);
    }

    void TryDripBlood(float drain)
    {
        _dripAccum += drain;
        if (_dripAccum < MapBloodConsts.DripDrainThreshold)
            return;

        _dripAccum = 0f;
        MapBloodHost host = MapBloodHost.Runtime;
        if (host == null)
            return;

        Vector3 feet = _refs != null ? _refs.transform.position : Vector3.zero;
        feet.x += Random.Range(-MapBloodConsts.DripJitterWorld, MapBloodConsts.DripJitterWorld);
        feet.z += Random.Range(-MapBloodConsts.DripJitterWorld, MapBloodConsts.DripJitterWorld);
        host.AddStamp(
            feet,
            Random.Range(0f, 360f),
            MapBloodConsts.DripScale,
            MapBloodConsts.DripAlpha);
    }

    void TickInfectionOnset(ICharacterBody body, float dt)
    {
        IReadOnlyList<BodyPartNode> roots = body.Roots;
        for (int r = 0; r < roots.Count; r++)
            TickInfectionOnsetNode(body, roots[r], dt);
    }

    void TickInfectionOnsetNode(ICharacterBody body, BodyPartNode node, float dt)
    {
        if (node == null || node.Kind == BodyPartKind.Prosthetic)
            return;

        if (node.HasCondition)
        {
            int bleed = TotalBleedIntensityOn(node);
            bool hasInfected = HasEffect(node, BodyPartEffectIds.Infected);
            if (bleed > 0 && !hasInfected)
            {
                float mul = InfectedOnsetMul(node);
                _bleedAgeByPart.TryGetValue(node.PartId, out float age);
                age += dt * mul;
                _bleedAgeByPart[node.PartId] = age;
                if (age >= BodyIllness.InfectedOnsetSeconds)
                    body.AddEffect(
                        node.PartId,
                        new BodyPartEffect(BodyPartEffectIds.Infected, 1, -1f));
            }
            else if (bleed <= 0)
                _bleedAgeByPart.Remove(node.PartId);
        }

        IReadOnlyList<BodyPartNode> children = node.Children;
        for (int i = 0; i < children.Count; i++)
            TickInfectionOnsetNode(body, children[i], dt);
    }

    static float InfectedOnsetMul(BodyPartNode node)
    {
        if (!HasEffect(node, BodyPartEffectIds.Bandaged))
            return 1f;

        float dirty01 = BodyHealApply.BandageDirty01(node);
        float clean = BodyIllness.BandageCleanInfectedOnsetMul;
        float dirty = BodyIllness.BandageDirtyInfectedOnsetMul;
        return clean + (dirty - clean) * dirty01;
    }

    void TickInfectionRace(ICharacterBody body, float dt)
    {
        if (!HasAnyInfected(body))
        {
            if (body.InfectionProgress01 > 0f || body.InfectionImmunity01 > 0f)
            {
                body.SetInfectionProgress01(0f);
                body.SetInfectionImmunity01(0f);
            }

            return;
        }

        float progress = body.InfectionProgress01
                         + BodyIllness.InfectedProgressPerSecond * dt;
        float filtration = BodyCapacity.BloodFiltration(body);
        float mul = AntibioticImmunityMul(body);
        float immunity = body.InfectionImmunity01
                         + BodyIllness.ImmunityPerSecond * filtration * mul * dt;

        if (immunity >= 1f)
        {
            ClearAllInfected(body);
            body.SetInfectionProgress01(0f);
            body.SetInfectionImmunity01(0f);
            return;
        }

        body.SetInfectionProgress01(progress);
        body.SetInfectionImmunity01(immunity);
    }

    void TickToxinClear(ICharacterBody body, float dt)
    {
        if (body.Toxin01 <= 0f)
            return;

        float filtration = BodyCapacity.BloodFiltration(body);
        float clear = BodyIllness.ToxinClearPerSecond * filtration * dt;
        if (clear <= 0f)
            return;

        body.SetToxin01(body.Toxin01 - clear);
    }

    static int BleedIntensityOn(BodyPartNode node, string effectId)
    {
        int sum = 0;
        IReadOnlyList<BodyPartEffect> effects = node.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectId != effectId)
                continue;
            int intensity = effects[i].Intensity;
            sum += intensity < 1 ? 1 : intensity;
        }

        return sum;
    }

    static int TotalBleedIntensityOn(BodyPartNode node) =>
        BleedIntensityOn(node, BodyPartEffectIds.Bleed)
        + BleedIntensityOn(node, BodyPartEffectIds.OrganBleed);

    static int EffectIntensity(BodyPartNode node, string effectId)
    {
        IReadOnlyList<BodyPartEffect> effects = node.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectId == effectId)
                return effects[i].Intensity;
        }

        return 0;
    }

    static bool HasEffect(BodyPartNode node, string effectId)
    {
        return EffectIntensity(node, effectId) > 0;
    }

    static float AntibioticImmunityMul(ICharacterBody body)
    {
        if (body == null || !body.TryGet(BodyPartIds.Chest, out BodyPartNode chest) || chest == null)
            return 1f;

        IReadOnlyList<BodyPartEffect> effects = chest.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectId != BodyPartEffectIds.Antibiotic)
                continue;
            return BodyIllness.ImmunityGainMul(effects[i].Intensity);
        }

        return 1f;
    }

    bool HasAnyInfected(ICharacterBody body)
    {
        IReadOnlyList<BodyPartNode> roots = body.Roots;
        for (int r = 0; r < roots.Count; r++)
        {
            if (HasInfectedSubtree(roots[r]))
                return true;
        }

        return false;
    }

    static bool HasInfectedSubtree(BodyPartNode node)
    {
        if (node == null)
            return false;
        if (HasEffect(node, BodyPartEffectIds.Infected))
            return true;
        IReadOnlyList<BodyPartNode> children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (HasInfectedSubtree(children[i]))
                return true;
        }

        return false;
    }

    void ClearAllInfected(ICharacterBody body)
    {
        IReadOnlyList<BodyPartNode> roots = body.Roots;
        for (int r = 0; r < roots.Count; r++)
            ClearInfectedSubtree(body, roots[r]);
    }

    static void ClearInfectedSubtree(ICharacterBody body, BodyPartNode node)
    {
        if (node == null)
            return;

        if (HasEffect(node, BodyPartEffectIds.Infected))
        {
            _scratchForClear.Clear();
            IReadOnlyList<BodyPartEffect> effects = node.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].EffectId == BodyPartEffectIds.Infected)
                    continue;
                _scratchForClear.Add(effects[i]);
            }

            body.ClearEffectsOn(node.PartId);
            for (int i = 0; i < _scratchForClear.Count; i++)
                body.AddEffect(node.PartId, _scratchForClear[i]);
        }

        IReadOnlyList<BodyPartNode> children = node.Children;
        for (int i = 0; i < children.Count; i++)
            ClearInfectedSubtree(body, children[i]);
    }

    static readonly List<BodyPartEffect> _scratchForClear = new(16);
}
