# XR Transfer Helper

XR Transfer Helper는 GPS가 잘 동작하지 않는 실내 공간에서 QR 코드를 기준으로 현재 위치를 파악하고, 목적지까지 XR 경로 안내를 제공하는 것을 목표로 하는 Unity 프로젝트입니다.

현재 단계에서는 QR 코드 인식 전 단계의 프로토타입으로, 앱 시작 후 `Route A / B / C` 중 하나를 선택하면 현재 VR 기기가 바라보는 방향을 기준으로 루트가 생성됩니다. 사용자는 루트를 따라 이동하면서 남은 진행률을 확인할 수 있고, 루트를 벗어나면 경고 문구를, 목적지에 도착하면 도착 문구를 볼 수 있습니다.

---

## 현재 파일 구조

```text
XR_TransferHelper/
├─ Assets/
│  ├─ Editor/
│  │  └─ NavigationPrototypeBuilder.cs
│  ├─ Script/
│  │  ├─ CanvasInstallOvrRaycaster.cs
│  │  ├─ GazeRouteSelector.cs
│  │  ├─ HandUiPinchClicker.cs
│  │  ├─ HeadLockedHudFollower.cs
│  │  ├─ PalmHudFollower.cs
│  │  ├─ RouteHudMirror.cs
│  │  ├─ RouteNavigationController.cs
│  │  ├─ RoutePathRenderer.cs
│  │  └─ VrUiInputModuleBootstrap.cs
│  ├─ Scenes/
│  │  ├─ MAIN.unity
│  │  ├─ YUYKIM2.unity
│  │  └─ YoungKong.unity
│  ├─ Material/
│  │  └─ RouteMat.mat
│  ├─ Resources/
│  ├─ XR/
│  ├─ XRI/
│  ├─ Oculus/
│  ├─ Plugins/
│  └─ TextMesh Pro/
├─ Packages/
│  ├─ manifest.json
│  └─ packages-lock.json
├─ ProjectSettings/
├─ doc/
│  ├─ XR_LightDirection_Progress.md
│  └─ xr_transferhelper_issues_and_plan.md
└─ instruction.md
```

---

## 주요 폴더 역할

| 경로 | 역할 |
| --- | --- |
| `Assets/Editor` | Unity 에디터에서 실행하는 자동 생성 도구가 들어 있음 |
| `Assets/Script` | 런타임에서 동작하는 네비게이션, HUD, 입력 처리 스크립트가 들어 있음 |
| `Assets/Scenes` | 테스트 및 실제 실행용 Unity 씬 |
| `Assets/Material` | 루트 표시 등에 사용하는 머티리얼 |
| `Assets/Resources` | Meta/Oculus 관련 설정 에셋과 Input Actions 에셋 |
| `Assets/XR`, `Assets/XRI` | OpenXR, XR Interaction Toolkit 관련 설정 |
| `Assets/Oculus` | Meta XR SDK 관련 프로젝트 설정 |
| `Assets/Plugins/Android` | Android 빌드용 Manifest 설정 |
| `Packages` | Unity 패키지 의존성 정보 |
| `ProjectSettings` | Unity 프로젝트 전체 설정 |
| `doc` | 진행 상황, 문제점, 계획 정리 문서 |

---

## 주요 씬

| 씬 | 역할 |
| --- | --- |
| `Assets/Scenes/MAIN.unity` | 기본 또는 원본 테스트 씬으로 사용하는 씬 |
| `Assets/Scenes/YUYKIM2.unity` | `NavigationPrototypeBuilder`가 생성하는 VR UI 네비게이션 프로토타입 씬 |
| `Assets/Scenes/YoungKong.unity` | 별도 테스트 또는 작업용 씬 |

---

## 스크립트 역할 정리

### `Assets/Editor/NavigationPrototypeBuilder.cs`

Unity 에디터 메뉴에서 네비게이션 프로토타입을 자동 생성하는 스크립트입니다.

- `Tools/XR Transfer Helper/Create Navigation Prototype` 메뉴 제공
- `Tools/XR Transfer Helper/Create YUYKIM2 VR UI Scene` 메뉴 제공
- `NavigationRoot`, `RoutePoints`, `LightRoute`, `NavigationHUD`, `PalmHUD` 자동 생성
- Route A/B/C 테스트 경로 포인트 생성
- `LineRenderer`와 `RoutePathRenderer` 설정
- 목적지 선택 버튼, 남은 거리, 진행률, 경고, 도착 UI 생성
- 버튼 클릭 이벤트를 `RouteNavigationController`에 연결
- `YUYKIM2.unity` 씬 저장 및 Build Settings 등록

### `Assets/Script/RouteNavigationController.cs`

네비게이션의 핵심 동작을 담당하는 메인 컨트롤러입니다.

- Route A/B/C 선택 처리
- 선택된 루트를 현재 사용자 시야 방향 기준으로 정렬
- 사용자의 현재 위치와 루트 사이 거리 계산
- 루트 이탈 여부 판단
- 남은 거리, ETA, 진행률 `%` 갱신
- 방향 화살표 회전 처리
- 이탈 경고 UI 표시
- 도착 상태 처리
- 손 추적, 컨트롤러 트리거, 레이 기반 목적지 선택 보조 처리

### `Assets/Script/RoutePathRenderer.cs`

루트 포인트들을 `LineRenderer`로 연결해 실제 경로 선을 화면에 표시하는 스크립트입니다.

- 경로 포인트 배열을 받아 선으로 렌더링
- 경로 포인트가 바뀌면 `Refresh()`로 선 갱신
- `LineRenderer` 기본 폭, 코너, 캡 설정 보정

### `Assets/Script/HeadLockedHudFollower.cs`

메인 HUD가 사용자 시야 앞을 따라오게 하는 스크립트입니다.

- 카메라 앞 일정 거리로 HUD 위치 이동
- 사용자의 시야 방향에 맞춰 HUD 회전
- 부드럽게 따라오도록 보간 처리

### `Assets/Script/PalmHudFollower.cs`

손바닥 HUD가 손 위치를 따라오게 하는 스크립트입니다.

- 오른손 또는 왼손 앵커 탐색
- 손 추적 상태 확인
- 손이 보이면 손 근처에 Palm HUD 표시
- 손 추적이 끊기면 HUD 숨김 처리

### `Assets/Script/RouteHudMirror.cs`

메인 HUD의 정보를 Palm HUD로 복사하는 스크립트입니다.

- 남은 거리 텍스트 복사
- 상태 메시지 복사
- ETA 텍스트 복사

### `Assets/Script/CanvasInstallOvrRaycaster.cs`

World Space Canvas에서 Quest/XR UI 입력이 동작하도록 Raycaster를 자동 설정하는 스크립트입니다.

- Android Quest 빌드에서는 `OVRRaycaster` 우선 사용
- 에디터 또는 일반 XR 환경에서는 `TrackedDeviceGraphicRaycaster` 우선 사용
- 기본 `GraphicRaycaster`와 XR/OVR Raycaster 충돌을 줄이도록 정리

### `Assets/Script/VrUiInputModuleBootstrap.cs`

`EventSystem`의 입력 모듈을 XR/Quest 환경에 맞게 자동 설정하는 스크립트입니다.

- Quest Android 빌드에서는 `OVRInputModule` 우선 사용
- 에디터 또는 일반 XR 환경에서는 `XRUIInputModule` 우선 사용
- 기존 입력 모듈을 정리하고 필요한 모듈을 자동 추가

### `Assets/Script/HandUiPinchClicker.cs`

손가락 pinch 입력으로 UI 버튼을 클릭하기 위한 보조 스크립트입니다.

- OVRHand 기반 손 추적 정보 탐색
- 검지와 엄지 거리로 pinch 여부 판단
- pinch 시 버튼 클릭 이벤트 실행
- 손 추적이 불안정한 경우 컨트롤러 입력 또는 gaze dwell 방식으로 보조 선택
- 현재는 메인 `RouteNavigationController`에서 직접 선택 처리를 하므로, 이전/보조 입력 방식에 가까움

### `Assets/Script/GazeRouteSelector.cs`

시선 dwell 방식으로 Route A/B/C를 선택하기 위한 보조 스크립트입니다.

- 화면 중앙에 가까운 버튼 탐색
- 일정 시간 바라보면 해당 버튼 선택
- 버튼 강조 색상과 디버그 텍스트 표시
- 현재는 `RouteNavigationController`에서 제거하도록 처리되어 있어, 이전 프로토타입 또는 대체 입력 방식에 가까움

---

## 현재 구현 상태 요약

- 앱 시작 후 Route A/B/C 중 하나를 선택할 수 있음.
- 현재 VR 기기가 바라보는 방향을 기준으로 루트가 생성됨.
- 루트를 벗어나면 빨간색 경고 문구가 표시됨.
- 루트를 따라 이동하면 목적지까지 남은 진행률을 `%`로 확인할 수 있음.
- 목적지에 도착하면 초록색 도착 안내 문구가 표시됨.

---

## 앞으로 해야 할 일

- 루트 생성 기준을 현재 기기 방향이 아니라 QR 코드 기준 좌표로 변경
- 전체 UI 디자인 개선
- 인식한 QR 코드마다 갈 수 있는 목적지 목록을 다르게 표시
- 루트 이탈 시 경고음 재생
- 목적지 도착 시 도착 알림음 재생

---

## 최종 목표

XR Transfer Helper는 일반 네비게이션처럼 단순히 목적지를 입력해 루트를 생성하는 앱이 아니라, GPS가 제대로 작동하지 않는 실내 공간에서 QR 코드를 기준으로 현재 위치를 파악하고 목적지까지 안내하는 XR 실내 길 안내 시스템을 목표로 합니다.

