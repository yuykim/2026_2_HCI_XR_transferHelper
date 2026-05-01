using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RouteNavigationController : MonoBehaviour
{
    [Header("Route")]
    [SerializeField] private Transform userTransform;
    [SerializeField] private Transform[] routePoints;
    [SerializeField] private Transform[] routePointsA;
    [SerializeField] private Transform[] routePointsB;
    [SerializeField] private Transform[] routePointsC;
    [SerializeField] private RoutePathRenderer routePathRenderer;
    [SerializeField] private bool alignRoutesToStartupView = true;
    [SerializeField] private Transform routePointsRoot;
    [SerializeField] private float pointReachRadius = 0.35f;
    [SerializeField] private float routeDeviationThreshold = 2.5f;
    [SerializeField] private float walkingSpeedMetersPerSecond = 1.2f;

    [Header("Pinch Route Selection")]
    [SerializeField] private bool usePinchRouteSelection = true;
    [SerializeField] private float selectionStartupGraceSeconds = 1.0f;
    [SerializeField] private float pinchDownDistanceMeters = 0.028f;
    [SerializeField] private float pinchUpDistanceMeters = 0.04f;
    [SerializeField] private float pinchCooldownSeconds = 0.35f;
    [SerializeField] private bool preferHandRay = true;
    [SerializeField] private bool allowGazeRay = false;
    [SerializeField] private bool allowControllerTriggerFallback = true;
    [SerializeField] private bool allowControllerRay = true;
    [SerializeField] private float maxSelectionRayDistance = 4.0f;
    [SerializeField] private float selectionHitPadding = 220f;
    [SerializeField] private float gazeScreenFallbackMaxPixels = 520f;
    [SerializeField] private bool showSelectionRay = true;
    [SerializeField] private float selectionRayStartOffset = 0.08f;
    [SerializeField] private bool previewRoutesBeforeSelection = false;
    [SerializeField] private float routePreviewIntervalSeconds = 2.0f;
    [SerializeField] private bool followSelectionPanelToUser = false;
    [SerializeField] private float hudFollowDistance = 1.25f;
    [SerializeField] private float hudFollowVerticalOffset = -0.08f;
    [SerializeField] private float hudFollowSmooth = 12f;
    [SerializeField] private bool showEyeLevelOffRouteWarning = true;
    [SerializeField] private float eyeWarningDistance = 0.85f;
    [SerializeField] private float eyeWarningHeightOffset = -0.02f;

    [Header("UI")]
    [SerializeField] private GameObject destinationSelectPanel;
    [SerializeField] private Button routeAButton;
    [SerializeField] private Button routeBButton;
    [SerializeField] private Button routeCButton;
    [SerializeField] private TMP_Text remainingDistanceText;
    [SerializeField] private TMP_Text etaText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text arrivalText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text directionArrowText;
    [SerializeField] private RectTransform directionArrowRect;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private CanvasGroup warningGroup;
    [SerializeField] private CanvasGroup arrivalGroup;

    private int _targetIndex = 1;
    private bool _navigationStarted;
    private bool _arrived;
    private float _totalRouteDistance;
    private float _selectionEnabledTime;
    private float _nextPreviewTime;
    private int _previewIndex = -1;
    private RouteChoice _currentPinchTarget = RouteChoice.None;
    private string _currentSelectionSource = "Ray";
    private string _lastPinchStatus = "Waiting for hand pinch.";
    private bool _hasSelectionRay;
    private Ray _currentSelectionRay;
    private float _currentSelectionRayLength;
    private LineRenderer _selectionRayRenderer;
    private bool _wasPinching;
    private float _lastPinchSelectionTime = -10f;
    private Component _leftHand;
    private Component _rightHand;
    private Transform _leftAnchor;
    private Transform _rightAnchor;
    private Transform _leftIndexTip;
    private Transform _leftThumbTip;
    private Transform _rightIndexTip;
    private Transform _rightThumbTip;
    private Transform _leftPointerPose;
    private Transform _rightPointerPose;
    private Transform _leftControllerAnchor;
    private Transform _rightControllerAnchor;
    private PropertyInfo _isTrackedProperty;
    private PropertyInfo _isHighConfidenceProperty;
    private PropertyInfo _pointerPoseProperty;
    private FieldInfo _pointerPoseField;
    private MethodInfo _getFingerIsPinchingMethod;
    private MethodInfo _getFingerPinchStrengthMethod;
    private object _indexFingerEnumValue;
    private MethodInfo _ovrInputGetButtonMethod;
    private object _ovrPrimaryIndexTriggerButton;
    private object _ovrSecondaryIndexTriggerButton;
    private object _ovrPrimaryHandTriggerButton;
    private object _ovrSecondaryHandTriggerButton;
    private object _ovrControllerMask;
    private TextMeshPro _eyeWarningText;
    private bool _routesAlignedToStartupView;

    private enum RouteChoice
    {
        None,
        A,
        B,
        C
    }

    private void Reset()
    {
        userTransform = Camera.main != null ? Camera.main.transform : null;
        routePathRenderer = GetComponentInChildren<RoutePathRenderer>();
    }

    private void Awake()
    {
        if (userTransform == null && Camera.main != null)
            userTransform = Camera.main.transform;

        if ((routePoints == null || routePoints.Length == 0) && routePathRenderer != null)
            routePoints = routePathRenderer.RoutePoints;

        if ((routePointsA == null || routePointsA.Length == 0) && routePoints != null && routePoints.Length > 0)
            routePointsA = routePoints;
    }

    private void Start()
    {
        ConfigurePinchRouteSelection();
        SetRoutePreview(null);
        SetWarning(false);
        SetArrived(false);
        if (destinationSelectPanel != null)
            destinationSelectPanel.SetActive(true);
        SetNavigationStatsVisible(false);
        SetStatus("Use hand/controller ray on A/B/C, then pinch or trigger.");
        UpdateRemainingDistanceText(0f);
        UpdateProgress(0f);
        SetDirectionArrow(false, false, 0f);
        SetSelectionRayVisible(false);
        ResetRouteSelection();
    }

    private void ConfigurePinchRouteSelection()
    {
        EnsureRouteButtons();

        var hudObject = GameObject.Find("NavigationHUD");
        if (hudObject == null)
            return;

        followSelectionPanelToUser = false;
        allowGazeRay = false;
        EnsureHeadLockedHudFollower(hudObject);
        NormalizeHudForReadability(hudObject);

        var oldClicker = hudObject.GetComponent<HandUiPinchClicker>();
        if (oldClicker != null)
            Destroy(oldClicker);

        var gazeSelector = hudObject.GetComponent<GazeRouteSelector>();
        if (gazeSelector != null)
            Destroy(gazeSelector);
    }

    private void AlignRoutesToStartupViewIfNeeded()
    {
        if (!alignRoutesToStartupView || _routesAlignedToStartupView)
            return;

        if (userTransform == null && Camera.main != null)
            userTransform = Camera.main.transform;
        if (userTransform == null)
            return;

        ResolveRoutePointsRootIfNeeded();
        if (routePointsRoot == null)
            return;

        var forward = userTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return;

        var targetPosition = routePointsRoot.position;
        targetPosition.x = userTransform.position.x;
        targetPosition.z = userTransform.position.z;
        routePointsRoot.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(forward.normalized, Vector3.up));
        _routesAlignedToStartupView = true;

        if (routePathRenderer != null)
            routePathRenderer.Refresh();
    }

    private void ResolveRoutePointsRootIfNeeded()
    {
        if (routePointsRoot != null)
            return;

        routePointsRoot = FindRoutePointsRoot(routePointsA);
        if (routePointsRoot == null)
            routePointsRoot = FindRoutePointsRoot(routePointsB);
        if (routePointsRoot == null)
            routePointsRoot = FindRoutePointsRoot(routePointsC);
    }

    private static Transform FindRoutePointsRoot(Transform[] points)
    {
        if (points == null)
            return null;

        foreach (var point in points)
        {
            if (point == null || point.parent == null)
                continue;

            var group = point.parent;
            if (group.parent != null)
                return group.parent;

            return group;
        }

        return null;
    }

    private void EnsureHeadLockedHudFollower(GameObject hudObject)
    {
        var follower = hudObject.GetComponent<HeadLockedHudFollower>();
        if (follower == null)
            follower = hudObject.AddComponent<HeadLockedHudFollower>();

        if (userTransform == null && Camera.main != null)
            userTransform = Camera.main.transform;

        follower.SetTarget(userTransform, hudFollowDistance, hudFollowVerticalOffset, hudFollowSmooth);
    }

    private void NormalizeHudForReadability(GameObject hudObject)
    {
        var canvasRect = hudObject.transform as RectTransform;
        if (canvasRect != null)
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

        if (destinationSelectPanel != null)
        {
            var rect = destinationSelectPanel.transform as RectTransform;
            SetRect(rect, new Vector2(0f, 110f), new Vector2(900f, 250f));

            var title = destinationSelectPanel.transform.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.fontSize = 38f;
                title.fontStyle = FontStyles.Bold;
                title.alignment = TextAlignmentOptions.Center;
                SetRect(title.rectTransform, new Vector2(0f, 72f), new Vector2(840f, 58f));
            }
        }

        SetButtonLayout(routeAButton, new Vector2(-280f, -52f), new Vector2(250f, 82f), 36f);
        SetButtonLayout(routeBButton, new Vector2(0f, -52f), new Vector2(250f, 82f), 36f);
        SetButtonLayout(routeCButton, new Vector2(280f, -52f), new Vector2(250f, 82f), 36f);

        if (remainingDistanceText != null)
        {
            var panel = remainingDistanceText.transform.parent as RectTransform;
            SetRect(panel, new Vector2(0f, 303f), new Vector2(620f, 82f));
            remainingDistanceText.fontSize = 36f;
            remainingDistanceText.fontStyle = FontStyles.Bold;
            remainingDistanceText.alignment = TextAlignmentOptions.Center;
            SetRect(remainingDistanceText.rectTransform, Vector2.zero, new Vector2(590f, 60f));
        }

        if (statusText != null)
        {
            statusText.transform.parent.gameObject.SetActive(true);
            var panel = statusText.transform.parent as RectTransform;
            SetRect(panel, new Vector2(0f, -178f), new Vector2(960f, 64f));
            statusText.fontSize = 23f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(1f, 0.96f, 0.72f, 1f);
            SetStretch(statusText.rectTransform, 18f, 8f, 18f, 8f);
        }

        if (warningText != null)
        {
            var panel = warningText.transform.parent as RectTransform;
            SetRect(panel, new Vector2(0f, 150f), new Vector2(760f, 104f));
            warningText.fontSize = 38f;
            warningText.lineSpacing = -4f;
            warningText.alignment = TextAlignmentOptions.Center;
            SetStretch(warningText.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (directionArrowText != null)
        {
            directionArrowText.fontSize = 96f;
            SetRect(directionArrowText.rectTransform, new Vector2(0f, -40f), new Vector2(160f, 160f));
        }

        if (progressSlider != null)
        {
            var panel = progressSlider.transform.parent as RectTransform;
            SetRect(panel, new Vector2(0f, -292f), new Vector2(620f, 118f));
            SetRect(progressSlider.transform as RectTransform, new Vector2(0f, -8f), new Vector2(360f, 28f));
            ConfigureProgressLabel(progressSlider.transform.parent, "RunnerIcon", new Vector2(-240f, 44f), new Vector2(82f, 32f), 20f);
            ConfigureProgressLabel(progressSlider.transform.parent, "TrainIcon", new Vector2(240f, 44f), new Vector2(96f, 32f), 20f);
        }

        if (progressText != null)
        {
            progressText.fontSize = 20f;
            progressText.alignment = TextAlignmentOptions.Center;
            SetRect(progressText.rectTransform, new Vector2(0f, -42f), new Vector2(120f, 30f));
        }

        SetNavigationStatsVisible(_navigationStarted);
    }

    private static void SetButtonLayout(Button button, Vector2 anchoredPosition, Vector2 size, float labelSize)
    {
        if (button == null)
            return;

        var rect = button.transform as RectTransform;
        SetRect(rect, anchoredPosition, size);

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;

        label.fontSize = labelSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        SetStretch(label.rectTransform, 8f, 8f, 8f, 8f);
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ConfigureProgressLabel(Transform progressPanel, string childName, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        if (progressPanel == null)
            return;

        var label = progressPanel.Find(childName)?.GetComponent<TMP_Text>();
        if (label == null)
            return;

        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        SetRect(label.rectTransform, anchoredPosition, size);
    }

    private void SetNavigationStatsVisible(bool visible)
    {
        SetParentActive(remainingDistanceText, visible);
        SetParentActive(progressSlider, visible);
    }

    private static void SetParentActive(Component component, bool active)
    {
        if (component == null || component.transform.parent == null)
            return;

        component.transform.parent.gameObject.SetActive(active);
    }

    private void Update()
    {
        if (!_navigationStarted)
        {
            UpdateRouteSelectionDemo();
            return;
        }

        if (!_navigationStarted || _arrived || userTransform == null || routePoints == null || routePoints.Length < 2)
            return;

        AdvanceRoutePointIfNeeded();

        var remainingDistance = CalculateRemainingDistance(userTransform.position);
        UpdateRemainingDistanceText(remainingDistance);
        UpdateProgress(remainingDistance);

        var offRoute = DistanceFromRoute(userTransform.position) > routeDeviationThreshold;
        SetWarning(offRoute);
        UpdateDirectionArrow(offRoute);

        if (offRoute)
            SetStatus("Please return to the path.");
        else
            SetStatus($"Heading to RP {_targetIndex:00}");

        if (_targetIndex >= routePoints.Length - 1 &&
            HorizontalDistance(userTransform.position, routePoints[_targetIndex].position) <= pointReachRadius)
        {
            Arrive();
        }
    }

    public void StartNavigation()
    {
        StartRouteA();
    }

    public void StartRouteA()
    {
        StartNavigationForRoute(routePointsA, "A");
    }

    public void StartRouteB()
    {
        StartNavigationForRoute(routePointsB, "B");
    }

    public void StartRouteC()
    {
        StartNavigationForRoute(routePointsC, "C");
    }

    private void StartNavigationForRoute(Transform[] selectedRoute, string routeName)
    {
        routePoints = selectedRoute;
        if (routePoints == null || routePoints.Length < 2)
        {
            SetStatus($"Route {routeName} is not assigned.");
            return;
        }

        AlignRoutesToStartupViewIfNeeded();
        _navigationStarted = true;
        _arrived = false;
        _targetIndex = 1;
        _totalRouteDistance = CalculateRouteDistance();

        if (destinationSelectPanel != null)
            destinationSelectPanel.SetActive(false);
        SetNavigationStatsVisible(true);
        SetSelectionRayVisible(false);
        SetEyeWarningVisible(false);

        SetArrived(false);
        SetWarning(false);
        SetStatus($"Route {routeName} selected. Follow the yellow arrow.");
        SetDirectionArrow(true, false, 0f);

        SetRoutePreview(routePoints);
    }

    private void ResetRouteSelection()
    {
        if (userTransform == null && Camera.main != null)
            userTransform = Camera.main.transform;

        _selectionEnabledTime = Time.time + Mathf.Max(0f, selectionStartupGraceSeconds);
        _nextPreviewTime = Time.time + Mathf.Max(0.1f, routePreviewIntervalSeconds);
        _previewIndex = -1;
        _currentPinchTarget = RouteChoice.None;
        _wasPinching = false;
    }

    private void UpdateRouteSelectionDemo()
    {
        UpdateSelectionPanelFollow();

        if (previewRoutesBeforeSelection)
            UpdateCyclicRoutePreview();

        if (!usePinchRouteSelection)
            return;

        EnsureRouteButtons();
        ResolvePinchInputsIfNeeded();
        ApplyRouteButtonColors();

        if (Time.time < _selectionEnabledTime)
        {
            var remaining = Mathf.CeilToInt(_selectionEnabledTime - Time.time);
            SetStatus($"Get ready: aim hand/controller ray, then pinch or trigger ({remaining})");
            return;
        }

        var choice = GetLookedRouteChoice();
        UpdateSelectionRayVisual();
        if (choice == RouteChoice.None)
        {
            _currentPinchTarget = RouteChoice.None;
            _wasPinching = IsPinchingNow();
            ApplyRouteButtonColors();
            SetStatus("Use hand/controller ray on A/B/C, then pinch or trigger.");
            return;
        }

        _currentPinchTarget = choice;
        ApplyRouteButtonColors();
        SetStatus($"Route {choice} ready. Pinch or trigger to select. ({_currentSelectionSource})");

        if (!TryConsumePinchDown())
        {
            SetStatus($"Route {choice} ready. {_lastPinchStatus}");
            return;
        }

        StartRouteChoice(choice);
    }

    private void UpdateSelectionPanelFollow()
    {
        // Kept for older serialized scenes. The whole NavigationHUD now follows the camera.
        followSelectionPanelToUser = false;
    }

    private void UpdateCyclicRoutePreview()
    {
        if (Time.time < _nextPreviewTime)
            return;

        _nextPreviewTime = Time.time + Mathf.Max(0.1f, routePreviewIntervalSeconds);
        _previewIndex = (_previewIndex + 1) % 3;

        switch (_previewIndex)
        {
            case 0:
                SetRoutePreview(routePointsA);
                break;
            case 1:
                SetRoutePreview(routePointsB);
                break;
            default:
                SetRoutePreview(routePointsC);
                break;
        }
    }

    private RouteChoice GetLookedRouteChoice()
    {
        _hasSelectionRay = false;
        _currentSelectionRayLength = maxSelectionRayDistance;

        var hasFallbackRay = false;
        var fallbackRay = default(Ray);
        var fallbackSource = "Ray";

        if (preferHandRay)
        {
            if (TryEvaluateHandRays(ref hasFallbackRay, ref fallbackRay, ref fallbackSource, out var choice))
                return choice;
            if (TryEvaluateControllerRays(ref hasFallbackRay, ref fallbackRay, ref fallbackSource, out choice))
                return choice;
        }
        else
        {
            if (TryEvaluateControllerRays(ref hasFallbackRay, ref fallbackRay, ref fallbackSource, out var choice))
                return choice;
            if (TryEvaluateHandRays(ref hasFallbackRay, ref fallbackRay, ref fallbackSource, out choice))
                return choice;
        }

        if (allowGazeRay && TryGetGazeRay(out var gazeRay))
        {
            if (TryEvaluateSelectionRay(gazeRay, "Gaze ray", out var choice))
                return choice;
            if (TryEvaluateGazeScreenCenter("Gaze center", out choice))
                return choice;
            RememberFallbackRay(gazeRay, "Gaze ray", ref hasFallbackRay, ref fallbackRay, ref fallbackSource);
        }

        if (hasFallbackRay)
        {
            _hasSelectionRay = true;
            _currentSelectionRay = fallbackRay;
            _currentSelectionRayLength = maxSelectionRayDistance;
            _currentSelectionSource = fallbackSource;
        }

        return RouteChoice.None;
    }

    private bool TryEvaluateHandRays(
        ref bool hasFallbackRay,
        ref Ray fallbackRay,
        ref string fallbackSource,
        out RouteChoice choice)
    {
        if (TryGetHandRay(_rightHand, _rightPointerPose, _rightAnchor, _rightIndexTip, out var rightRay))
        {
            if (TryEvaluateSelectionRay(rightRay, "Right hand ray", out choice))
                return true;
            RememberFallbackRay(rightRay, "Right hand ray", ref hasFallbackRay, ref fallbackRay, ref fallbackSource);
        }

        if (TryGetHandRay(_leftHand, _leftPointerPose, _leftAnchor, _leftIndexTip, out var leftRay))
        {
            if (TryEvaluateSelectionRay(leftRay, "Left hand ray", out choice))
                return true;
            RememberFallbackRay(leftRay, "Left hand ray", ref hasFallbackRay, ref fallbackRay, ref fallbackSource);
        }

        choice = RouteChoice.None;
        return false;
    }

    private bool TryEvaluateControllerRays(
        ref bool hasFallbackRay,
        ref Ray fallbackRay,
        ref string fallbackSource,
        out RouteChoice choice)
    {
        choice = RouteChoice.None;
        if (!allowControllerRay)
            return false;

        ResolveControllerAnchorsIfNeeded();
        if (TryGetTransformRay(_rightControllerAnchor, out var rightRay))
        {
            if (TryEvaluateSelectionRay(rightRay, "Right controller ray", out choice))
                return true;
            RememberFallbackRay(rightRay, "Right controller ray", ref hasFallbackRay, ref fallbackRay, ref fallbackSource);
        }

        if (TryGetTransformRay(_leftControllerAnchor, out var leftRay))
        {
            if (TryEvaluateSelectionRay(leftRay, "Left controller ray", out choice))
                return true;
            RememberFallbackRay(leftRay, "Left controller ray", ref hasFallbackRay, ref fallbackRay, ref fallbackSource);
        }

        return false;
    }

    private bool TryEvaluateSelectionRay(Ray selectionRay, string source, out RouteChoice choice)
    {
        var best = RouteChoice.None;
        var bestDistance = float.MaxValue;
        EvaluateRouteButtonRay(routeAButton, RouteChoice.A, selectionRay, maxSelectionRayDistance, selectionHitPadding, ref best, ref bestDistance);
        EvaluateRouteButtonRay(routeBButton, RouteChoice.B, selectionRay, maxSelectionRayDistance, selectionHitPadding, ref best, ref bestDistance);
        EvaluateRouteButtonRay(routeCButton, RouteChoice.C, selectionRay, maxSelectionRayDistance, selectionHitPadding, ref best, ref bestDistance);

        if (best == RouteChoice.None)
        {
            choice = RouteChoice.None;
            return false;
        }

        _hasSelectionRay = true;
        _currentSelectionRay = selectionRay;
        _currentSelectionRayLength = bestDistance;
        _currentSelectionSource = source;
        choice = best;
        return true;
    }

    private bool TryEvaluateGazeScreenCenter(string source, out RouteChoice choice)
    {
        choice = RouteChoice.None;

        var cam = Camera.main;
        if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
            return false;

        var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var best = RouteChoice.None;
        var bestDistance = float.MaxValue;
        EvaluateRouteButtonScreenCenter(routeAButton, RouteChoice.A, cam, screenCenter, ref best, ref bestDistance);
        EvaluateRouteButtonScreenCenter(routeBButton, RouteChoice.B, cam, screenCenter, ref best, ref bestDistance);
        EvaluateRouteButtonScreenCenter(routeCButton, RouteChoice.C, cam, screenCenter, ref best, ref bestDistance);

        if (best == RouteChoice.None || bestDistance > gazeScreenFallbackMaxPixels)
            return false;

        _hasSelectionRay = true;
        _currentSelectionRay = new Ray(cam.transform.position, cam.transform.forward);
        _currentSelectionRayLength = maxSelectionRayDistance;
        _currentSelectionSource = source;
        choice = best;
        return true;
    }

    private static void RememberFallbackRay(
        Ray ray,
        string source,
        ref bool hasFallbackRay,
        ref Ray fallbackRay,
        ref string fallbackSource)
    {
        if (hasFallbackRay)
            return;

        hasFallbackRay = true;
        fallbackRay = ray;
        fallbackSource = source;
    }

    private void StartRouteChoice(RouteChoice choice)
    {
        switch (choice)
        {
            case RouteChoice.A:
                StartRouteA();
                break;
            case RouteChoice.B:
                StartRouteB();
                break;
            case RouteChoice.C:
                StartRouteC();
                break;
        }
    }

    private void SetRoutePreview(Transform[] previewRoute)
    {
        if (routePathRenderer == null)
            return;
        routePathRenderer.SetRoutePoints(previewRoute ?? Array.Empty<Transform>());
        routePathRenderer.Refresh();
    }

    private bool TryGetHandRay(Component hand, Transform pointerPose, Transform handAnchor, Transform indexTip, out Ray ray)
    {
        if (!IsHandTracked(hand))
        {
            ray = default;
            return false;
        }

        if (pointerPose != null && pointerPose.gameObject.activeInHierarchy)
        {
            ray = new Ray(pointerPose.position, pointerPose.forward);
            return ray.direction.sqrMagnitude > 0.0001f;
        }

        if (indexTip != null && indexTip.gameObject.activeInHierarchy)
        {
            var direction = indexTip.forward;
            if (direction.sqrMagnitude <= 0.0001f && handAnchor != null)
                direction = handAnchor.forward;
            ray = new Ray(indexTip.position, direction.normalized);
            return ray.direction.sqrMagnitude > 0.0001f;
        }

        if (handAnchor != null && handAnchor.gameObject.activeInHierarchy)
        {
            ray = new Ray(handAnchor.position, handAnchor.forward);
            return ray.direction.sqrMagnitude > 0.0001f;
        }

        ray = default;
        return false;
    }

    private static bool TryGetTransformRay(Transform anchor, out Ray ray)
    {
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
        {
            ray = default;
            return false;
        }

        ray = new Ray(anchor.position, anchor.forward);
        return ray.direction.sqrMagnitude > 0.0001f;
    }

    private static bool TryGetGazeRay(out Ray ray)
    {
        var cam = Camera.main;
        if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
        {
            ray = default;
            return false;
        }

        ray = new Ray(cam.transform.position, cam.transform.forward);
        return true;
    }

    private void EnsureRouteButtons()
    {
        if (routeAButton == null)
            routeAButton = FindRouteButton("Destination_A_Button");
        if (routeBButton == null)
            routeBButton = FindRouteButton("Destination_B_Button");
        if (routeCButton == null)
            routeCButton = FindRouteButton("Destination_C_Button");
    }

    private static Button FindRouteButton(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static void EvaluateRouteButtonRay(
        Button button,
        RouteChoice choice,
        Ray ray,
        float maxDistance,
        float hitPadding,
        ref RouteChoice best,
        ref float bestDistance)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            return;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return;

        var plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out var distance))
            return;

        if (distance >= bestDistance)
            return;
        if (distance < 0f || distance > maxDistance)
            return;

        var hitPoint = ray.GetPoint(distance);
        var localPoint = rect.InverseTransformPoint(hitPoint);
        var paddedRect = rect.rect;
        paddedRect.xMin -= hitPadding;
        paddedRect.xMax += hitPadding;
        paddedRect.yMin -= hitPadding;
        paddedRect.yMax += hitPadding;
        if (!paddedRect.Contains(new Vector2(localPoint.x, localPoint.y)))
            return;

        bestDistance = distance;
        best = choice;
    }

    private static void EvaluateRouteButtonScreenCenter(
        Button button,
        RouteChoice choice,
        Camera cam,
        Vector2 screenCenter,
        ref RouteChoice best,
        ref float bestDistance)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            return;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return;

        var viewportPoint = cam.WorldToViewportPoint(rect.position);
        if (viewportPoint.z < 0f)
            return;

        var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
        var distance = Vector2.Distance(screenCenter, screenPoint);
        if (distance >= bestDistance)
            return;

        bestDistance = distance;
        best = choice;
    }

    private void ApplyRouteButtonColors()
    {
        SetRouteButtonColor(routeAButton, new Color(0.95f, 0.48f, 0.28f, 0.95f), _currentPinchTarget == RouteChoice.A);
        SetRouteButtonColor(routeBButton, new Color(0.24f, 0.74f, 0.98f, 0.95f), _currentPinchTarget == RouteChoice.B);
        SetRouteButtonColor(routeCButton, new Color(0.54f, 0.34f, 0.92f, 0.95f), _currentPinchTarget == RouteChoice.C);
    }

    private static void SetRouteButtonColor(Button button, Color color, bool active)
    {
        if (button == null)
            return;

        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = active ? Color.Lerp(color, Color.white, 0.58f) : color;
        button.transform.localScale = active ? Vector3.one * 1.08f : Vector3.one;
    }

    private void UpdateSelectionRayVisual()
    {
        if (!showSelectionRay || !_hasSelectionRay)
        {
            SetSelectionRayVisible(false);
            return;
        }

        if (_selectionRayRenderer == null)
            _selectionRayRenderer = CreateSelectionRayRenderer();

        if (_selectionRayRenderer == null)
            return;

        var length = Mathf.Clamp(_currentSelectionRayLength, 0.05f, maxSelectionRayDistance);
        _selectionRayRenderer.enabled = true;
        _selectionRayRenderer.positionCount = 2;
        var direction = _currentSelectionRay.direction.normalized;
        var start = _currentSelectionRay.origin + direction * Mathf.Max(0f, selectionRayStartOffset);
        _selectionRayRenderer.SetPosition(0, start);
        _selectionRayRenderer.SetPosition(1, start + direction * length);
    }

    private LineRenderer CreateSelectionRayRenderer()
    {
        var rayObject = new GameObject("RouteSelectionRay");
        rayObject.transform.SetParent(transform, false);

        var renderer = rayObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader != null)
            renderer.material = new Material(shader);
        renderer.widthMultiplier = 0.01f;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 2;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.startColor = new Color(1f, 0.9f, 0.15f, 0.95f);
        renderer.endColor = new Color(1f, 0.9f, 0.15f, 0.2f);
        renderer.enabled = false;
        return renderer;
    }

    private void SetSelectionRayVisible(bool visible)
    {
        if (_selectionRayRenderer != null)
            _selectionRayRenderer.enabled = visible;
    }

    private bool TryConsumePinchDown()
    {
        var isPinching = IsPinchingNow();
        if (!isPinching)
        {
            _wasPinching = false;
            return false;
        }

        if (_wasPinching || Time.time - _lastPinchSelectionTime < pinchCooldownSeconds)
            return false;

        _wasPinching = true;
        _lastPinchSelectionTime = Time.time;
        return true;
    }

    private bool IsPinchingNow()
    {
        ResolvePinchInputsIfNeeded();

        var leftTracked = IsHandTracked(_leftHand);
        var rightTracked = IsHandTracked(_rightHand);
        var leftReady = TryReadHandPinch(_leftHand, _leftIndexTip, _leftThumbTip, out var leftDistance);
        var rightReady = TryReadHandPinch(_rightHand, _rightIndexTip, _rightThumbTip, out var rightDistance);

        if (leftReady && leftDistance <= pinchDownDistanceMeters)
        {
            _lastPinchStatus = "Left hand pinch detected.";
            return true;
        }
        if (rightReady && rightDistance <= pinchDownDistanceMeters)
        {
            _lastPinchStatus = "Right hand pinch detected.";
            return true;
        }

        if (leftReady || rightReady)
        {
            _lastPinchStatus = "Hand tracked, pinch not closed.";
        }
        else if (leftTracked || rightTracked)
        {
            _lastPinchStatus = "Hand tracked, but pinch API/tips missing.";
        }
        else
        {
            _lastPinchStatus = allowControllerTriggerFallback
                ? "Use controller trigger, or enable hand tracking."
                : "No tracked OVRHand. Put controllers down and enable hand tracking.";
        }

        if (allowControllerTriggerFallback && TryReadOvrHandTrigger())
        {
            _lastPinchStatus = "Controller trigger detected.";
            return true;
        }

        return false;
    }

    private void ResolvePinchInputsIfNeeded()
    {
        if (_leftHand != null && _rightHand != null && _leftIndexTip != null && _rightIndexTip != null && _ovrInputGetButtonMethod != null)
            return;

        if (_leftHand == null)
            _leftHand = FindOvrHandComponent(new[] { "LeftHandAnchor", "LeftHand", "LHandAnchor" }, out _leftAnchor);
        if (_rightHand == null)
            _rightHand = FindOvrHandComponent(new[] { "RightHandAnchor", "RightHand", "RHandAnchor" }, out _rightAnchor);

        ResolveFingerTips(_leftAnchor, ref _leftIndexTip, ref _leftThumbTip);
        ResolveFingerTips(_rightAnchor, ref _rightIndexTip, ref _rightThumbTip);

        var sample = _leftHand != null ? _leftHand : _rightHand;
        if (sample != null && _getFingerIsPinchingMethod == null)
            ResolveOvrHandApi(sample);
        _leftPointerPose ??= TryReadPointerPose(_leftHand);
        _rightPointerPose ??= TryReadPointerPose(_rightHand);

        if (allowControllerTriggerFallback)
            ResolveOvrInputFallbackApi();
        if (allowControllerRay)
            ResolveControllerAnchorsIfNeeded();
    }

    private void ResolveControllerAnchorsIfNeeded()
    {
        if (_rightControllerAnchor == null)
            _rightControllerAnchor = FindFirstTransformByName("RightControllerAnchor", "RightControllerInHandAnchor", "RightHandOnControllerAnchor");
        if (_leftControllerAnchor == null)
            _leftControllerAnchor = FindFirstTransformByName("LeftControllerAnchor", "LeftControllerInHandAnchor", "LeftHandOnControllerAnchor");
    }

    private static Transform FindFirstTransformByName(params string[] names)
    {
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null)
                return go.transform;
        }

        return null;
    }

    private void ResolveOvrHandApi(Component sample)
    {
        var handType = sample.GetType();
        _isTrackedProperty = handType.GetProperty("IsTracked", BindingFlags.Public | BindingFlags.Instance);
        _isHighConfidenceProperty = handType.GetProperty("IsDataHighConfidence", BindingFlags.Public | BindingFlags.Instance);
        _pointerPoseProperty = handType.GetProperty("PointerPose", BindingFlags.Public | BindingFlags.Instance);
        _pointerPoseField = handType.GetField("PointerPose", BindingFlags.Public | BindingFlags.Instance);

        var fingerType = handType.GetNestedType("HandFinger", BindingFlags.Public);
        if (fingerType == null)
            return;

        _indexFingerEnumValue = Enum.Parse(fingerType, "Index");
        _getFingerIsPinchingMethod = handType.GetMethod("GetFingerIsPinching", new[] { fingerType });
        _getFingerPinchStrengthMethod = handType.GetMethod("GetFingerPinchStrength", new[] { fingerType });
    }

    private Transform TryReadPointerPose(Component hand)
    {
        if (hand == null)
            return null;

        try
        {
            if (_pointerPoseProperty != null && _pointerPoseProperty.GetValue(hand, null) is Transform propertyTransform)
                return propertyTransform;
        }
        catch
        {
            // Some SDK versions expose pointer pose differently.
        }

        try
        {
            if (_pointerPoseField != null && _pointerPoseField.GetValue(hand) is Transform fieldTransform)
                return fieldTransform;
        }
        catch
        {
            // Fall back to anchor/index transforms.
        }

        return null;
    }

    private bool TryReadHandPinch(Component hand, Transform indexTip, Transform thumbTip, out float pinchDistance)
    {
        pinchDistance = pinchUpDistanceMeters + 0.02f;
        if (!IsHandTracked(hand))
            return false;

        if (indexTip != null && thumbTip != null)
        {
            pinchDistance = Vector3.Distance(indexTip.position, thumbTip.position);
            return true;
        }

        if (_indexFingerEnumValue != null && _getFingerIsPinchingMethod != null)
        {
            try
            {
                var value = _getFingerIsPinchingMethod.Invoke(hand, new[] { _indexFingerEnumValue });
                if (value is bool pressed && pressed)
                {
                    pinchDistance = pinchDownDistanceMeters * 0.5f;
                    return true;
                }
            }
            catch
            {
                // Keep trying other pinch sources.
            }
        }

        if (_indexFingerEnumValue != null && _getFingerPinchStrengthMethod != null)
        {
            try
            {
                var value = _getFingerPinchStrengthMethod.Invoke(hand, new[] { _indexFingerEnumValue });
                if (value is float strength)
                {
                    pinchDistance = Mathf.Lerp(pinchUpDistanceMeters + 0.01f, pinchDownDistanceMeters * 0.5f, Mathf.Clamp01(strength));
                    return true;
                }
            }
            catch
            {
                // Ignore unavailable SDK variants.
            }
        }

        return false;
    }

    private bool TryReadOvrHandTrigger()
    {
        if (_ovrInputGetButtonMethod == null)
            return false;

        try
        {
            if (_ovrPrimaryIndexTriggerButton != null &&
                InvokeOvrInputGet(_ovrPrimaryIndexTriggerButton))
            {
                return true;
            }

            if (_ovrSecondaryIndexTriggerButton != null &&
                InvokeOvrInputGet(_ovrSecondaryIndexTriggerButton))
            {
                return true;
            }

            if (_ovrPrimaryHandTriggerButton != null &&
                InvokeOvrInputGet(_ovrPrimaryHandTriggerButton))
            {
                return true;
            }

            if (_ovrSecondaryHandTriggerButton != null &&
                InvokeOvrInputGet(_ovrSecondaryHandTriggerButton))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private bool InvokeOvrInputGet(object button)
    {
        var parameters = _ovrInputGetButtonMethod.GetParameters();
        var args = parameters.Length == 1
            ? new[] { button }
            : new[] { button, _ovrControllerMask };
        return (bool)_ovrInputGetButtonMethod.Invoke(null, args);
    }

    private void ResolveOvrInputFallbackApi()
    {
        if (_ovrInputGetButtonMethod != null)
            return;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var inputType = asm.GetType("OVRInput");
            if (inputType == null)
                continue;

            var buttonType = inputType.GetNestedType("Button", BindingFlags.Public);
            if (buttonType == null)
                continue;

            var getMethod = FindOvrInputGetMethod(inputType, buttonType);
            if (getMethod == null)
                continue;

            try
            {
                _ovrPrimaryIndexTriggerButton = TryParseEnum(buttonType, "PrimaryIndexTrigger");
                _ovrSecondaryIndexTriggerButton = TryParseEnum(buttonType, "SecondaryIndexTrigger");
                _ovrPrimaryHandTriggerButton = TryParseEnum(buttonType, "PrimaryHandTrigger");
                _ovrSecondaryHandTriggerButton = TryParseEnum(buttonType, "SecondaryHandTrigger");
                if (_ovrPrimaryIndexTriggerButton == null &&
                    _ovrSecondaryIndexTriggerButton == null &&
                    _ovrPrimaryHandTriggerButton == null &&
                    _ovrSecondaryHandTriggerButton == null)
                {
                    continue;
                }

                _ovrControllerMask = ResolveOvrControllerMask(inputType, getMethod);
                _ovrInputGetButtonMethod = getMethod;
                return;
            }
            catch
            {
                // Keep searching for compatible OVRInput variants.
            }
        }
    }

    private static object TryParseEnum(Type enumType, string name)
    {
        try
        {
            return Enum.Parse(enumType, name);
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo FindOvrInputGetMethod(Type inputType, Type buttonType)
    {
        foreach (var method in inputType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "Get" || method.ReturnType != typeof(bool))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == buttonType)
                return method;
            if (parameters.Length == 2 && parameters[0].ParameterType == buttonType)
                return method;
        }

        return null;
    }

    private static object ResolveOvrControllerMask(Type inputType, MethodInfo getMethod)
    {
        var parameters = getMethod.GetParameters();
        if (parameters.Length < 2)
            return null;

        var controllerType = parameters[1].ParameterType;
        if (controllerType.IsEnum)
        {
            try
            {
                return Enum.Parse(controllerType, "Active");
            }
            catch
            {
                return Activator.CreateInstance(controllerType);
            }
        }

        var nestedControllerType = inputType.GetNestedType("Controller", BindingFlags.Public);
        if (nestedControllerType != null && nestedControllerType.IsEnum)
        {
            try
            {
                return Enum.Parse(nestedControllerType, "Active");
            }
            catch
            {
                return Activator.CreateInstance(nestedControllerType);
            }
        }

        return parameters[1].DefaultValue;
    }

    private bool IsHandTracked(Component hand)
    {
        if (hand == null)
            return false;

        var tracked = ReadBoolProperty(hand, _isTrackedProperty, true);
        var highConfidence = ReadBoolProperty(hand, _isHighConfidenceProperty, true);
        return tracked && highConfidence;
    }

    private static Component FindOvrHandComponent(string[] anchorNames, out Transform anchor)
    {
        foreach (var component in FindObjectsOfType<Component>(true))
        {
            if (component == null || component.GetType().Name != "OVRHand")
                continue;

            var lowerName = component.name.ToLowerInvariant();
            foreach (var anchorName in anchorNames)
            {
                if (lowerName.Contains(anchorName.ToLowerInvariant().Replace("anchor", string.Empty)))
                {
                    anchor = component.transform;
                    return component;
                }
            }
        }

        foreach (var anchorName in anchorNames)
        {
            var anchorObject = GameObject.Find(anchorName);
            if (anchorObject == null)
                continue;

            anchor = anchorObject.transform;
            foreach (var component in anchorObject.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "OVRHand")
                    return component;
            }
        }

        anchor = null;
        return null;
    }

    private static void ResolveFingerTips(Transform anchor, ref Transform indexTip, ref Transform thumbTip)
    {
        if (anchor == null)
            return;

        var candidates = anchor.GetComponentsInChildren<Transform>(true);
        if (indexTip == null)
            indexTip = FindFingerTip(candidates, "IndexTip", "Hand_IndexTip");
        if (thumbTip == null)
            thumbTip = FindFingerTip(candidates, "ThumbTip", "Hand_ThumbTip");
    }

    private static Transform FindFingerTip(Transform[] candidates, string firstName, string secondName)
    {
        foreach (var t in candidates)
        {
            var n = t.name;
            if (n.IndexOf(firstName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf(secondName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return t;
            }
        }

        return null;
    }

    private static bool ReadBoolProperty(object instance, PropertyInfo propertyInfo, bool fallback)
    {
        if (instance == null || propertyInfo == null)
            return fallback;

        try
        {
            var value = propertyInfo.GetValue(instance, null);
            return value is bool b ? b : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void AdvanceRoutePointIfNeeded()
    {
        while (_targetIndex < routePoints.Length - 1 &&
               routePoints[_targetIndex] != null &&
               HorizontalDistance(userTransform.position, routePoints[_targetIndex].position) <= pointReachRadius)
        {
            _targetIndex++;
        }
    }

    private void Arrive()
    {
        _arrived = true;
        SetWarning(false);
        SetEyeWarningVisible(false);
        SetArrived(true);
        UpdateRemainingDistanceText(0f);
        UpdateProgress(0f);
        SetDirectionArrow(false, false, 0f);
        SetStatus("Arrived.");
    }

    private float CalculateRouteDistance()
    {
        if (routePoints == null || routePoints.Length < 2)
            return 0f;

        var distance = 0f;
        for (var i = 0; i < routePoints.Length - 1; i++)
        {
            if (routePoints[i] == null || routePoints[i + 1] == null)
                continue;

            distance += HorizontalDistance(routePoints[i].position, routePoints[i + 1].position);
        }

        return distance;
    }

    private float CalculateRemainingDistance(Vector3 userPosition)
    {
        if (routePoints == null || routePoints.Length < 2)
            return 0f;

        var distance = HorizontalDistance(userPosition, routePoints[_targetIndex].position);
        for (var i = _targetIndex; i < routePoints.Length - 1; i++)
            distance += HorizontalDistance(routePoints[i].position, routePoints[i + 1].position);

        return distance;
    }

    private float DistanceFromRoute(Vector3 userPosition)
    {
        var minDistance = float.MaxValue;

        for (var i = 0; i < routePoints.Length - 1; i++)
        {
            if (routePoints[i] == null || routePoints[i + 1] == null)
                continue;

            var distance = DistancePointToSegmentXZ(userPosition, routePoints[i].position, routePoints[i + 1].position);
            if (distance < minDistance)
                minDistance = distance;
        }

        return minDistance;
    }

    private void UpdateRemainingDistanceText(float meters)
    {
        if (remainingDistanceText != null)
            remainingDistanceText.text = $"To TRAIN: {Mathf.CeilToInt(meters)}m";

        if (etaText != null)
        {
            var seconds = walkingSpeedMetersPerSecond > 0f ? meters / walkingSpeedMetersPerSecond : 0f;
            etaText.text = seconds < 60f ? "Arrival: < 1min" : $"Arrival: {Mathf.CeilToInt(seconds / 60f)}min";
        }
    }

    private void UpdateProgress(float remainingDistance)
    {
        var progress = _totalRouteDistance > 0f ? Mathf.Clamp01(1f - remainingDistance / _totalRouteDistance) : 0f;

        if (progressSlider != null)
            progressSlider.value = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void SetWarning(bool active)
    {
        if (warningGroup != null)
        {
            warningGroup.alpha = active ? 1f : 0f;
            warningGroup.interactable = active;
            warningGroup.blocksRaycasts = active;
        }

        if (warningText != null)
            warningText.gameObject.SetActive(active);

        UpdateEyeLevelOffRouteWarning(active);
    }

    private void UpdateEyeLevelOffRouteWarning(bool active)
    {
        if (!showEyeLevelOffRouteWarning)
            return;

        EnsureEyeWarningText();
        if (_eyeWarningText == null || userTransform == null)
            return;

        if (!active)
        {
            SetEyeWarningVisible(false);
            return;
        }

        var targetPosition = userTransform.position +
                             userTransform.forward * eyeWarningDistance +
                             Vector3.up * eyeWarningHeightOffset;
        _eyeWarningText.transform.position = targetPosition;

        var toCamera = _eyeWarningText.transform.position - userTransform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            _eyeWarningText.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);

        SetEyeWarningVisible(true);
    }

    private void EnsureEyeWarningText()
    {
        if (_eyeWarningText != null)
            return;

        var warningObject = new GameObject("OffRouteEyeWarning");
        warningObject.transform.SetParent(transform, false);
        _eyeWarningText = warningObject.AddComponent<TextMeshPro>();
        _eyeWarningText.text = "OFF ROUTE!\nRETURN TO PATH";
        _eyeWarningText.alignment = TextAlignmentOptions.Center;
        _eyeWarningText.fontSize = 1.7f;
        _eyeWarningText.color = new Color(1f, 0.14f, 0.08f, 1f);
        _eyeWarningText.outlineWidth = 0.2f;
        _eyeWarningText.enableWordWrapping = false;
        _eyeWarningText.raycastTarget = false;
        warningObject.transform.localScale = Vector3.one * 0.08f;
        SetEyeWarningVisible(false);
    }

    private void SetEyeWarningVisible(bool visible)
    {
        if (_eyeWarningText != null)
            _eyeWarningText.gameObject.SetActive(visible);
    }

    private void SetArrived(bool active)
    {
        if (arrivalGroup != null)
        {
            arrivalGroup.alpha = active ? 1f : 0f;
            arrivalGroup.interactable = active;
            arrivalGroup.blocksRaycasts = active;
        }

        if (arrivalText != null)
            arrivalText.gameObject.SetActive(active);
    }

    private void UpdateDirectionArrow(bool offRoute)
    {
        if (directionArrowRect == null || routePoints == null || routePoints.Length == 0)
            return;

        var destination = routePoints[Mathf.Clamp(_targetIndex, 0, routePoints.Length - 1)];
        if (destination == null)
            return;

        var toTarget = destination.position - userTransform.position;
        toTarget.y = 0f;

        var forward = userTransform.forward;
        forward.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f || forward.sqrMagnitude <= 0.0001f)
            return;

        var signedAngle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up);
        SetDirectionArrow(true, offRoute, -signedAngle);
    }

    private void SetDirectionArrow(bool active, bool offRoute, float zRotation)
    {
        if (directionArrowText != null)
        {
            directionArrowText.gameObject.SetActive(active);
            directionArrowText.color = offRoute
                ? new Color(1f, 0.08f, 0.04f, 0.92f)
                : new Color(1f, 0.95f, 0.45f, 0.9f);
        }

        if (directionArrowRect != null)
        {
            directionArrowRect.gameObject.SetActive(active);
            directionArrowRect.localEulerAngles = new Vector3(0f, 0f, zRotation);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static float DistancePointToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
    {
        var p = new Vector2(point.x, point.z);
        var start = new Vector2(a.x, a.z);
        var end = new Vector2(b.x, b.z);
        var segment = end - start;

        if (segment.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.Distance(p, start);

        var t = Mathf.Clamp01(Vector2.Dot(p - start, segment) / segment.sqrMagnitude);
        return Vector2.Distance(p, start + segment * t);
    }
}
