using UnityEngine;

public class AlignmentManager : MonoBehaviour
{
    [SerializeField] private Transform navigationRoot;
    [SerializeField] private Transform alignTarget;

    [SerializeField] private bool alignOnlyOnce = false;
    [SerializeField] private bool keepYFromNavigationRoot = true;
    [SerializeField] private float yawOffset = 0f;

    private bool alreadyAligned = false;

    private void Start()
    {
        AlignNow();
    }

    public void AlignNow()
    {
        if (alignOnlyOnce && alreadyAligned) return;
        if (navigationRoot == null || alignTarget == null) return;

        Vector3 newPos = alignTarget.position;

        if (keepYFromNavigationRoot)
            newPos.y = navigationRoot.position.y;

        float yaw = alignTarget.eulerAngles.y + yawOffset;
        Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

        navigationRoot.SetPositionAndRotation(newPos, newRot);
        alreadyAligned = true;

        Debug.Log("NavigationRoot aligned to AlignTarget.");
    }
}