using UnityEngine;

public class XRRigAligner : MonoBehaviour
{
    [Header("연결 설정")]
    public Transform navigationRoot; // 길 안내 오브젝트 (예: test)
    public Transform rightController; // XR Origin > TrackingSpace > RightHand Controller

    void Start()
    {
        // 1. 앱이 시작될 때 경로를 자동으로 숨깁니다.
        if (navigationRoot != null)
        {
            navigationRoot.gameObject.SetActive(false);
            Debug.Log("시작: 경로를 숨겼습니다. A 버튼을 눌러 정렬하세요.");
        }
    }

    void Update()
    {
        // 방법 1: 오큘러스 전용 A 버튼 (OVR Manager 필요)
        bool ovrAButton = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);

        // 방법 2: 유니티 범용 마우스 클릭/스페이스바 (PC 링크 테스트용)
        bool pcTest = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);

        // 둘 중 하나라도 눌리면 실행
        if (ovrAButton || pcTest)
        {
            AlignPath();
        }
    }

    public void AlignPath()
    {
        if (navigationRoot == null || rightController == null)
        {
            Debug.LogWarning("NavigationRoot 또는 RightController가 연결되지 않았습니다!");
            return;
        }

        // 3. 경로를 활성화(보이게) 합니다.
        navigationRoot.gameObject.SetActive(true);

        // 4. 경로의 위치를 현재 컨트롤러의 위치로 이동시킵니다.
        navigationRoot.position = rightController.position;

        // 5. 경로의 회전값 중 Y축(좌우 방향)만 컨트롤러와 일치시킵니다.
        // X, Z축까지 맞추면 손목 각도에 따라 경로가 땅으로 꺼질 수 있어 Y만 맞춥니다.
        float targetYaw = rightController.eulerAngles.y;
        navigationRoot.rotation = Quaternion.Euler(0, targetYaw, 0);

        Debug.Log($"경로 정렬 완료: {navigationRoot.name}이(가) 컨트롤러 위치로 이동했습니다.");
    }
}