using UnityEngine;

public sealed class HeadLockedHudFollower : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private float distance = 1.25f;
    [SerializeField] private float verticalOffset = -0.08f;
    [SerializeField] private float smoothSpeed = 12f;

    public void SetTarget(Transform cameraTransform, float targetDistance, float targetVerticalOffset, float targetSmoothSpeed)
    {
        targetCamera = cameraTransform;
        distance = targetDistance;
        verticalOffset = targetVerticalOffset;
        smoothSpeed = targetSmoothSpeed;
    }

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            if (Camera.main == null)
                return;
            targetCamera = Camera.main.transform;
        }

        var targetPosition = targetCamera.position +
                             targetCamera.forward * Mathf.Max(0.2f, distance) +
                             Vector3.up * verticalOffset;
        var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothSpeed) * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);

        var toHud = transform.position - targetCamera.position;
        if (toHud.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toHud.normalized, Vector3.up), t);
    }
}
