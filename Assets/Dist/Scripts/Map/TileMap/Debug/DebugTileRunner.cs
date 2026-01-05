     using System;
namespace IsoTilemap
{
        public class DebugTileRunner : IFrameState
    {
        Action _action;
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Tick(float dt)
        {
            // Debug.Log("DebugRunner Tick: " + dt);
            _action?.Invoke();
        }
        public DebugTileRunner(Action action)
        {
            _action = action;
        }
    }
}