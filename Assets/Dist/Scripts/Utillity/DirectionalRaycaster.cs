using UnityEngine;

public class DirectionalRaycaster : MonoBehaviour
{
    [SerializeField] private LayerMask _interactableMask = ~0;

    private Vector3 _lastOrigin;
    private Vector3 _lastDirection;
    private float _lastRadius;
    private float _lastDistance;

    public bool TrySphereCast(
        Vector3 origin,
        Vector3 direction,
        float radius,
        float maxDistance,
        out RaycastHit hit)
    {
        if (direction == Vector3.zero || maxDistance <= 1e-4f)
        {
            hit = default;
            return false;
        }

        _lastOrigin = origin;
        _lastDirection = direction;
        _lastRadius = radius;
        _lastDistance = maxDistance;

        return Physics.SphereCast(
            origin,
            radius,
            direction.normalized,
            out hit,
            maxDistance,
            _interactableMask,
            QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        if (_lastDirection == Vector3.zero || _lastDistance <= 1e-4f) return;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(_lastOrigin, _lastDirection.normalized * _lastDistance);
        Gizmos.DrawWireSphere(_lastOrigin + _lastDirection.normalized * _lastDistance, _lastRadius);
    }
}
