// ============================================================
// CharacterHearing — Definition 청각 반경 + grid occlusion audibility
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class CharacterHearing
{
    float _hearingRadiusMeters = CharacterHearingDefaults.BaseRadius;
    IMapTopologyQuery _topologyQuery;

    public float HearingRadius => _hearingRadiusMeters;
    public float EffectiveHearingRadius => _hearingRadiusMeters;
    public float TopologyCellSize => _topologyQuery != null ? _topologyQuery.CellSize : 1f;

    public void ApplyFromDefinition(CharacterDefinition definition)
    {
        _hearingRadiusMeters = definition != null
            ? definition.Senses.hearingRadiusMeters
            : CharacterSenseBlock.Default.hearingRadiusMeters;
    }

    public void BindMapCollision(MapTopologyLineCast lineCast)
    {
        _topologyQuery = lineCast != null ? lineCast.Query : null;
    }

    public bool CanDetect(Vector3 listenerFeet, Vector3 targetFeet, CharacterMotor targetMotor) =>
        CanDetect(listenerFeet, targetFeet, targetMotor, 1f);

    public bool CanDetect(
        Vector3 listenerFeet,
        Vector3 targetFeet,
        CharacterMotor targetMotor,
        float targetNoise01) =>
        CharacterHearingEvaluator.CanDetect(
            listenerFeet,
            targetFeet,
            targetMotor,
            EffectiveHearingRadius,
            _topologyQuery,
            targetNoise01);

    public bool CanKeepTarget(Vector3 listenerFeet, Vector3 targetFeet, CharacterMotor targetMotor) =>
        CanDetect(listenerFeet, targetFeet, targetMotor);

    public bool TryEvaluateAudibility(
        Vector3 listenerFeet,
        Vector3 targetFeet,
        CharacterMotor targetMotor,
        out float audibility01) =>
        TryEvaluateAudibility(listenerFeet, targetFeet, targetMotor, 1f, out audibility01);

    public bool TryEvaluateAudibility(
        Vector3 listenerFeet,
        Vector3 targetFeet,
        CharacterMotor targetMotor,
        float targetNoise01,
        out float audibility01) =>
        CharacterHearingEvaluator.TryEvaluateAudibility(
            listenerFeet,
            targetFeet,
            targetMotor,
            EffectiveHearingRadius,
            _topologyQuery,
            targetNoise01,
            out audibility01);
}
