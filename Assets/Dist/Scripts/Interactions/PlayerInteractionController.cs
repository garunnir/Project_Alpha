using UnityEngine;
using UnityEngine.InputSystem;
namespace Interactions
{
    [RequireComponent(typeof(CharacterState))]
    [RequireComponent(typeof(DirectionalRaycaster))]
public class PlayerInteractionController : MonoBehaviour
{
    private IInteractable _currentTarget;
    private CharacterState _characterState;
    private DirectionalRaycaster _raycaster;
    private Collider _lastHitCollider;

    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        _raycaster = GetComponent<DirectionalRaycaster>();
    }

    // 인풋 시스템에서 호출할 메서드
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (_currentTarget == null) return;

        var interactor = gameObject;
        if (_currentTarget.CanInteract(interactor))
        {
            _currentTarget.Interact(interactor);
        }
    }

    private void Update()
    {
        UpdateInteractionTarget();
    }

    private void UpdateInteractionTarget()
    {
        Vector3 lookDir = _characterState.FacingDir;
        if (!_raycaster.TryRaycast(transform.position, lookDir, out RaycastHit hit))
        {
            _lastHitCollider = null;
            if (_currentTarget != null) ClearTarget();
            return;
        }

        if (hit.collider == _lastHitCollider) return;
        _lastHitCollider = hit.collider;

        var interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (Config.DebugMode.PlayerInteraction) Debug.Log("Raycast hit: " + hit.collider.gameObject.name);

        if (interactable != null)
        {
            if (interactable != _currentTarget) ChangeTarget(interactable);
        }
        else if (_currentTarget != null)
        {
            ClearTarget();
        }
    }

    private void ChangeTarget(IInteractable newTarget)
    {
        if (_currentTarget != null)
        {
            _currentTarget.OnUnfocus(gameObject);
        }

        _currentTarget = newTarget;
        _currentTarget.OnFocus(gameObject);
        if(Config.DebugMode.PlayerInteraction) Debug.Log("Focused on: " + (newTarget as MonoBehaviour).gameObject.name);
    }

    private void ClearTarget()
    {
        _currentTarget.OnUnfocus(gameObject);
        _currentTarget = null;
        if(Config.DebugMode.PlayerInteraction) Debug.Log("Unfocused");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (_characterState == null)
            _characterState = GetComponent<CharacterState>();
        if (_raycaster == null)
            _raycaster = GetComponent<DirectionalRaycaster>();
        Gizmos.DrawRay(transform.position, _characterState.FacingDir.normalized * _raycaster.Range);
    }
}
}
