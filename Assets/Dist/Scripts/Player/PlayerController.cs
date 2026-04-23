// ============================================================
// PlayerController — 오브젝트의 플레이어 기능을 통합 관리하는 컴포넌트
// ============================================================
using UnityEngine;
using Interactions;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(PlayerInputDirectionAnim))]
[RequireComponent(typeof(PlayerInteractionController))]
[RequireComponent(typeof(PlayerLookController))]
public class PlayerController : MonoBehaviour, IPlayControllable
{
    [Title("References")]
    [Required, SerializeField] private CharacterState _state;
    [Required, SerializeField] private PlayerInputDirectionAnim _directionAnim;
    [Required, SerializeField] private PlayerInteractionController _interaction;
    [Required, SerializeField] private PlayerLookController _aimController;

    public CharacterState State => _state;
    public PlayerInputDirectionAnim DirectionAnim => _directionAnim;
    public PlayerInteractionController Interaction => _interaction;

    [ShowInInspector, ReadOnly]
    [PropertyOrder(10)]
    private bool HasMovable => _movable != null;

    private IMovable _movable;

    private void Awake()
    {
        EnsureReferences();
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
        if (!_directionAnim) TryGetComponent(out _directionAnim);
        if (!_interaction) TryGetComponent(out _interaction);
        if (!_aimController) TryGetComponent(out _aimController);
        if (_movable == null) TryGetComponent(out _movable);
    }

    public void SetControlEnabled(bool enabled)
    {
        _movable?.SetControllEnabled(enabled);
        _aimController?.SetEnabled(enabled);
    }
}
