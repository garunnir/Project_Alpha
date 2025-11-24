using UnityEngine;
using UnityEngine.InputSystem;
namespace Interactions
{
    [RequireComponent(typeof(CharacterState))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _interactDistance = 3f;
    [SerializeField] private LayerMask _interactableMask;

    private IInteractable _currentTarget;
    private CharacterState _characterState;
    private Vector3 _lastLookDir;
    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
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
        
        Vector3 LookDir = _characterState.FacingDir.normalized;
        if(LookDir==Vector3.zero) return;

        Ray ray = new Ray(transform.position,LookDir);

        if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableMask))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
    Debug.Log("Raycast hit: " + hit.collider.gameObject.name);
            if (interactable != null)
            {
                // 타겟 변경 감지
                if (interactable != _currentTarget)
                {
                    ChangeTarget(interactable);
                }
                return;
            }
        }

        // 더 이상 인터랙트 가능한 걸 안 보고 있을 때
        if (_currentTarget != null)
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
        Debug.Log("Focused on: " + (newTarget as MonoBehaviour).gameObject.name);
        // 여기서 UI에 displayName, hintText 띄우는 것도 가능
    }

    private void ClearTarget()
    {
        _currentTarget.OnUnfocus(gameObject);
        _currentTarget = null;
        Debug.Log ("Unfocused");
        // UI 숨기기 등
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if(_characterState==null)
            _characterState = GetComponent<CharacterState>();
        Gizmos.DrawRay(transform.position, _characterState.FacingDir.normalized * _interactDistance);
    }
}
}