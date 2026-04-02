# XR Light Direction — 프로젝트 진행 정리

## 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | XR_Light_direction |
| 목표 | Meta Quest 3에서 QR 마커 기반 실내 AR 길안내 |
| 구현 장소 | 학교 건물 내부 (교실 → 화장실/출구 등) |
| 개발 인원 | 1인 |
| 개발 기간 | 3일 목표 |

---

## ✅ 완료된 세팅

### Unity 환경
- [x] Unity **2022.3.60f1 LTS** 설치
- [x] Android Build Support, Android SDK & NDK Tools, OpenJDK 모듈 설치
- [x] 프로젝트 생성 — **3D (Built-In Render Pipeline)**
- [x] `.gitignore` 생성

### Meta XR SDK
- [x] **Meta XR All-in-One SDK (v85.0.0)** 임포트
- [x] Graphics API → **Direct3D11** 전환
- [x] Project Setup Tool Required fixes 전부 해결

### OpenXR 설정
- [x] OpenXR Plugin 설치
- [x] XR Plugin Management 설치
- [x] Android 탭 OpenXR Feature Groups:
  - [x] Meta XR ✅
  - [x] Hand Interaction Poses ✅
  - [x] Meta Quest Support ✅
  - [x] Meta XR Feature ✅
  - [x] Meta XR Foveation ✅
- [x] Enabled Interaction Profiles → **Oculus Touch Controller Profile** 추가

### Player Settings (Android)
- [x] Package Name: `com.yuykim.xrlightdirection`
- [x] Minimum API Level: **Android 10 (API 29)**
- [x] Scripting Backend: **IL2CPP**
- [x] Target Architectures: **ARM64** 전용

### Build Settings
- [x] Platform: **Android** (Switch Platform 완료)
- [x] Scenes In Build: `Scenes/MAIN` 추가
- [x] Development Build: ON
- [x] Run Device: **Oculus Quest 3** 인식 확인 ✅

---

## 📋 앞으로의 계획

### Day 1 — 첫 빌드 & Passthrough 구현

- [ ] **Build And Run** 으로 빈 씬 빌드 성공 확인
- [ ] `OVRCameraRig` Prefab 씬에 배치
- [ ] Passthrough 활성화 (현실 배경이 보이는지 확인)
- [ ] 가상 오브젝트(큐브) 현실 공간에 띄우기 테스트

### Day 2 — QR 마커 인식 + 경로 렌더링

- [ ] AR Foundation + `ARTrackedImageManager` 세팅
- [ ] 마커 이미지 1장 등록 및 인식 테스트
- [ ] 마커 기준 웨이포인트 좌표 3~5개 하드코딩
- [ ] `LineRenderer`로 바닥 경로선 표시
- [ ] 화살표 오브젝트 방향 배치

### Day 3 — 완성 & 안정화

- [ ] QR 인식 전/후 상태 분기 UI ("QR을 비춰주세요")
- [ ] 경로선 펄스 애니메이션 (선택)
- [ ] 최종 빌드 확인 + 크래시 수정
- [ ] 데모 시나리오 리허설

---

## 기술 스택 요약

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 2022.3.60f1 LTS |
| 언어 | C# |
| XR SDK | Meta XR All-in-One SDK v85 |
| 렌더 파이프라인 | Built-In Render Pipeline |
| 빌드 타겟 | Android / ARM64 / IL2CPP |
| 마커 인식 | AR Foundation + ARTrackedImageManager |
| 경로 표시 | LineRenderer + 화살표 오브젝트 |
| 기기 연결 | USB (adb) / ADB over Wi-Fi |

---

## 참고 메모

- Passthrough는 `OVRCameraRig` → `OVRPassthroughLayer` 컴포넌트로 활성화
- QR 마커 대신 **이미지 마커** (AR Foundation Reference Image Library) 방식 사용
- Vuforia 사용 금지 — 라이선스 제한 있음
- 무선 빌드: `adb tcpip 5555` → `adb connect [Quest3 IP]:5555`
