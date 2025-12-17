using UnityEngine;

public interface IFrameState
{
    void Enter();
    void Tick(float dt);
    void Exit();
}

public class StateRunner : Singleton<StateRunner>
{
    IFrameState _state;

    void Update() => _state?.Tick(Time.deltaTime);

    public void ChangeState(IFrameState next)
    {
        if (ReferenceEquals(_state, next)) return;
        _state?.Exit();
        _state = next;
        _state?.Enter();
    }
}