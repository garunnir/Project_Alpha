// ============================================================
// CharacterSightHost — 본체 SightDir 구독 게이트 (마우스 sample accept / Dig 잠금)
// ============================================================

using IsoTilemap;
using UnityEngine;

public enum CharacterSightDriveMode
{
    /// <summary>마우스 Aim sample 수락.</summary>
    Free = 0,
    /// <summary>Dig LMB 잠금 — 마우스 sample 무시, 블록 포즈 유지.</summary>
    StructureLock = 1,
}

/// <summary>입력은 sample만 제안. 본체가 상태(Free/StructureLock)로 구독 여부를 정한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public sealed class CharacterSightHost : MonoBehaviour
{
    [SerializeField] float _castOriginYOffset = 0.35f;

    CharacterState _state;
    CharacterActionHost _actionHost;

    /// <summary>possess 플레이어 Sight. <see cref="CharacterSessionHub.SessionSightHost"/>.</summary>
    public static CharacterSightHost Active => CharacterSessionHub.SessionSightHost;

    public CharacterSightDriveMode Mode { get; private set; } = CharacterSightDriveMode.Free;

    /// <summary>true면 입력이 마우스 SphereCast sample을 제안해도 된다.</summary>
    public bool AcceptsMouseAim => Mode == CharacterSightDriveMode.Free;

    void Awake() => ResolveRefs();

    void ResolveRefs()
    {
        // CharacterState는 루트, SightHost는 GameplayCore — 같은 GO TryGet 금지.
        if (_state == null)
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        if (_actionHost == null)
            _actionHost = CharacterBodyResolve.GetInBody<CharacterActionHost>(this);
    }

    public void SetCastOriginYOffset(float offset) =>
        _castOriginYOffset = offset;

    /// <summary>DigPipeline begin — 마우스 aim 구독 끊고 블록에 SightDir 고정.</summary>
    public void EnterStructureLock()
    {
        Mode = CharacterSightDriveMode.StructureLock;
        RefreshStructureLockPose();
    }

    /// <summary>DigPipeline clear — 마우스 aim 구독 재개.</summary>
    public void ExitStructureLock()
    {
        Mode = CharacterSightDriveMode.Free;
    }

    /// <summary>
    /// 입력 제안 수락. StructureLock이면 false(무시).
    /// </summary>
    public bool TryAcceptMouseAim(Vector3 sightDirFlat, Vector3 aimWorldPoint, float reach)
    {
        ResolveRefs();
        if (!AcceptsMouseAim || _state == null)
            return false;
        if (sightDirFlat.sqrMagnitude < 1e-4f)
            return false;

        _state.SetAimDir(sightDirFlat.normalized, aimWorldPoint, reach);
        return true;
    }

    void LateUpdate()
    {
        if (Mode != CharacterSightDriveMode.StructureLock)
            return;
        if (_state == null || !_state.IsAiming)
            return;

        RefreshStructureLockPose();
    }

    void RefreshStructureLockPose()
    {
        ResolveRefs();
        if (_state == null || _actionHost == null)
            return;

        CharacterDigPipeline dig = _actionHost.DigPipeline;
        if (dig == null || !dig.TryGetActiveTarget(out DigTileTarget target))
        {
            ExitStructureLock();
            return;
        }

        float cellSize = ResolveCellSize();
        Vector3 lookOrigin = CharacterFeetPose.GetFeetWorld(transform);
        if (!DigTileTargetAimPose.TryGetSightWorldPoint(
                in target,
                cellSize,
                lookOrigin,
                out Vector3 aimPoint))
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * _castOriginYOffset;
        Vector3 sightFlat = aimPoint - origin;
        sightFlat.y = 0f;
        if (sightFlat.sqrMagnitude < 1e-4f)
            return;

        _state.SetAimDir(sightFlat.normalized, aimPoint, sightFlat.magnitude);
    }

    static float ResolveCellSize()
    {
        MapDigColumnHost digHost = MapDigColumnHost.Runtime;
        if (digHost != null)
            return digHost.CellSize;

        MapPlantHost plantHost = MapPlantHost.Runtime;
        return plantHost != null ? plantHost.CellSize : 1f;
    }
}
