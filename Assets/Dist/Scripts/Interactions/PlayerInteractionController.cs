using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Interactions
{
    [RequireComponent(typeof(CharacterState))]
    [RequireComponent(typeof(DirectionalRaycaster))]
    [RequireComponent(typeof(PlayerAimController))]
    public class PlayerInteractionController : MonoBehaviour
    {
        private IInteractable _currentTarget;
        private CharacterState _characterState;
        private DirectionalRaycaster _raycaster;
        private PlayerAimController _aimController;
        private Collider _lastHitCollider;
        private InputActions _interactAction;
        private readonly Dictionary<Collider, IInteractable> _interactableCache = new();

        private void Awake()
        {
            _characterState = GetComponent<CharacterState>();
            _raycaster = GetComponent<DirectionalRaycaster>();
            _aimController = GetComponent<PlayerAimController>();
        }

        private void Start()
        {
            _interactAction = InputManager.Instance.Actions;
            _interactAction.Player.Interaction.performed += OnInteract;
        }

        private void OnDestroy()
        {
            if (_interactAction != null)
                _interactAction.Player.Interaction.performed -= OnInteract;
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            if (_currentTarget == null) return;

            var interactor = gameObject;
            if (_currentTarget.CanInteract(interactor))
                _currentTarget.Interact(interactor);
        }

        private void LateUpdate()
        {
            UpdateInteractionTarget();
        }

        private void UpdateInteractionTarget()
        {
            if (!_characterState.HasInteractionFocus)
            {
                _lastHitCollider = null;
                if (_currentTarget != null) ClearTarget();
                return;
            }

            Vector3 origin = transform.position + Vector3.up * _aimController.CastOriginYOffset;
            Vector3 direction = _characterState.InteractionDir;
            float maxDistance = _characterState.InteractionReach;

            if (!_raycaster.TrySphereCast(
                    origin,
                    direction,
                    _aimController.SphereRadius,
                    maxDistance,
                    out RaycastHit hit))
            {
                _lastHitCollider = null;
                if (_currentTarget != null) ClearTarget();
                return;
            }

            if (hit.collider == _lastHitCollider) return;
            _lastHitCollider = hit.collider;

            if (!_interactableCache.TryGetValue(hit.collider, out var interactable))
            {
                interactable = hit.collider.GetComponentInParent<IInteractable>();
                _interactableCache[hit.collider] = interactable;
            }

            if (Config.DebugMode.PlayerInteraction)
                Debug.Log("Interaction SphereCast hit: " + hit.collider.gameObject.name);

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
                _currentTarget.OnUnfocus(gameObject);

            _currentTarget = newTarget;
            _currentTarget.OnFocus(gameObject);

            if (Config.DebugMode.PlayerInteraction)
                Debug.Log("Focused on: " + (newTarget as MonoBehaviour).gameObject.name);
        }

        private void ClearTarget()
        {
            _currentTarget.OnUnfocus(gameObject);
            _currentTarget = null;

            if (Config.DebugMode.PlayerInteraction)
                Debug.Log("Unfocused");
        }
    }
}
