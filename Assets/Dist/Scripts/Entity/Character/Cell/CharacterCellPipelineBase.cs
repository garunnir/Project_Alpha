// ============================================================
// CharacterCellPipelineBase — Cell Arrive+Work 공통 (이동 취소·틱)
// ============================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class CharacterCellPipelineBase
{
    protected readonly CharacterActionHost ActionHost;
    protected readonly CharacterArriveHost ArriveHost;
    protected readonly CharacterTimedWorkPlayback Work = new();
    protected readonly CharacterMotor Motor;

    bool _moveCancelSubscribed;
    bool _pipelineActive;

    protected CharacterCellPipelineBase(
        CharacterActionHost actionHost,
        CharacterArriveHost arriveHost,
        CharacterMotor motor)
    {
        ActionHost = actionHost;
        ArriveHost = arriveHost;
        Motor = motor;
    }

    public bool IsBusy =>
        _pipelineActive ||
        (ArriveHost != null && ArriveHost.IsBusy) ||
        Work.IsBusy;

    public float WorkProgress01 => Work.IsBusy ? Work.Progress01 : 0f;

    public void BindWorkAnim(Animator animator, int workLayerIndex) =>
        Work.Bind(animator, workLayerIndex);

    public void Tick(float deltaTime)
    {
        if (!Work.IsBusy)
            return;

        Work.Tick(deltaTime);
    }

    public virtual void Cancel()
    {
        UnsubscribeMoveCancel();
        _pipelineActive = false;
        ArriveHost?.Cancel();
        Work.Cancel();
    }

    public void OnOwnerDisabled()
    {
        UnsubscribeMoveCancel();
        _pipelineActive = false;
    }

    protected bool BeginArrive(
        Vector3 destination,
        float stopping,
        Action onArrived,
        Func<bool> tryIsArrived = null)
    {
        if (ArriveHost == null)
            return false;

        bool started = ArriveHost.TryBegin(
            destination,
            stopping,
            onArrived,
            onCancelled: EndPipeline,
            suppressInput: true,
            tryIsArrived: tryIsArrived);

        if (!started)
            return false;

        _pipelineActive = true;
        SubscribeMoveCancel();
        return true;
    }

    protected void EndPipeline()
    {
        UnsubscribeMoveCancel();
        _pipelineActive = false;
    }

    protected TimeScaleChannel ResolveTimeChannel() =>
        Motor != null && Motor.IsPossessed
            ? TimeScaleChannel.Player
            : TimeScaleChannel.World;

    void SubscribeMoveCancel()
    {
        if (_moveCancelSubscribed)
            return;

        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.PlayerMovePerformed += OnMovePerformedWhileBusy;
        _moveCancelSubscribed = true;
    }

    void UnsubscribeMoveCancel()
    {
        if (!_moveCancelSubscribed)
            return;

        InputManager input = InputManager.Instance;
        if (input != null)
            input.PlayerMovePerformed -= OnMovePerformedWhileBusy;

        _moveCancelSubscribed = false;
    }

    void OnMovePerformedWhileBusy(InputAction.CallbackContext ctx)
    {
        if (!IsBusy)
            return;

        Vector2 dir = ctx.ReadValue<Vector2>();
        if (dir.sqrMagnitude <= Mathf.Epsilon)
            return;

        if (ActionHost != null)
            ActionHost.CancelAll();
        else
            Cancel();
    }
}
