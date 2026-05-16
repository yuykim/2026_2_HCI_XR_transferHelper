using UnityEngine;

public class ControllerPathAligner : MonoBehaviour
{
    [Header("설정")]
    public Transform navigationRoot; // 길 안내 선의 부모 객체
    public OVRInput.Controller controller = OVRInput.Controller.RTouch; // 오른쪽 컨트롤러 기준
    public OVRInput.Button alignButton = OVRInput.Button.One; // 'A' 버튼

    void Update()
    {
        // 컨트롤러의 특정 버튼(A 버튼)을 누르는 순간
        if (OVRInput.GetDown(alignButton, controller))
        {
            AlignPathToController();
        }
    }

    void AlignPathToController()
    {
        // 1. 현재 컨트롤러의 위치와 방향 가져오기
        Vector3 controllerPos = OVRInput.GetLocalControllerPosition(controller);
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(controller);

        // 2. 경로의 위치를 컨트롤러 위치로 이동
        navigationRoot.position = controllerPos;

        // 3. 경로의 방향을 컨트롤러가 보는 방향(Y축 회전만)으로 일치
        float yaw = controllerRot.eulerAngles.y;
        navigationRoot.rotation = Quaternion.Euler(0, yaw, 0);

        Debug.Log("경로가 현재 컨트롤러 위치로 정렬되었습니다!");
    }
}