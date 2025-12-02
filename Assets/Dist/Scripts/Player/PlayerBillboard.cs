using UnityEngine;

public class PlayerBillboard : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField, Range(0, 15)] float yawThresholdDeg = 1f;
    [SerializeField, Range(0, .05f)] float updateInterval = .02f;
    float nextT, lastYaw;

    void Awake(){ if(!cam) cam = Camera.main; }

    void LateUpdate()
    {
        if(Time.time < nextT) return; nextT = Time.time + updateInterval;

        float yaw = cam.transform.eulerAngles.y;
        if (Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) < yawThresholdDeg) return;
        lastYaw = yaw;

        var toCam = cam.transform.position - transform.position;
        toCam.y = 0f; if (toCam.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
    }
}
