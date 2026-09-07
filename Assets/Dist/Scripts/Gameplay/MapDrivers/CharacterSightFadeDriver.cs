// ============================================================
// CharacterSightFadeDriver — possessed Spot 부채꼴 시야 → NPC 메시 페이드
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

/// <summary>
/// Spot은 <see cref="CharacterVision"/> 시야각·반경의 시각 동기. XZ 부채꼴 + 눈높이 3D topology LOS.
/// 서 있는 층 가시성 판정 없음. 전방은 <see cref="CharacterSightForward.ResolveXZ"/>.
/// </summary>
[DefaultExecutionOrder(51)]
[DisallowMultipleComponent]
public sealed class CharacterSightFadeDriver : MonoBehaviour, IMapSightFadeDriver
{
    [SerializeField] CharacterState _playerState;
    [SerializeField] Transform _playerBody;
    [SerializeField] TileMapManager _tileMapManager;
    [SerializeField] CharacterSightFadeSettings _settings = CharacterSightFadeSettings.DefaultUnity;

    CharacterVision _playerVision;
    bool _isActive;

    public CharacterSightFadeSettings Settings => _settings;

    public void SetPlayerState(CharacterState playerState)
    {
        _playerState = playerState;
        _playerVision = null;
        if (_playerState != null)
            _playerVision = CharacterBodyResolve.GetInBody<CharacterVision>(_playerState);
    }

    public void SetPlayerBody(Transform playerBody) => _playerBody = playerBody;

    public void Init(TileMapManager map)
    {
        _tileMapManager = map;
        _isActive = map != null;
    }

    public void Shutdown()
    {
        _isActive = false;
        RestoreAllHostsFullyVisible();
    }

    void LateUpdate()
    {
        if (!_isActive || _playerState == null || _playerBody == null)
            return;

        if (_playerVision == null && _playerState != null)
            _playerVision = CharacterBodyResolve.GetInBody<CharacterVision>(_playerState);

        float radius = _playerVision != null
            ? _playerVision.EffectiveDetectRadius
            : CharacterVisionDefaults.DetectRadius;

        float spotAngle;
        float innerSpotAngle;
        if (_playerVision != null)
        {
            spotAngle = _playerVision.EffectiveSpotAngleDegrees;
            innerSpotAngle = _playerVision.EffectiveInnerSpotAngleDegrees;
        }
        else if (!PlayerSightVisionBinder.TryGetConeAngles(out spotAngle, out innerSpotAngle))
        {
            spotAngle = CharacterVisionDefaults.SpotAngleDegrees;
            innerSpotAngle = spotAngle * CharacterVisionDefaults.InnerSpotAngleRatio;
        }

        Vector3 forward = CharacterSightForward.ResolveXZ(_playerState, _playerBody);

        Vector3 playerFeet = CharacterFeetPose.GetFeetWorld(_playerBody);
        MapTopologyLineCast lineCast = _tileMapManager != null
            ? _tileMapManager.MapCollisionServices?.LineCast
            : null;

        float dt = TimeScaleService.Delta(TimeScaleChannel.World);
        GameObject possessedGo = _playerBody.gameObject;

        for (int i = 0; i < CharacterBodyHost.ActiveCount; i++)
        {
            CharacterBodyHost bodyHost = CharacterBodyHost.GetActive(i);
            if (bodyHost == null)
                continue;

            CharacterSightFadeHost fadeHost = CharacterBodyResolve.GetInBody<CharacterSightFadeHost>(bodyHost);
            if (fadeHost == null)
                continue;

            fadeHost.ConfigureSettings(in _settings);

            if (bodyHost.gameObject == possessedGo)
            {
                fadeHost.SetPossessedSkip(true);
                fadeHost.TickDisplay(dt);
                continue;
            }

            fadeHost.SetPossessedSkip(false);

            float target;
            if (GameplayData.Traits != null && GameplayData.Traits.Has(TraitIds.Omnivision))
            {
                target = 1f;
            }
            else
            {
                Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(bodyHost.transform);
                target = CharacterSightFadeEvaluator.EvaluateTarget(
                    playerFeet,
                    targetFeet,
                    forward,
                    radius,
                    spotAngle,
                    innerSpotAngle,
                    in _settings,
                    lineCast);
            }

            fadeHost.SetTargetVisibility(target);
            fadeHost.TickDisplay(dt);
        }
    }

    void OnDisable() => RestoreAllHostsFullyVisible();

    void OnDestroy() => Shutdown();

    static void RestoreAllHostsFullyVisible()
    {
        for (int i = 0; i < CharacterBodyHost.ActiveCount; i++)
        {
            CharacterBodyHost bodyHost = CharacterBodyHost.GetActive(i);
            CharacterSightFadeHost fadeHost = CharacterBodyResolve.GetInBody<CharacterSightFadeHost>(bodyHost);
            if (fadeHost == null)
                continue;

            fadeHost.SetPossessedSkip(true);
            fadeHost.TickDisplay(0f);
            fadeHost.SetPossessedSkip(false);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!_settings.DrawEditorGizmos)
            return;

        float outer;
        float inner;
        Vector3 center;
        Vector3 forward;
        float spotAngle;
        float innerSpotAngle;

        if (!PlayerSightVisionBinder.TryGetConeAngles(out spotAngle, out innerSpotAngle))
        {
            spotAngle = CharacterVisionDefaults.SpotAngleDegrees;
            innerSpotAngle = spotAngle * CharacterVisionDefaults.InnerSpotAngleRatio;
        }

        if (Application.isPlaying && _playerBody != null)
        {
            if (_playerVision == null && _playerState != null)
                _playerState.TryGetComponent(out _playerVision);

            outer = _playerVision != null
                ? _playerVision.EffectiveDetectRadius
                : CharacterVisionDefaults.DetectRadius;
            center = CharacterFeetPose.GetFeetWorld(_playerBody);
            if (_playerVision != null)
            {
                spotAngle = _playerVision.EffectiveSpotAngleDegrees;
                innerSpotAngle = _playerVision.EffectiveInnerSpotAngleDegrees;
            }

            forward = CharacterSightForward.ResolveXZ(_playerState, _playerBody);
        }
        else
        {
            outer = CharacterVisionDefaults.DetectRadius;
            center = _playerBody != null
                ? CharacterFeetPose.GetFeetWorld(_playerBody)
                : transform.position;
            forward = CharacterSightForward.ResolveXZ(_playerState, _playerBody);
        }

        inner = Mathf.Max(0f, outer - Mathf.Max(0f, _settings.FadeWidthMeters));
        CharacterSightFadeGizmoColors.DrawVisionSectorXZ(
            center,
            forward.normalized,
            outer,
            inner,
            spotAngle,
            innerSpotAngle);
    }
#endif
}
