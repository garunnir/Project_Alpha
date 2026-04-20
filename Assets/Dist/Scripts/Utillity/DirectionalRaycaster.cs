using UnityEngine;

public class DirectionalRaycaster : MonoBehaviour
{
    [SerializeField] private float _range = 3f;
    [SerializeField] private LayerMask _mask;

    public float Range => _range;

    private Vector3 _lastOrigin;
    private Vector3 _lastDirection;

    public bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        if (direction == Vector3.zero)
        {
            hit = default;
            return false;
        }
        _lastOrigin = origin;
        _lastDirection = direction;
        bool isRayCasted = Physics.Raycast(new Ray(origin, direction.normalized), out hit, _range, _mask);
        return isRayCasted; 
    }

    private void OnDrawGizmosSelected()
    {        
        if (_lastDirection == Vector3.zero) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawRay(_lastOrigin, _lastDirection.normalized * _range);
    }
}
