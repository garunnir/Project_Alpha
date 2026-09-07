// ============================================================
// ICellTargetSessionTickable — CellTargetSessionDriver가 틱하는 세션
// ============================================================

public interface ICellTargetSessionTickable : IUiCancelConsumer
{
    void Tick();
    void Cancel();
}
