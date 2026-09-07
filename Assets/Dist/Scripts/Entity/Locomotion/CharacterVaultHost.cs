// ============================================================
// CharacterVaultHost — 담넘기·벽넘기 입력·궤적·행동큐 Cell 종류
// ============================================================

using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(CharacterMotor))]
public sealed class CharacterVaultHost : MonoBehaviour
{
    public const string WorkLayerName = CharacterWorkLayerAnim.LayerName;

    [SerializeField] VaultClipCatalog _clips;

    CharacterState _state;
    CharacterMotor _motor;
    CharacterActionHost _actionHost;
    Rigidbody _rigidbody;
    Animator _animator;
    int _workLayerIndex = -1;
    float _animatorSpeedBeforeVault = 1f;
    bool _applyRootMotionBeforeVault;
    MapCollisionServices _mapCollision;
    float _cellSize = 1f;

    bool _active;
    bool _scriptedBegun;
    bool _moveLockedByUs;
    bool _suppressInput;
    VaultCandidate _candidate;
    Vector3 _startBody;
    Vector3 _peakBody;
    Vector3 _endBody;
    AnimationCurve _progressCurve;
    float _elapsed;
    float _duration;
    float _autoCooldown;

    bool _holdTracking;
    bool _vaultConsumedPress;
    float _holdElapsed;
    VaultCandidate _holdCandidate;

    public bool IsBusy => _active;
    public float Progress01 =>
        _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

    /// <summary>Mantle vault 진행 중 — <see cref="CharacterVaultIkHost"/> 손 IK.</summary>
    public bool IsMantleIkActive =>
        _active && _candidate.Style == VaultCrossStyle.Mantle;

    public VaultCandidate ActiveCandidate => _candidate;

    public float VaultCellSize => _cellSize;

    /// <summary>이번 E press에서 vault 홀드/시전 중인지 (디버그·확장용).</summary>
    public bool SuppressInteractForCurrentPress => _holdTracking || _vaultConsumedPress;

    /// <summary>벽타기 등과 E 홀드 우선순위 — vault 후보가 있으면 true.</summary>
    public bool WouldAcceptInteractPress()
    {
        if (_active || _holdTracking || _motor == null || !_motor.IsPossessed)
            return false;
        if (_motor.IsMoveLocked && !_moveLockedByUs)
            return false;
        if (!CanStartVault())
            return false;
        return TryProbeCandidate(out _);
    }

    void Awake()
    {
        ResolveBodyRefs();
        TryGetComponent(out _rigidbody);
    }

    void ResolveBodyRefs()
    {
        CharacterBodyRefs refs = this.GetBodyRefs();
        if (refs != null)
        {
            _state = refs.State;
            _motor = refs.Motor;
            _actionHost = refs.ActionHost;
            return;
        }

        _state = GetComponent<CharacterState>();
        _motor = GetComponent<CharacterMotor>();
        TryGetComponent(out _actionHost);
    }

    public void Bind(CharacterLocomotionAnim locomotion)
    {
        if (locomotion == null || !locomotion.TryGetWorkAnim(out _animator, out _workLayerIndex))
        {
            _animator = null;
            _workLayerIndex = -1;
        }
    }

    public void SetClipCatalog(VaultClipCatalog clips) => _clips = clips;

    void OnEnable()
    {
        if (_motor != null && _motor.IsPossessed)
            ConnectInput();
    }

    void OnDisable()
    {
        DisconnectInput();
        CancelInternal(invokeEnd: false);
    }

    public void BindMapCollision(MapCollisionServices services)
    {
        _mapCollision = services;
        _cellSize = services != null && services.Query.CellSize > 0f
            ? services.Query.CellSize
            : 1f;
    }

    public void NotifyPossessedChanged(bool possessed)
    {
        if (possessed)
            ConnectInput();
        else
        {
            DisconnectInput();
            CancelHold();
            if (_active)
                Cancel();
        }
    }

    public void Cancel()
    {
        if (!_active)
        {
            CancelHold();
            return;
        }

        CancelInternal(invokeEnd: true);
    }

    /// <summary>
    /// Interaction.performed에서 호출. vault 후보가 있으면 홀드 추적 시작·상호작용 억제.
    /// </summary>
    public bool TryHandleInteractPress()
    {
        if (_active || _motor == null || !_motor.IsPossessed)
            return false;
        if (_holdTracking)
            return true;
        if (_motor.IsMoveLocked && !_moveLockedByUs)
            return false;
        if (!CanStartVault())
            return false;
        if (!TryProbeCandidate(out VaultCandidate candidate))
            return false;

        _holdTracking = true;
        _vaultConsumedPress = false;
        _holdElapsed = 0f;
        _holdCandidate = candidate;
        return true;
    }

    void Update()
    {
        float dt = TimeScaleService.Delta(
            _motor != null && _motor.IsPossessed
                ? TimeScaleChannel.Player
                : TimeScaleChannel.World);
        if (_actionHost != null)
            dt *= _actionHost.ActionTickScale;

        if (_autoCooldown > 0f)
            _autoCooldown = Mathf.Max(0f, _autoCooldown - dt);

        TickHold(dt);
    }

    void FixedUpdate()
    {
        float dt = TimeScaleService.FixedDelta(
            _motor != null && _motor.IsPossessed
                ? TimeScaleChannel.Player
                : TimeScaleChannel.World);
        if (_actionHost != null)
            dt *= _actionHost.ActionTickScale;

        if (_active)
        {
            TickMotion(dt);
            return;
        }

        TryAutoSprintVault();
    }

    void TickHold(float dt)
    {
        if (!_holdTracking || _active)
            return;

        _holdElapsed += dt;

        if (_holdElapsed < VaultConsts.HoldSeconds)
            return;

        _vaultConsumedPress = true;
        _holdTracking = false;
        TryEnqueueVault(_holdCandidate);
    }

    void TryAutoSprintVault()
    {
        if (_autoCooldown > 0f || _holdTracking || _active)
            return;
        if (_motor == null || !_motor.IsPossessed || !_motor.IsSprinting)
            return;
        if (_state == null || _state.MoveDir.sqrMagnitude < 1e-4f)
            return;
        if (!CanStartVault() || _state.IsAiming)
            return;
        if (!TryProbeCandidate(out VaultCandidate candidate))
            return;
        if (!MapVaultQuery.IsAutoSprintEligible(candidate, _state.GridFootprint))
            return;
        if (GetApproachSpeedMps() < VaultConsts.AutoSprintMinApproachSpeedMps)
            return;

        _autoCooldown = VaultConsts.AutoRetryCooldown;
        TryEnqueueVault(candidate);
    }

    /// <summary>이동 입력 방향으로 실제 전진 중인 속도(m/s). 벽에 멈춰 있으면 0.</summary>
    float GetApproachSpeedMps()
    {
        Vector3 moveDir = ResolveVaultApproachDir();
        if (moveDir.sqrMagnitude < 1e-6f || _motor == null)
            return 0f;

        float dt = TimeScaleService.FixedDelta(
            _motor.IsPossessed ? TimeScaleChannel.Player : TimeScaleChannel.World);
        if (_actionHost != null)
            dt *= _actionHost.ActionTickScale;
        if (dt <= 1e-6f)
            return 0f;

        float forward = Vector3.Dot(_motor.LastAppliedDelta, moveDir);
        return Mathf.Max(0f, forward) / dt;
    }

    bool TryEnqueueVault(VaultCandidate candidate)
    {
        if (_actionHost == null)
            return TryBegin(candidate);

        return _actionHost.TryRunOrEnqueue(
            CharacterActionKind.Cell,
            () => TryBegin(candidate));
    }

    bool TryBegin(VaultCandidate candidate)
    {
        if (_active || _motor == null || _rigidbody == null || _state == null)
            return false;
        if (!CanStartVault())
            return false;

        float baseDuration = _clips != null
            ? _clips.ResolveDuration(candidate.Height, candidate.Style)
            : DefaultDuration(candidate.Height, candidate.Style);
        if (baseDuration <= 0f)
            baseDuration = DefaultDuration(candidate.Height, candidate.Style);

        float approachMps = ResolveApproachSpeedForVault();
        float duration = baseDuration * VaultConsts.ResolveDurationScale(approachMps);

        _candidate = candidate;
        _startBody = _rigidbody.position;
        _endBody = BodyFromFeetCell(candidate.LandingFeetCell);
        _peakBody = BuildPeakBody(_startBody, _endBody, candidate);
        _progressCurve = _clips != null
            ? _clips.ResolveProgressCurve(candidate.Height, candidate.Style)
            : null;
        _elapsed = 0f;
        _duration = duration;
        _active = true;

        _motor.BeginScriptedLocomotion();
        _scriptedBegun = true;
        if (!_motor.IsMoveLocked)
        {
            _motor.SetMoveLocked(true);
            _moveLockedByUs = true;
        }

        if (_motor.IsPossessed)
        {
            SetScriptedInput(false);
            _suppressInput = true;
        }

        if (_animator != null)
        {
            _applyRootMotionBeforeVault = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
        }

        PlayClip(candidate, duration);
        return true;
    }

    /// <summary>시전 순간 접근 속도. 달리기 자동은 전진 delta, E 홀드는 mover 속도 폴백.</summary>
    float ResolveApproachSpeedForVault()
    {
        float mps = GetApproachSpeedMps();
        if (mps > 0.01f)
            return mps;
        if (_motor == null || _state == null || _state.MoveDir.sqrMagnitude < 1e-4f)
            return 0f;

        return Mathf.Max(0f, _motor.CurrentSpeed);
    }

    void TickMotion(float dt)
    {
        _elapsed += dt;
        float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
        Vector3 body = SampleTrajectory(t);
        _rigidbody.MovePosition(body);
        _rigidbody.linearVelocity = Vector3.zero;
        _state.SnapWorldPosition(body);

        if (t < 1f)
            return;

        Finish();
    }

    Vector3 SampleTrajectory(float t)
    {
        if (_candidate.Style == VaultCrossStyle.Mantle &&
            _candidate.Height == VaultHeightClass.High)
        {
            AnimationCurve yProgress = _clips != null
                ? _clips.ResolveHighMantleYProgress()
                : null;
            AnimationCurve xzProgress = _clips != null
                ? _clips.ResolveHighMantleXzProgress()
                : null;
            float xzStartT = _clips != null
                ? _clips.ResolveHighMantleXzStartT()
                : VaultConsts.HighMantleXzStartT;

            return AnimationHybridTrajectory.SampleMantleDecoupled(
                _startBody,
                _endBody,
                t,
                yProgress,
                xzProgress,
                xzStartT);
        }

        return AnimationHybridTrajectory.SampleArc(
            _startBody,
            _peakBody,
            _endBody,
            t,
            _progressCurve);
    }

    Vector3 BuildPeakBody(Vector3 start, Vector3 end, in VaultCandidate candidate)
    {
        if (candidate.Style == VaultCrossStyle.CrossOver)
        {
            Vector3 mid = (start + end) * 0.5f;
            mid.y = Mathf.Max(start.y, end.y) + VaultConsts.CrossPeakHeightCells * _cellSize;
            return mid;
        }

        // Mantle: 먼저 착지 XZ로 올라가며 벽 상단 근처
        Vector3 peak = end;
        peak.y = Mathf.Max(start.y, end.y) + 0.15f * _cellSize;
        if (end.y > start.y + 0.01f)
            peak.y = Mathf.Lerp(start.y, end.y, 0.85f) + 0.1f * _cellSize;
        return peak;
    }

    Vector3 BodyFromFeetCell(Vector3Int feetCell)
    {
        Vector3 feet = TileHelper.ConvertGridToWorldPos(feetCell, _cellSize);
        float feetOffset = CharacterFeetPose.GetFeetOffset(transform);
        return feet + Vector3.up * feetOffset;
    }

    void Finish()
    {
        Vector3 end = _endBody;
        _rigidbody.MovePosition(end);
        _rigidbody.linearVelocity = Vector3.zero;
        _state.SnapWorldPosition(end);
        EndSession();
    }

    void CancelInternal(bool invokeEnd)
    {
        CancelHold();
        if (!_active)
            return;

        if (invokeEnd && _rigidbody != null && _state != null)
        {
            Vector3 pos = _rigidbody.position;
            _state.SnapWorldPosition(pos);
        }

        EndSession();
    }

    void EndSession()
    {
        _active = false;
        _elapsed = 0f;
        _duration = 0f;
        _progressCurve = null;

        if (_scriptedBegun && _motor != null)
        {
            _motor.EndScriptedLocomotion();
            _scriptedBegun = false;
        }

        if (_moveLockedByUs && _motor != null)
        {
            _motor.SetMoveLocked(false);
            _moveLockedByUs = false;
        }

        if (_suppressInput)
        {
            SetScriptedInput(true);
            _suppressInput = false;
        }

        if (_animator != null)
        {
            _animator.speed = _animatorSpeedBeforeVault;
            _animator.applyRootMotion = _applyRootMotionBeforeVault;
        }

        CharacterWorkLayerAnim.Stop(_animator, _workLayerIndex);
    }

    void CancelHold()
    {
        _holdTracking = false;
        _holdElapsed = 0f;
        _holdCandidate = default;
    }

    void OnInteractStarted(InputAction.CallbackContext ctx)
    {
        if (!ctx.started)
            return;

        TryHandleInteractPress();
    }

    void OnInteractCanceled(InputAction.CallbackContext ctx)
    {
        if (_vaultConsumedPress || _active)
        {
            _vaultConsumedPress = false;
            CancelHold();
            return;
        }

        // 짧은 탭: 홀드 미달 — 상호작용은 PlayerInteractionController가 performed에서
        // vault 억제로 막혔을 수 있으므로 여기서는 홀드만 해제.
        // performed는 이미 vault TryHandleInteractPress로 소비됨 → 탭 시 상호작용 재실행 필요.
        bool wasHolding = _holdTracking;
        CancelHold();
        if (!wasHolding)
            return;

        TryFallbackInteract();
    }

    void TryFallbackInteract()
    {
        PlayerPossessedInputHost input = FindFirstObjectByType<PlayerPossessedInputHost>();
        if (input == null || input.Body != gameObject)
            return;

        var interaction = input.GetComponent<Interactions.PlayerInteractionController>();
        interaction?.TryInteractFocused();
    }

    void ConnectInput()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.PlayerInteractStarted -= OnInteractStarted;
        input.PlayerInteractStarted += OnInteractStarted;
        input.PlayerInteractCanceled -= OnInteractCanceled;
        input.PlayerInteractCanceled += OnInteractCanceled;
    }

    void DisconnectInput()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.PlayerInteractStarted -= OnInteractStarted;
        input.PlayerInteractCanceled -= OnInteractCanceled;
    }

    bool CanStartVault()
    {
        if (_mapCollision == null || _state == null)
            return false;
        if (_state.IsSwimming || _state.IsDiving)
            return false;
        return true;
    }

    bool TryProbeCandidate(out VaultCandidate candidate)
    {
        candidate = default;
        if (_mapCollision == null || _state == null)
            return false;

        Vector3 approachDir = ResolveVaultApproachDir();
        if (approachDir.sqrMagnitude < 1e-6f)
            return false;

        return MapVaultQuery.TryFindCandidate(
            _mapCollision.Query,
            _state.GridPos,
            _state.GridFootprint,
            approachDir,
            out candidate);
    }

    /// <summary>이동 입력 방향만. 벽 쪽 WASD를 누른 상태에서만 프로브.</summary>
    Vector3 ResolveVaultApproachDir()
    {
        if (_state == null)
            return Vector3.zero;

        Vector3 move = _state.MoveDir;
        move.y = 0f;
        return move.sqrMagnitude > 1e-4f ? move.normalized : Vector3.zero;
    }

    void PlayClip(in VaultCandidate candidate, float motionDurationSeconds)
    {
        if (_clips == null || _animator == null)
            return;

        AnimationClip clip = _clips.Resolve(candidate.Height, candidate.Style);
        if (clip == null)
            return;

        _animatorSpeedBeforeVault = _animator.speed;
        if (clip.length > 0f && motionDurationSeconds > 0f)
            _animator.speed = clip.length / motionDurationSeconds;

        if (!CharacterWorkLayerAnim.TryPlay(_animator, ref _workLayerIndex, clip) &&
            Config.DebugMode.PlayerPosUpdate)
        {
            Debug.LogWarning(
                $"[CharacterVaultHost] Work clip not played: {clip.name} (layer={_workLayerIndex}).",
                this);
        }
    }

    static float DefaultDuration(VaultHeightClass height, VaultCrossStyle style)
    {
        if (height == VaultHeightClass.Low)
            return style == VaultCrossStyle.Mantle
                ? VaultConsts.LowMantleDurationSeconds
                : VaultConsts.LowCrossDurationSeconds;
        return style == VaultCrossStyle.Mantle
            ? VaultConsts.HighMantleDurationSeconds
            : VaultConsts.HighCrossDurationSeconds;
    }

    static void SetScriptedInput(bool allowPlayerInput)
    {
        PlayerPossessedInputHost input = FindFirstObjectByType<PlayerPossessedInputHost>();
        input?.SetScriptedLocomotionInput(allowPlayerInput);
    }
}
