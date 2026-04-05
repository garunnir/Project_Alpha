using UnityEngine;

public class DirectionalRaycaster : MonoBehaviour
{
    [SerializeField] private float _range = 3f;
    [SerializeField] private LayerMask _mask;

    public float Range => _range;

    public bool TryRaycast(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        if (direction == Vector3.zero)
        {
            hit = default;
            return false;
        }
        return Physics.Raycast(new Ray(origin, direction.normalized), out hit, _range, _mask);
    }
}
