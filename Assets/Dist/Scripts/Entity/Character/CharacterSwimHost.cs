// ============================================================
// CharacterSwimHost — 발밑 immersion → Swim 모드·이동 배율·수직 입력
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterState))]
[DefaultExecutionOrder(5)]
public sealed class CharacterSwimHost : MonoBehaviour
{
    CharacterState _state;
    CharacterMotor _motor;
    PlayerMovement _movement;
    CharacterBodyHost _bodyHost;
    CharacterBreathHost _breath;
    TileMapManager _tileMapManager;
    float _cellSize = 1f;
    bool _diveHeld;
    bool _riseHeld;
    bool _inputConnected;
    MapSwimImmersion _last;

    public MapSwimImmersion LastImmersion => _last;
    public bool DiveHeld => _diveHeld;
    public CharacterBreathHost Breath => _breath;

    void Awake()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _state = refs.State;
            _motor = refs.Motor;
            _bodyHost = refs.BodyHost;
            refs.TryGet(out CharacterPainHost pain);
            _breath = new CharacterBreathHost(_bodyHost, pain);
        }
        else
        {
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
            _motor = CharacterBodyResolve.GetInBody<CharacterMotor>(this);
            _bodyHost = CharacterBodyResolve.GetInBody<CharacterBodyHost>(this);
            CharacterBodyResolve.TryGetInBody(this, out CharacterPainHost pain);
            _breath = new CharacterBreathHost(_bodyHost, pain);
        }

        _movement = CharacterBodyResolve.GetInBody<PlayerMovement>(this);
    }

    void OnEnable()
    {
        if (_motor != null && _motor.IsPossessed)
            ConnectSwimInput();
    }

    void OnDisable() => DisconnectSwimInput();

    void Update()
    {
        float dt = TimeScaleService.Delta(
            _motor != null && _motor.IsPossessed
                ? TimeScaleChannel.Player
                : TimeScaleChannel.World);
        if (dt <= 0f)
            return;

        SyncPossessedInput();
        RefreshImmersion();
        ApplyMovementGate();
        ApplyVerticalInput();
        _breath?.TickSwim(dt, _last);
    }

    public void NotifyPossessedChanged(bool possessed)
    {
        if (possessed)
            ConnectSwimInput();
        else
        {
            DisconnectSwimInput();
            _diveHeld = false;
            _riseHeld = false;
        }
    }

    void SyncPossessedInput()
    {
        bool possessed = _motor != null && _motor.IsPossessed;
        if (possessed && !_inputConnected)
            ConnectSwimInput();
        else if (!possessed && _inputConnected)
        {
            DisconnectSwimInput();
            _diveHeld = false;
            _riseHeld = false;
        }
    }

    void RefreshImmersion()
    {
        if (_state == null)
            _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        if (_state == null)
            return;

        Vector3 feet = CharacterFeetPose.GetFeetWorld(transform.position, CharacterFeetPose.GetFeetOffset(transform));
        ResolveCellSize();
        bool diveWanted = _diveHeld && (_motor == null || _motor.IsPossessed);

        _last = MapSwimQuery.Resolve(feet, _cellSize, diveWanted, _state.GridFootprint);

        bool wading = _last.Mode == MapSwimMode.Wade;
        bool swimming = _last.Mode == MapSwimMode.Swim;
        bool diving = _last.Mode == MapSwimMode.Dive;
        _state.SetSwimMode(wading, swimming, diving);
    }

    void ApplyMovementGate()
    {
        float factor = 1f;
        bool blockSprint = false;
        float sprintFactor = 1f;

        switch (_last.Mode)
        {
            case MapSwimMode.Wade:
                factor = MapSwimConsts.WadeSpeedFactor;
                sprintFactor = MapSwimConsts.WadeSprintFactor;
                break;
            case MapSwimMode.Swim:
                factor = MapSwimConsts.SwimSpeedFactor;
                blockSprint = true;
                break;
            case MapSwimMode.Dive:
                factor = MapSwimConsts.DiveSpeedFactor;
                blockSprint = true;
                break;
        }

        _motor?.SetSwimMovement(factor);
        if (_movement != null)
        {
            bool active = _last.Mode != MapSwimMode.Dry;
            _movement.SetSwimMovement(active, factor, blockSprint, sprintFactor);
        }
    }

    void ApplyVerticalInput()
    {
        if (_state == null)
            return;

        if (!_last.CanSwim
            || _last.Mode == MapSwimMode.Dry
            || _last.Mode == MapSwimMode.Wade)
        {
            _state.SetSwimVerticalInput(0f);
            return;
        }

        float vertical = 0f;
        if (_diveHeld)
            vertical = -1f;
        else if (_riseHeld)
            vertical = 1f;

        if (NeedsEmergencyAscend())
            vertical = 1f;

        _state.SetSwimVerticalInput(vertical);
    }

    bool NeedsEmergencyAscend()
    {
        ICharacterBody body = _bodyHost != null ? _bodyHost.Body : null;
        if (body == null || body.IsDeadState)
            return false;

        return _last.HeadSubmerged && BodyCapacity.IsCapacityDowned(body);
    }

    void ResolveCellSize()
    {
        if (_tileMapManager == null)
            _tileMapManager = FindFirstObjectByType<TileMapManager>();

        IWorldGrid grid = _tileMapManager != null ? _tileMapManager.WorldGrid : null;
        if (grid != null && grid.CellSize > 0f)
            _cellSize = grid.CellSize;
    }

    void ConnectSwimInput()
    {
        InputManager input = InputManager.Instance;
        if (input == null || _inputConnected)
            return;

        input.PlayerDivePerformed += OnDive;
        input.PlayerDiveCanceled += OnDive;
        input.PlayerSwimRisePerformed += OnSwimRise;
        input.PlayerSwimRiseCanceled += OnSwimRise;
        _inputConnected = true;
    }

    void DisconnectSwimInput()
    {
        InputManager input = InputManager.Instance;
        if (input == null || !_inputConnected)
            return;

        input.PlayerDivePerformed -= OnDive;
        input.PlayerDiveCanceled -= OnDive;
        input.PlayerSwimRisePerformed -= OnSwimRise;
        input.PlayerSwimRiseCanceled -= OnSwimRise;
        _inputConnected = false;
        _diveHeld = false;
        _riseHeld = false;
    }

    void OnDive(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _diveHeld = false;
            return;
        }

        if (context.performed || context.started)
            _diveHeld = true;
    }

    void OnSwimRise(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            _riseHeld = false;
            return;
        }

        if (context.performed || context.started)
            _riseHeld = true;
    }
}
