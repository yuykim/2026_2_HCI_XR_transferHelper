using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class QRWallAligner : MonoBehaviour
{
    [Header("필수 연결 요소")]
    public ARTrackedImageManager imageManager;
    public Transform navigationRoot; // 경로들의 부모 객체

    [Header("벽면 부착 보정 값")]
    [Tooltip("QR 코드가 바닥에서 떨어진 높이(미터)")]
    public float qrHeightFromFloor = 1.5f; 
    [Tooltip("복도가 뻗은 방향 (정면 직진=0, 우회전=90, 좌회전=-90)")]
    public float pathDirectionOffset = 90f; 

    private void OnEnable() => imageManager.trackedImagesChanged += OnImagesChanged;
    private void OnDisable() => imageManager.trackedImagesChanged -= OnImagesChanged;

    private void OnImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // 새로 QR을 인식했거나, 기존 QR을 계속 쳐다보고 있을 때 모두 작동
        foreach (var trackedImage in eventArgs.added) { AlignPath(trackedImage); }
        foreach (var trackedImage in eventArgs.updated) { AlignPath(trackedImage); }
    }

    private void AlignPath(ARTrackedImage trackedImage)
    {
        // 상태가 Tracking일 때만 (카메라에서 놓치면 멈춤)
        if (trackedImage.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking) return;

        // 1. 위치 보정: Root를 QR 위치로 가져오되, 지정한 높이만큼 바닥으로 끌어내림
        Vector3 qrPos = trackedImage.transform.position;
        navigationRoot.position = new Vector3(qrPos.x, qrPos.y - qrHeightFromFloor, qrPos.z);

        // 2. 방향 보정: 벽면 기준 좌우 꺾임 적용 (경로가 공중으로 뜨지 않도록 X, Z 회전은 0으로 고정)
        float qrYaw = trackedImage.transform.eulerAngles.y;
        navigationRoot.rotation = Quaternion.Euler(0, qrYaw + pathDirectionOffset, 0);

        // 3. 어떤 마커를 인식했는지 로그 출력 (이후 분기 처리용)
        Debug.Log($"[QR 인식됨] 마커 이름: {trackedImage.referenceImage.name} / 루트 정렬 완료");
    }
}