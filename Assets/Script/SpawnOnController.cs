using UnityEngine;

public class SpawnOnController : MonoBehaviour
{
    [Header("불러올 오브젝트 (test)")]
    public GameObject targetObject;

    [Header("오른쪽 컨트롤러 위치 참조")]
    public Transform controllerTransform;

    [Header("컨트롤러 앞쪽으로 띄울 거리")]
    public float spawnDistance = 0.5f;

    private void Start()
    {
        // 시작할 때는 바로 보이지 않도록 비활성화
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 오른쪽 컨트롤러(RTouch)의 검지 트리거 버튼(PrimaryIndexTrigger)을 누르는 순간 감지!
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            if (targetObject != null && controllerTransform != null)
            {
                // 오브젝트 활성화
                targetObject.SetActive(true);
                
                // 컨트롤러의 위치에서 앞쪽(forward)으로 spawnDistance만큼 떨어진 곳에 배치
                targetObject.transform.position = controllerTransform.position + (controllerTransform.forward * spawnDistance);
                
                // 컨트롤러가 바라보는 방향과 똑같이 회전
                targetObject.transform.rotation = controllerTransform.rotation;
            }
        }
    }
}
