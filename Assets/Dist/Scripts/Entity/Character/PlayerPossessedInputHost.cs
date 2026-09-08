// ============================================================
// PlayerPossessedInputHost — 디바이스 입력을 possessed 본체 공용 API에 연결
// ============================================================

using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPossessedInputHost : MonoBehaviour, IPlayControllable
{
    [SerializeField] PlayerMovement _movement;
    [SerializeField] PlayerAimController _aimController;
    [SerializeField] PlayerCombatController _combatController;
    [SerializeField] PlayerStealthController _stealthController;
    [SerializeField] TileObjectPointerController _tileObjectPointer;
    [SerializeField] PlayerFloorVisibilityDriver _floorVisibility;
    [SerializeField] SightLineProximityBlendDriver _sightLineBlend;
    [SerializeField] CharacterVisibilityBroadcaster _visibilityBroadcaster;
    [SerializeField] CharacterSightFadeDriver _sightFade;
    [SerializeField] CharacterHearingPingDriver _hearingPing;
    [SerializeField] PlayerInventoryRuntime _inventoryRuntime;

    GameObject _body;
    CharacterState _bodyState;
    Transform _bodyTransform;

    public Transform BodyTransform => _bodyTransform;
    public CharacterState BodyState => _bodyState;
    public GameObject Body => _body;

    void Awake() => EnsureRefs();

    void OnValidate() => EnsureRefs();

    void EnsureRefs()
    {
        if (_movement == null)
            TryGetComponent(out _movement);
        if (_aimController == null)
            TryGetComponent(out _aimController);
        if (_combatController == null)
            TryGetComponent(out _combatController);
        if (_stealthController == null)
            TryGetComponent(out _stealthController);
        if (_tileObjectPointer == null)
            TryGetComponent(out _tileObjectPointer);
        if (_sightFade == null)
            TryGetComponent(out _sightFade);
        if (_hearingPing == null)
            TryGetComponent(out _hearingPing);
        if (_inventoryRuntime == null)
            TryGetComponent(out _inventoryRuntime);
    }

    public void Bind(GameObject body)
    {
        EnsureRefs();
        _body = body;
        _bodyTransform = body != null ? body.transform : null;
        _bodyState = body != null ? body.GetBodyComponent<CharacterState>() : null;

        CharacterMotor motor = body != null ? body.GetBodyComponent<CharacterMotor>() : null;
        CharacterAttacker attacker = body != null ? body.GetBodyComponent<CharacterAttacker>() : null;
        CharacterActionHost actionHost = body != null ? body.GetBodyComponent<CharacterActionHost>() : null;
        CharacterFacingAnim facing = body != null ? body.GetBodyComponent<CharacterFacingAnim>() : null;

        _movement?.BindBody(motor, _bodyState, facing);
        _aimController?.BindBody(_bodyState, _bodyTransform);
        _combatController?.BindBody(attacker, _bodyState, actionHost, Camera.main);
        _stealthController?.BindBody(_bodyState, _movement);

        _floorVisibility?.SetPlayerState(_bodyState);
        _sightLineBlend?.SetPlayerState(_bodyState);
        _visibilityBroadcaster?.BindPlayerState(_bodyState);
        _sightFade?.SetPlayerState(_bodyState);
        _sightFade?.SetPlayerBody(_bodyTransform);
        _hearingPing?.SetPlayerState(_bodyState);
        _hearingPing?.SetPlayerBody(_bodyTransform);

        TileMapManager map = FindFirstObjectByType<TileMapManager>();
        if (map != null && map.MapCollisionServices != null)
            _aimController?.BindMapCollision(map.MapCollisionServices.LineCast);
        if (map != null)
        {
            _sightFade?.Init(map);
            _hearingPing?.Init(map);
        }

        if (body != null)
            PlayerSightVisionBinder.Bind(this);
        else
            PlayerSightVisionBinder.Clear();

        if (body != null && body.TryGetBodyComponent(out CharacterSessionHub session))
            session.BecomePlayer(_movement, _inventoryRuntime);
        else if (body != null)
            Debug.LogError("[PlayerPossessedInputHost] CharacterSessionHub is required on the possessed body.", this);
    }

    public void SetControlEnabled(bool enabled)
    {
        _movement?.SetControllEnabled(enabled);
        _aimController?.SetEnabled(enabled);
        _combatController?.SetEnabled(enabled);
        _stealthController?.SetEnabled(enabled);
        _tileObjectPointer?.SetEnabled(enabled);
    }

    /// <summary>스크립트 조향 중 입력 차단. possessed·Player 채널 유지.</summary>
    public void SetScriptedLocomotionInput(bool allowPlayerInput)
    {
        _movement?.SetMovementInputEnabled(allowPlayerInput);
        _aimController?.SetEnabled(allowPlayerInput);
        _combatController?.SetEnabled(allowPlayerInput);
        _stealthController?.SetEnabled(allowPlayerInput);
        _tileObjectPointer?.SetEnabled(allowPlayerInput);
    }
}
