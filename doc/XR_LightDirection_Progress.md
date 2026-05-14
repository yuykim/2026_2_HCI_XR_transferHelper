# XR Light Direction - 프로젝트 진행 정리

## 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | XR_Light_direction / XR Transfer Helper |
| 목표 | Meta Quest 3에서 QR 마커 기반 실내 XR 길 안내 구현 |
| 구현 장소 | 학교 건물 내부, 교실, 화장실, 출구 등 |
| 현재 단계 | 경로 안내 프로토타입 구현 완료, 실제 경로 데이터 제작 및 QR 기준점 보정 시도 단계 |

---

## 현재 상황 요약

- Unity와 Meta Quest 3 기반 XR 개발 환경 세팅은 완료됨.
- Android 빌드 타겟, OpenXR, Meta XR SDK, Quest 3 실행 환경까지 기본 설정이 완료됨.
- 현재는 앱 시작 후 사전에 만들어둔 경로 중 하나를 선택하는 방식으로 동작함.
- 선택한 루트는 현재 VR 기기가 바라보는 방향을 기준으로 생성됨.
- 루트를 따라 이동하면 남은 거리, ETA, 진행률을 HUD에서 확인할 수 있음.
- 루트를 벗어나면 텍스트와 소리로 경고를 제공함.
- 목적지에 도착하면 텍스트와 소리로 도착 안내를 제공함.
- QR 코드를 기준점으로 사용해 위치와 방향을 보정하는 기능은 현재 시도 중이지만, Quest 3 환경에서 안정적으로 고정하는 데 어려움이 있음.
- 이번 주 안에 개발 가능한 기능은 마무리하고, 구현이 어려운 부분은 제한 사유와 대체 방식을 정리할 예정임.

---

## 완료된 세팅

### Unity 환경

- [x] Unity **2022.3.60f1 LTS** 설치
- [x] Android Build Support, Android SDK & NDK Tools, OpenJDK 모듈 설치
- [x] 프로젝트 생성 - **3D (Built-In Render Pipeline)**
- [x] `.gitignore` 생성

### Meta XR SDK

- [x] **Meta XR All-in-One SDK (v85.0.0)** 임포트
- [x] Graphics API를 **Direct3D11**로 전환
- [x] Project Setup Tool Required fixes 해결

### OpenXR 설정

- [x] OpenXR Plugin 설치
- [x] XR Plugin Management 설치
- [x] Android OpenXR Feature Groups 설정
- [x] Meta XR, Hand Interaction Poses, Meta Quest Support, Meta XR Feature, Meta XR Foveation 활성화
- [x] Enabled Interaction Profiles에 **Oculus Touch Controller Profile** 추가

### Player Settings (Android)

- [x] Package Name: `com.yuykim.xrlightdirection`
- [x] Minimum API Level: **Android 10 (API 29)**
- [x] Scripting Backend: **IL2CPP**
- [x] Target Architectures: **ARM64** 전용

### Build Settings

- [x] Platform: **Android** 전환 완료
- [x] Scenes In Build에 `Scenes/MAIN` 추가
- [x] Development Build 활성화
- [x] Run Device에서 **Oculus Quest 3** 인식 확인

---

## 현재 구현 완료된 기능

- [x] 시작 시 사전에 만들어둔 경로 선택 기능
- [x] 사용자가 바라보는 방향을 기준으로 경로 생성 기능
- [x] `LineRenderer` 기반 경로선 표시 기능
- [x] 방향 화살표 오브젝트 표시 기능
- [x] 경로 이동 중 남은 거리와 진행 상황을 퍼센트로 표시하는 기능
- [x] 경로 이탈 시 텍스트 및 소리로 알림 제공
- [x] 목적지 도착 시 텍스트 및 소리로 알림 제공
- [x] Quest/OVR Raycaster 및 XR 입력 모듈 자동 설정

---

## 추가 구현이 필요한 기능

### 실제 사용할 경로 데이터 제작

- [ ] 교수님 오피스 경로
- [ ] 산학협력관 경로
- [ ] 테스트용 간단 경로

---

## 수정 및 개선이 필요한 부분

### UI 부가 정보 추가

- [ ] 현재 위치 안내
- [ ] 목적지 정보
- [ ] 남은 거리 또는 예상 도착 정보
- [ ] 경로 안내 상태 표시

### 경로 기준점 안정화

- [ ] 경로 생성 기준을 보다 안정적으로 고정
- [ ] 사용자가 직접 기준점을 잡을 수 있는 버튼 기능 추가
- [ ] 예: "현재 방향을 기준으로 경로 정렬" 버튼

---

## 추가로 시도해볼 기능

- [ ] QR 코드 기반 위치 및 방향 보정
- [ ] QR 코드를 기준점으로 삼아 경로를 더 안정적으로 고정하는 방식 테스트
- [ ] QR 인식 결과를 현재 경로 시작점 또는 방향 보정값으로 연결하는 방식 검토

---

## 현재 구현이 어려운 부분

### 절대좌표 기반 경로 생성

- Quest 3 환경에서는 실내 공간의 절대좌표를 안정적으로 유지하기 어려움.
- GPS를 사용할 수 없는 실내 환경에서는 사용자의 실제 위치를 고정된 월드 좌표로 정확하게 유지하는 데 한계가 있음.
- 기기 추적 좌표는 실행 시점, 사용자의 초기 위치, 바라보는 방향, 공간 인식 상태에 따라 달라질 수 있음.
- 따라서 현재 방식에서는 건물 전체를 하나의 절대좌표계로 두고 경로를 생성하는 방식은 구현이 제한적임.
- 대안으로 사용자의 초기 위치/방향을 기준으로 경로를 정렬하거나, QR 코드를 기준점으로 삼아 위치와 방향을 보정하는 방식이 더 현실적임.

---

## 마무리 방향

- 이번 주 안에 현재 구현된 경로 안내 프로토타입을 데모 가능한 수준으로 정리함.
- 실제 사용할 경로 데이터 3종을 제작하고, UI에 필요한 안내 정보를 추가함.
- QR 코드 기반 기준점 보정은 계속 시도하되, 안정적으로 구현되지 않을 경우 제한 사유를 문서에 남김.
- 최종 발표 또는 제출 시에는 구현 완료 기능, 시도 중인 기능, 구현이 제한된 기능을 구분해서 설명함.

---

## 기술 스택 요약

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 2022.3.60f1 LTS |
| 언어 | C# |
| XR SDK | Meta XR All-in-One SDK v85 |
| 렌더 파이프라인 | Built-In Render Pipeline |
| 빌드 타겟 | Android / ARM64 / IL2CPP |
| 마커 인식 | QR 코드 기반 위치/방향 보정 시도 중 |
| 경로 표시 | LineRenderer + 방향 화살표 오브젝트 |
| UI 표시 | World Space Canvas, Main HUD, Palm HUD |
| 알림 방식 | 텍스트 안내 + 경고음/도착 알림음 |
| 기기 연결 | USB adb / ADB over Wi-Fi |

---

## 참고 메모

- 현재 프로토타입의 핵심 씬은 `Assets/Scenes/YUYKIM2.unity`임.
- `NavigationPrototypeBuilder`는 네비게이션 테스트 씬과 기본 오브젝트를 자동 생성하는 에디터 도구임.
- 현재 경로 생성 기준은 QR 마커가 아니라 사용자가 바라보는 방향임.
- 최종 목표는 QR 코드를 기준으로 실내 현재 위치를 파악하고, 목적지까지 XR 경로 안내를 제공하는 것임.
- 무선 빌드가 필요하면 `adb tcpip 5555` 실행 후 `adb connect [Quest3 IP]:5555`로 연결.
