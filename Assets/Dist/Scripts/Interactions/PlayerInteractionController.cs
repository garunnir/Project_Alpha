using UnityEngine;
using UnityEngine.InputSystem;
namespace Interactions
{
    [RequireComponent(typeof(CharacterState))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableMask;

    private IInteractable currentTarget;
    private CharacterState _characterState;
    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
    }

    // 인풋 시스템에서 호출할 메서드
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (currentTarget == null) return;

        var interactor = gameObject;
        if (currentTarget.CanInteract(interactor))
        {
            currentTarget.Interact(interactor);
        }
    }

    private void Update()
    {
        UpdateInteractionTarget();
    }

    private void UpdateInteractionTarget()
    {
        Vector3 LookDir = _characterState.FacingDir;
        Ray ray = new Ray(transform.position,LookDir);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableMask))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
    Debug.Log("Raycast hit: " + hit.collider.gameObject.name);
            if (interactable != null)
            {
                // 타겟 변경 감지
                if (interactable != currentTarget)
                {
                    ChangeTarget(interactable);
                }
                return;
            }
        }

        // 더 이상 인터랙트 가능한 걸 안 보고 있을 때
        if (currentTarget != null)
        {
            ClearTarget();
        }
    }

    private void ChangeTarget(IInteractable newTarget)
    {
        if (currentTarget != null)
        {
            currentTarget.OnUnfocus(gameObject);
        }

        currentTarget = newTarget;
        currentTarget.OnFocus(gameObject);
        Debug.Log("Focused on: " + (newTarget as MonoBehaviour).gameObject.name);
        // 여기서 UI에 displayName, hintText 띄우는 것도 가능
    }

    private void ClearTarget()
    {
        currentTarget.OnUnfocus(gameObject);
        currentTarget = null;
        Debug.Log ("Unfocused");
        // UI 숨기기 등
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if(_characterState==null)
            _characterState = GetComponent<CharacterState>();
        Gizmos.DrawRay(transform.position, _characterState.FacingDir * interactDistance);
    }
}
}