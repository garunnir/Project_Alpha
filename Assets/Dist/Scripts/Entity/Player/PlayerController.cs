// ============================================================
// PlayerController — 오브젝트의 플레이어 기능을 통합 관리하는 컴포넌트
// ============================================================
// 레거시: NpcSample·possess 경로는 PlayerPossessedInputHost. 이 타입은 씬 레거시용.
using UnityEngine;
using Interactions;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(CharacterLocomotionAnim))]
[RequireComponent(typeof(PlayerInteractionController))]
[RequireComponent(typeof(PlayerAimController))]
[RequireComponent(typeof(TileObjectPointerController))]
public class PlayerController : MonoBehaviour, IPlayControllable
{
    [Title("References")]
    [Required, SerializeField] private CharacterState _state;
    [Required, SerializeField] private CharacterLocomotionAnim _locomotionAnim;
    [SerializeField] private CharacterFacingAnim _directionAnim;
    [Required, SerializeField] private PlayerInteractionController _interaction;
    [Required, SerializeField] private PlayerAimController _aimController;
    [SerializeField] private TileObjectPointerController _tileObjectPointer;
    [SerializeField] private PlayerCombatController _combatController;

    public CharacterState State => _state;
    public CharacterLocomotionAnim LocomotionAnim => _locomotionAnim;
    public CharacterFacingAnim DirectionAnim => _directionAnim;
    public PlayerInteractionController Interaction => _interaction;

    [ShowInInspector, ReadOnly]
    [PropertyOrder(10)]
    private bool HasMovable => _movable != null;

    private IMovable _movable;

    private void Awake()
    {
        EnsureReferences();
        EnsureRuntimeComponents();
        _movable = GetComponent<IMovable>();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void Reset()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (!_state) TryGetComponent(out _state);
        if (!_locomotionAnim) TryGetComponent(out _locomotionAnim);
        if (!_directionAnim) TryGetComponent(out _directionAnim);
        if (!_interaction) TryGetComponent(out _interaction);
        if (!_aimController) TryGetComponent(out _aimController);
        if (!_tileObjectPointer) TryGetComponent(out _tileObjectPointer);
        if (!_combatController) TryGetComponent(out _combatController);
        if (_movable == null) TryGetComponent(out _movable);
    }

    void EnsureRuntimeComponents()
    {
        if (_tileObjectPointer != null)
            return;

        if (!TryGetComponent(out _tileObjectPointer))
        {
            Debug.LogError(
                $"[PlayerController] '{name}' needs TileObjectPointerController on the prefab.",
                this);
        }
    }

    public void SetControlEnabled(bool enabled)
    {
        _movable?.SetControllEnabled(enabled);
        _aimController?.SetEnabled(enabled);
        _combatController?.SetEnabled(enabled);
        _tileObjectPointer?.SetEnabled(enabled);
    }
}
