// ============================================================
// InputManager — 입력 정책 SSOT
// ============================================================
// [A] UiMenu 입력  : AcquireUiMenuInput — 건설 등. Player 맵 OFF, UI 맵 ON.
// [B] PlayerAction : SuppressPlayerAction — 인벤 창 위 등. Zoom/Aim만 끔, Move는 유지.
// [C] Debug 입력   : AcquireDebugInput — 콘솔. Player OFF, UI+Debug ON.
// 소비: Player* / Ui* 이벤트 또는 TryRead* — Actions 직접 접근 금지.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : SceneSingleton<InputManager>
{
    public static Func<bool> click;

    InputActions _actions;
    InputAction _statusToggle;
    InputAction _stealthToggle;
    InputAction _diveHold;
    InputAction _swimRiseHold;
    InputAction _combatCycle;
    InputAction _combatAttack;

    public bool IsUiMenuInputActive => _uiMenuInputOwners.Count > 0;
    public bool IsDebugInputActive => _debugInputOwners.Count > 0;

    readonly HashSet<object> _uiMenuInputOwners = new();
    readonly HashSet<object> _debugInputOwners = new();
    readonly Dictionary<PlayerAction, HashSet<object>> _suppressedActions = new();
    bool _gameplayBlockedApplied;
    bool _debugMapApplied;
    /// <summary>LookAt Hold(duration) performed가 한 번이라도 왔는지. 임계는 InputActions SSOT.</summary>
    bool _lookAtHoldPerformed;

    public event Action<InputAction.CallbackContext> PlayerMovePerformed;
    public event Action<InputAction.CallbackContext> PlayerMoveCanceled;
    public event Action<InputAction.CallbackContext> PlayerRunPerformed;
    public event Action<InputAction.CallbackContext> PlayerRunCanceled;
    /// <summary>LookAt Hold performed — RMB 홀드 확정(조준 시작).</summary>
    public event Action<InputAction.CallbackContext> PlayerLookAtPerformed;
    /// <summary>LookAt canceled — RMB 릴리즈(조준 종료).</summary>
    public event Action<InputAction.CallbackContext> PlayerLookAtCanceled;
    /// <summary>LookAt이 Hold 확정 없이 canceled — RMB 짧은 탭(컨텍스트 메뉴).</summary>
    public event Action<InputAction.CallbackContext> PlayerLookAtTapPerformed;
    public event Action<InputAction.CallbackContext> PlayerInteractPerformed;
    public event Action<InputAction.CallbackContext> PlayerInteractStarted;
    public event Action<InputAction.CallbackContext> PlayerInteractCanceled;
    public event Action<InputAction.CallbackContext> PlayerInventoryTogglePerformed;
    public event Action<InputAction.CallbackContext> PlayerStatusTogglePerformed;
    public event Action<InputAction.CallbackContext> PlayerStealthTogglePerformed;
    public event Action<InputAction.CallbackContext> PlayerDivePerformed;
    public event Action<InputAction.CallbackContext> PlayerDiveCanceled;
    public event Action<InputAction.CallbackContext> PlayerSwimRisePerformed;
    public event Action<InputAction.CallbackContext> PlayerSwimRiseCanceled;
    public event Action<InputAction.CallbackContext> PlayerCombatCyclePerformed;
    public event Action<InputAction.CallbackContext> PlayerCombatAttackPerformed;

    public event Action<InputAction.CallbackContext> UiNavigateStarted;
    public event Action<InputAction.CallbackContext> UiNavigateCanceled;
    public event Action<InputAction.CallbackContext> UiSubmitPerformed;
    public event Action<InputAction.CallbackContext> UiPaginationPerformed;

    protected override void Awake()
    {
        base.Awake();
        click = IsClike;
        _actions = new InputActions();
        WireActionCallbacks();
        // StatusToggle / StealthToggle / Combat — inputactions codegen 갱신 전 런타임 바인드.
        _statusToggle = new InputAction("StatusToggle", InputActionType.Button, "<Keyboard>/tab");
        _statusToggle.performed += ForwardPlayerStatusTogglePerformed;
        _stealthToggle = new InputAction("StealthToggle", InputActionType.Button, "<Keyboard>/c");
        _stealthToggle.performed += ForwardPlayerStealthTogglePerformed;
        _diveHold = new InputAction("Dive", InputActionType.Button, "<Keyboard>/leftCtrl");
        _diveHold.performed += ForwardPlayerDivePerformed;
        _diveHold.canceled += ForwardPlayerDiveCanceled;
        _swimRiseHold = new InputAction("SwimRise", InputActionType.Button, "<Keyboard>/space");
        _swimRiseHold.performed += ForwardPlayerSwimRisePerformed;
        _swimRiseHold.canceled += ForwardPlayerSwimRiseCanceled;
        _combatCycle = new InputAction("CombatCycle", InputActionType.Button, "<Keyboard>/q");
        _combatCycle.performed += ForwardPlayerCombatCyclePerformed;
        // 조준(RMB Hold) 중 LMB 시전. Interact는 E라 충돌 없음.
        _combatAttack = new InputAction("CombatAttack", InputActionType.Button, "<Mouse>/leftButton");
        _combatAttack.performed += ForwardPlayerCombatAttackPerformed;
        _actions.Player.Enable();
        EnableCombatRuntimeActions();
        _actions.Debug.Disable();
    }

    public IDisposable AcquireUiMenuInput(object owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        _uiMenuInputOwners.Add(owner);
        ApplyActionMaps();
        return new UiMenuInputScope(this, owner);
    }

    public IDisposable AcquireDebugInput(object owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        _debugInputOwners.Add(owner);
        ApplyActionMaps();
        return new DebugInputScope(this, owner);
    }

    public void SuppressPlayerAction(PlayerAction action, object owner, bool suppress)
    {
        if (owner == null)
            return;

        if (!_suppressedActions.TryGetValue(action, out HashSet<object> owners))
        {
            owners = new HashSet<object>();
            _suppressedActions[action] = owners;
        }

        bool changed = suppress ? owners.Add(owner) : owners.Remove(owner);
        if (!changed)
            return;
    }

    public bool IsPlayerActionEnabled(PlayerAction action)
    {
        if (IsGameplayBlocked)
            return false;

        return !_suppressedActions.TryGetValue(action, out HashSet<object> owners) || owners.Count == 0;
    }

    bool IsGameplayBlocked => IsDebugInputActive || IsUiMenuInputActive;

    public bool TryReadZoomScroll(out float scrollY)
    {
        scrollY = 0f;
        if (!IsPlayerActionEnabled(PlayerAction.Zoom))
            return false;

        scrollY = _actions.Player.Zoom.ReadValue<Vector2>().y;
        return true;
    }

    public bool TryReadMove(out Vector2 move)
    {
        move = Vector2.zero;
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return false;

        move = _actions.Player.Move.ReadValue<Vector2>();
        return true;
    }

    public bool TryReadRunHeld(out bool isHeld)
    {
        isHeld = false;
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return false;

        isHeld = _actions.Player.Run.IsPressed();
        return true;
    }

    /// <summary>포인터 primary press가 현재 눌려 있는지.</summary>
    public bool TryReadPointerHeld(out bool isHeld)
    {
        isHeld = false;
        if (IsGameplayBlocked)
            return false;

        if (Pointer.current != null)
        {
            isHeld = Pointer.current.press.isPressed;
            return true;
        }

        if (Mouse.current != null)
        {
            isHeld = Mouse.current.leftButton.isPressed;
            return true;
        }

        return false;
    }

    public bool TryReadPointerScreenPosition(out Vector2 position)
    {
        position = Vector2.zero;

        if (Pointer.current != null)
        {
            position = Pointer.current.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        return false;
    }

    /// <summary>포인터 이동량(이 프레임 delta). 디바이스 없으면 false.</summary>
    public bool TryReadPointerDelta(out Vector2 delta)
    {
        delta = Vector2.zero;
        if (Pointer.current == null)
            return false;

        delta = Pointer.current.delta.ReadValue();
        return true;
    }

    /// <summary>포인터 primary press가 이 프레임에 눌렸는지.</summary>
    public bool TryReadPointerPressedThisFrame(out bool pressed)
    {
        pressed = Pointer.current?.press.wasPressedThisFrame ?? false;
        return Pointer.current != null;
    }

    /// <summary>포인터 primary press가 이 프레임에 떨어졌는지.</summary>
    public bool TryReadPointerReleasedThisFrame(out bool released)
    {
        released = Pointer.current?.press.wasReleasedThisFrame ?? false;
        return Pointer.current != null;
    }

    /// <summary>Cancel(Escape / UI Cancel)이 이 프레임에 수행됐는지.</summary>
    public bool TryReadCancelPerformedThisFrame(out bool canceled)
    {
        canceled = false;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            canceled = true;

        if (!canceled && IsUiMenuInputActive && _actions != null &&
            _actions.UI.Cancel.WasPerformedThisFrame())
            canceled = true;

        return true;
    }

    void WireActionCallbacks()
    {
        _actions.Player.Move.performed += ForwardPlayerMovePerformed;
        _actions.Player.Move.canceled += ForwardPlayerMoveCanceled;
        _actions.Player.Run.performed += ForwardPlayerRunPerformed;
        _actions.Player.Run.canceled += ForwardPlayerRunCanceled;
        _actions.Player.LookAt.performed += ForwardPlayerLookAtPerformed;
        _actions.Player.LookAt.canceled += ForwardPlayerLookAtCanceled;
        _actions.Player.Interaction.started += ForwardPlayerInteractStarted;
        _actions.Player.Interaction.performed += ForwardPlayerInteractPerformed;
        _actions.Player.Interaction.canceled += ForwardPlayerInteractCanceled;
        _actions.Player.InventoryToggle.performed += ForwardPlayerInventoryTogglePerformed;

        _actions.UI.Navigate.started += ForwardUiNavigateStarted;
        _actions.UI.Navigate.canceled += ForwardUiNavigateCanceled;
        _actions.UI.Submit.performed += ForwardUiSubmitPerformed;
        _actions.UI.Pagination.performed += ForwardUiPaginationPerformed;
    }

    void UnwireActionCallbacks()
    {
        if (_actions == null)
            return;

        _actions.Player.Move.performed -= ForwardPlayerMovePerformed;
        _actions.Player.Move.canceled -= ForwardPlayerMoveCanceled;
        _actions.Player.Run.performed -= ForwardPlayerRunPerformed;
        _actions.Player.Run.canceled -= ForwardPlayerRunCanceled;
        _actions.Player.LookAt.performed -= ForwardPlayerLookAtPerformed;
        _actions.Player.LookAt.canceled -= ForwardPlayerLookAtCanceled;
        _actions.Player.Interaction.started -= ForwardPlayerInteractStarted;
        _actions.Player.Interaction.performed -= ForwardPlayerInteractPerformed;
        _actions.Player.Interaction.canceled -= ForwardPlayerInteractCanceled;
        _actions.Player.InventoryToggle.performed -= ForwardPlayerInventoryTogglePerformed;

        _actions.UI.Navigate.started -= ForwardUiNavigateStarted;
        _actions.UI.Navigate.canceled -= ForwardUiNavigateCanceled;
        _actions.UI.Submit.performed -= ForwardUiSubmitPerformed;
        _actions.UI.Pagination.performed -= ForwardUiPaginationPerformed;
    }

    void ForwardPlayerMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerMovePerformed?.Invoke(ctx);
    }

    void ForwardPlayerMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerMoveCanceled?.Invoke(ctx);
    }

    void ForwardPlayerRunPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerRunPerformed?.Invoke(ctx);
    }

    void ForwardPlayerRunCanceled(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerRunCanceled?.Invoke(ctx);
    }

    void ForwardPlayerLookAtPerformed(InputAction.CallbackContext ctx)
    {
        // Hold 확정 자체는 Aim suppress와 무관하게 기록(짧은 탭 오인 방지).
        _lookAtHoldPerformed = true;

        if (!IsPlayerActionEnabled(PlayerAction.Aim))
            return;

        PlayerLookAtPerformed?.Invoke(ctx);
    }

    void ForwardPlayerLookAtCanceled(InputAction.CallbackContext ctx)
    {
        bool wasHold = _lookAtHoldPerformed;
        _lookAtHoldPerformed = false;

        PlayerLookAtCanceled?.Invoke(ctx);

        // Hold(duration) 미확정 릴리즈 = 탭. 임계는 InputActions LookAt Hold SSOT.
        if (!wasHold && IsPlayerActionEnabled(PlayerAction.Aim))
            PlayerLookAtTapPerformed?.Invoke(ctx);
    }

    void ForwardPlayerInteractStarted(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Interact))
            return;

        PlayerInteractStarted?.Invoke(ctx);
    }

    void ForwardPlayerInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Interact))
            return;

        PlayerInteractPerformed?.Invoke(ctx);
    }

    void ForwardPlayerInteractCanceled(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Interact))
            return;

        PlayerInteractCanceled?.Invoke(ctx);
    }

    void ForwardPlayerInventoryTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBlocked)
            return;

        PlayerInventoryTogglePerformed?.Invoke(ctx);
    }

    void ForwardPlayerStatusTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBlocked)
            return;

        PlayerStatusTogglePerformed?.Invoke(ctx);
    }

    void ForwardPlayerStealthTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBlocked)
            return;

        PlayerStealthTogglePerformed?.Invoke(ctx);
    }

    void ForwardPlayerDivePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerDivePerformed?.Invoke(ctx);
    }

    void ForwardPlayerDiveCanceled(InputAction.CallbackContext ctx)
    {
        PlayerDiveCanceled?.Invoke(ctx);
    }

    void ForwardPlayerSwimRisePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsPlayerActionEnabled(PlayerAction.Move))
            return;

        PlayerSwimRisePerformed?.Invoke(ctx);
    }

    void ForwardPlayerSwimRiseCanceled(InputAction.CallbackContext ctx)
    {
        PlayerSwimRiseCanceled?.Invoke(ctx);
    }

    void ForwardPlayerCombatCyclePerformed(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBlocked)
            return;

        PlayerCombatCyclePerformed?.Invoke(ctx);
    }

    void ForwardPlayerCombatAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBlocked)
            return;

        // 조준이 막힌 상태(인벤 창 위 등)에서는 클릭 시전 금지.
        if (!IsPlayerActionEnabled(PlayerAction.Aim))
            return;

        PlayerCombatAttackPerformed?.Invoke(ctx);
    }

    void ForwardUiNavigateStarted(InputAction.CallbackContext ctx)
    {
        if (!IsUiMenuInputActive)
            return;

        UiNavigateStarted?.Invoke(ctx);
    }

    void ForwardUiNavigateCanceled(InputAction.CallbackContext ctx)
    {
        if (!IsUiMenuInputActive)
            return;

        UiNavigateCanceled?.Invoke(ctx);
    }

    void ForwardUiSubmitPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsUiMenuInputActive)
            return;

        UiSubmitPerformed?.Invoke(ctx);
    }

    void ForwardUiPaginationPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsUiMenuInputActive)
            return;

        UiPaginationPerformed?.Invoke(ctx);
    }

    void ReleaseUiMenuInput(object owner)
    {
        if (owner == null || !_uiMenuInputOwners.Remove(owner))
            return;

        ApplyActionMaps();
    }

    void ReleaseDebugInput(object owner)
    {
        if (owner == null || !_debugInputOwners.Remove(owner))
            return;

        ApplyActionMaps();
    }

    void ApplyActionMaps()
    {
        bool gameplayBlocked = IsGameplayBlocked;
        bool debugMap = IsDebugInputActive;
        if (gameplayBlocked == _gameplayBlockedApplied && debugMap == _debugMapApplied)
            return;

        _gameplayBlockedApplied = gameplayBlocked;
        _debugMapApplied = debugMap;

        if (gameplayBlocked)
        {
            _actions.Player.Disable();
            DisableCombatRuntimeActions();
            _actions.UI.Enable();
        }
        else
        {
            _actions.UI.Disable();
            _actions.Player.Enable();
            EnableCombatRuntimeActions();
        }

        if (debugMap)
            _actions.Debug.Enable();
        else
            _actions.Debug.Disable();
    }

    void EnableCombatRuntimeActions()
    {
        _statusToggle?.Enable();
        _stealthToggle?.Enable();
        _diveHold?.Enable();
        _swimRiseHold?.Enable();
        _combatCycle?.Enable();
        _combatAttack?.Enable();
    }

    void DisableCombatRuntimeActions()
    {
        _statusToggle?.Disable();
        _stealthToggle?.Disable();
        _diveHold?.Disable();
        _swimRiseHold?.Disable();
        _combatCycle?.Disable();
        _combatAttack?.Disable();
    }

    protected override void OnDestroy()
    {
        UnwireActionCallbacks();
        DisposeRuntimeAction(ref _statusToggle, ForwardPlayerStatusTogglePerformed);
        DisposeRuntimeAction(ref _stealthToggle, ForwardPlayerStealthTogglePerformed);
        if (_diveHold != null)
        {
            _diveHold.performed -= ForwardPlayerDivePerformed;
            _diveHold.canceled -= ForwardPlayerDiveCanceled;
            _diveHold.Disable();
            _diveHold.Dispose();
            _diveHold = null;
        }
        if (_swimRiseHold != null)
        {
            _swimRiseHold.performed -= ForwardPlayerSwimRisePerformed;
            _swimRiseHold.canceled -= ForwardPlayerSwimRiseCanceled;
            _swimRiseHold.Disable();
            _swimRiseHold.Dispose();
            _swimRiseHold = null;
        }
        DisposeRuntimeAction(ref _combatCycle, ForwardPlayerCombatCyclePerformed);
        DisposeRuntimeAction(ref _combatAttack, ForwardPlayerCombatAttackPerformed);

        _actions?.Dispose();
        _actions = null;
        base.OnDestroy();
    }

    static void DisposeRuntimeAction(
        ref InputAction action,
        System.Action<InputAction.CallbackContext> handler)
    {
        if (action == null)
            return;

        action.performed -= handler;
        action.Disable();
        action.Dispose();
        action = null;
    }

    bool IsClike()
    {
        return TryReadPointerPressedThisFrame(out bool pressed) && pressed;
    }

    public static RaycastHit RayCast() //todo 공통사용가능한 부위로 옮겨야함.
    {
        var screenPos = Pointer.current?.position.ReadValue() ?? Vector2.zero;
        var ray = Camera.main.ScreenPointToRay(screenPos);
        Physics.Raycast(ray, out RaycastHit info);
        if (info.collider != null) Debug.Log(info.collider.name);
        return info;
    }

    sealed class UiMenuInputScope : IDisposable
    {
        readonly InputManager _manager;
        readonly object _owner;
        bool _disposed;

        public UiMenuInputScope(InputManager manager, object owner)
        {
            _manager = manager;
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _manager?.ReleaseUiMenuInput(_owner);
        }
    }

    sealed class DebugInputScope : IDisposable
    {
        readonly InputManager _manager;
        readonly object _owner;
        bool _disposed;

        public DebugInputScope(InputManager manager, object owner)
        {
            _manager = manager;
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _manager?.ReleaseDebugInput(_owner);
        }
    }
}
