using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HandUiPinchClicker : MonoBehaviour
{
    [SerializeField] private Camera eventCamera;
    [SerializeField] private Button targetButton;
    [SerializeField] private Component leftHandOverride;
    [SerializeField] private Component rightHandOverride;
    [SerializeField] private bool preferRightHand = true;
    [SerializeField] private float pinchDownDistanceMeters = 0.028f;
    [SerializeField] private float pinchUpDistanceMeters = 0.04f;
    [SerializeField] private float clickCooldownSeconds = 0.25f;
    [SerializeField] private bool enableDebugOverlay = true;
    [SerializeField] private float debugMessageDuration = 1.2f;
    [SerializeField] private float diagnosticMessageInterval = 1.0f;
    [SerializeField] private bool enableGazeDwellFallback = true;
    [SerializeField] private float gazeDwellSeconds = 1.1f;
    [SerializeField] private TMP_Text debugText;

    private GraphicRaycaster _raycaster;
    private PointerEventData _pointerEventData;
    private EventSystem _eventSystem;
    private bool _wasPinching;
    private float _lastClickTime = -10f;
    private float _nextDiagnosticTime;
    private float _debugHideAt;
    private Button _gazeTargetButton;
    private float _gazeStartTime;

    private Component _leftHand;
    private Component _rightHand;
    private Transform _leftAnchor;
    private Transform _rightAnchor;
    private PropertyInfo _isTrackedProperty;
    private PropertyInfo _isHighConfidenceProperty;
    private MethodInfo _getFingerIsPinchingMethod;
    private MethodInfo _getFingerPinchStrengthMethod;
    private object _indexFingerEnumValue;
    private Type _ovrInputType;
    private MethodInfo _ovrInputGetButtonMethod;
    private object _ovrPrimaryHandTriggerButton;
    private object _ovrSecondaryHandTriggerButton;
    private Transform _leftIndexTip;
    private Transform _leftThumbTip;
    private Transform _rightIndexTip;
    private Transform _rightThumbTip;

    public void SetReferences(Camera worldCamera, Button button)
    {
        eventCamera = worldCamera;
        targetButton = button;
    }

    private void Awake()
    {
        _raycaster = GetComponent<GraphicRaycaster>();
        EnsureEventCamera();
        EnsureDebugText();
    }

    private void Update()
    {
        EnsureDebugText();
        UpdateDebugVisibility();

        if (targetButton != null && (!targetButton.gameObject.activeInHierarchy || !targetButton.interactable))
            return;

        ResolveHandsIfNeeded();

        var rightReady = IsHandReady(_rightHand, _rightIndexTip, _rightThumbTip, out var rightPinchDistance);
        var leftReady = IsHandReady(_leftHand, _leftIndexTip, _leftThumbTip, out var leftPinchDistance);
        ApplyOvrInputPinchFallback(ref leftReady, ref leftPinchDistance, ref rightReady, ref rightPinchDistance);

        var useRight = preferRightHand ? rightReady || !leftReady : rightReady && !leftReady;
        var indexTip = useRight && rightReady ? _rightIndexTip : leftReady ? _leftIndexTip : null;
        var anchor = useRight && rightReady ? _rightAnchor : _leftAnchor;
        var pinchDistance = useRight && rightReady ? rightPinchDistance : leftPinchDistance;
        if (pinchDistance < 0f)
        {
            if (Time.time >= _nextDiagnosticTime)
            {
                ShowDebug("No pinch input: hand tracking/tips missing.");
                _nextDiagnosticTime = Time.time + diagnosticMessageInterval;
            }
            TryGazeDwellSelect();
            return;
        }

        var isPinching = pinchDistance <= pinchDownDistanceMeters;
        var hasReleased = pinchDistance >= pinchUpDistanceMeters;
        if (!isPinching && hasReleased)
            _wasPinching = false;

        if (!isPinching || _wasPinching)
        {
            TryGazeDwellSelect();
            return;
        }
        ShowDebug("PINCH DETECTED");
        if (Time.time - _lastClickTime < clickCooldownSeconds)
            return;

        var clickPosition = indexTip != null
            ? indexTip.position
            : anchor != null
                ? anchor.position
                : targetButton != null
                    ? targetButton.transform.position
                    : transform.position;
        if (TryClickAtWorldPosition(clickPosition, out var clickedButtonName))
        {
            ShowDebug($"Pinch click: {clickedButtonName}");
            _wasPinching = true;
            _lastClickTime = Time.time;
        }
        else
        {
            ShowDebug("Pinch detected, but button hit failed.");
        }
    }

    private void TryGazeDwellSelect()
    {
        if (!enableGazeDwellFallback)
            return;
        if (Time.time - _lastClickTime < clickCooldownSeconds)
            return;

        var lookedButton = FindLookedButton();
        if (lookedButton == null)
        {
            _gazeTargetButton = null;
            return;
        }

        if (_gazeTargetButton != lookedButton)
        {
            _gazeTargetButton = lookedButton;
            _gazeStartTime = Time.time;
        }

        var elapsed = Time.time - _gazeStartTime;
        ShowDebug($"Look to select {lookedButton.name}: {Mathf.Clamp01(elapsed / gazeDwellSeconds) * 100f:0}%");
        if (elapsed < gazeDwellSeconds)
            return;

        lookedButton.onClick.Invoke();
        _lastClickTime = Time.time;
        _gazeTargetButton = null;
        ShowDebug($"Gaze selected: {lookedButton.name}");
    }

    private void ResolveHandsIfNeeded()
    {
        if (_leftHand != null && _rightHand != null && _leftIndexTip != null && _rightIndexTip != null)
            return;

        if (leftHandOverride != null)
        {
            _leftHand = leftHandOverride;
            _leftAnchor = leftHandOverride.transform;
        }
        if (rightHandOverride != null)
        {
            _rightHand = rightHandOverride;
            _rightAnchor = rightHandOverride.transform;
        }

        if (_leftHand == null)
            _leftHand = FindOvrHandComponent(new[] { "LeftHandAnchor", "LeftHand", "LHandAnchor" }, out _leftAnchor);
        if (_rightHand == null)
            _rightHand = FindOvrHandComponent(new[] { "RightHandAnchor", "RightHand", "RHandAnchor" }, out _rightAnchor);
        ResolveFingerTips(_leftAnchor, ref _leftIndexTip, ref _leftThumbTip);
        ResolveFingerTips(_rightAnchor, ref _rightIndexTip, ref _rightThumbTip);

        var sample = _leftHand != null ? _leftHand : _rightHand;
        if (sample == null)
            return;

        var handType = sample.GetType();
        _isTrackedProperty = handType.GetProperty("IsTracked", BindingFlags.Public | BindingFlags.Instance);
        _isHighConfidenceProperty = handType.GetProperty("IsDataHighConfidence", BindingFlags.Public | BindingFlags.Instance);
        var fingerType = handType.GetNestedType("HandFinger", BindingFlags.Public);
        if (fingerType != null)
        {
            _indexFingerEnumValue = Enum.Parse(fingerType, "Index");
            _getFingerIsPinchingMethod = handType.GetMethod("GetFingerIsPinching", new[] { fingerType });
            _getFingerPinchStrengthMethod = handType.GetMethod("GetFingerPinchStrength", new[] { fingerType });
        }

        ResolveOvrInputFallbackApi();
    }

    private void ResolveOvrInputFallbackApi()
    {
        if (_ovrInputGetButtonMethod != null)
            return;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("OVRInput");
            if (t == null)
                continue;

            var buttonType = t.GetNestedType("Button", BindingFlags.Public);
            if (buttonType == null)
                continue;

            var getMethod = t.GetMethod("Get", new[] { buttonType });
            if (getMethod == null || getMethod.ReturnType != typeof(bool))
                continue;

            try
            {
                _ovrPrimaryHandTriggerButton = Enum.Parse(buttonType, "PrimaryHandTrigger");
                _ovrSecondaryHandTriggerButton = Enum.Parse(buttonType, "SecondaryHandTrigger");
                _ovrInputType = t;
                _ovrInputGetButtonMethod = getMethod;
                return;
            }
            catch
            {
                // ignore and keep searching
            }
        }
    }

    private void ApplyOvrInputPinchFallback(
        ref bool leftReady,
        ref float leftPinchDistance,
        ref bool rightReady,
        ref float rightPinchDistance)
    {
        if (_ovrInputGetButtonMethod == null)
            ResolveOvrInputFallbackApi();
        if (_ovrInputGetButtonMethod == null)
            return;

        try
        {
            if (!leftReady && _ovrPrimaryHandTriggerButton != null)
            {
                var pressed = (bool)_ovrInputGetButtonMethod.Invoke(null, new[] { _ovrPrimaryHandTriggerButton });
                if (pressed)
                {
                    leftReady = true;
                    leftPinchDistance = pinchDownDistanceMeters * 0.5f;
                }
            }

            if (!rightReady && _ovrSecondaryHandTriggerButton != null)
            {
                var pressed = (bool)_ovrInputGetButtonMethod.Invoke(null, new[] { _ovrSecondaryHandTriggerButton });
                if (pressed)
                {
                    rightReady = true;
                    rightPinchDistance = pinchDownDistanceMeters * 0.5f;
                }
            }
        }
        catch
        {
            // Ignore fallback errors and keep base behavior.
        }
    }

    private bool IsHandReady(Component hand, Transform indexTip, Transform thumbTip, out float pinchDistance)
    {
        pinchDistance = -1f;
        if (!IsHandTracked(hand))
            return false;

        if (indexTip != null && thumbTip != null)
        {
            pinchDistance = Vector3.Distance(indexTip.position, thumbTip.position);
            return true;
        }

        // Fallback when finger tip transforms are unavailable in this rig.
        if (_indexFingerEnumValue != null && _getFingerIsPinchingMethod != null)
        {
            try
            {
                var isPinchingResult = _getFingerIsPinchingMethod.Invoke(hand, new[] { _indexFingerEnumValue });
                if (isPinchingResult is bool isPinching && isPinching)
                {
                    pinchDistance = pinchDownDistanceMeters * 0.5f;
                    return true;
                }
            }
            catch
            {
                // ignore and keep trying strength fallback
            }
        }

        if (_indexFingerEnumValue != null && _getFingerPinchStrengthMethod != null)
        {
            try
            {
                var strengthResult = _getFingerPinchStrengthMethod.Invoke(hand, new[] { _indexFingerEnumValue });
                if (strengthResult is float strength)
                {
                    // Convert strength into pseudo distance so existing threshold logic remains usable.
                    pinchDistance = Mathf.Lerp(pinchUpDistanceMeters + 0.01f, pinchDownDistanceMeters * 0.5f, Mathf.Clamp01(strength));
                    return true;
                }
            }
            catch
            {
                // ignore
            }
        }

        pinchDistance = pinchUpDistanceMeters + 0.02f;
        return true;
    }

    private Button FindLookedButton()
    {
        if (!EnsureEventCamera())
            return null;

        // Primary path: UI raycast when available.
        if (_raycaster == null)
            _raycaster = GetComponent<GraphicRaycaster>();
        if (_eventSystem == null)
            _eventSystem = EventSystem.current;
        if (_raycaster != null && _eventSystem != null)
        {
            _pointerEventData ??= new PointerEventData(_eventSystem);
            _pointerEventData.Reset();
            _pointerEventData.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _pointerEventData.button = PointerEventData.InputButton.Left;

            var results = new System.Collections.Generic.List<RaycastResult>(8);
            _raycaster.Raycast(_pointerEventData, results);
            foreach (var result in results)
            {
                if (result.gameObject == null)
                    continue;
                var button = result.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                    continue;
                if (targetButton != null && button != targetButton)
                    continue;
                return button;
            }
        }

        // Fallback path: choose nearest visible button to center reticle.
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var nearest = FindNearestVisibleButton(center, float.MaxValue);
        if (nearest != null)
            ShowDebug($"Gaze target: {nearest.name}");
        return nearest;
    }

    private Button FindNearestVisibleButton(Vector2 screenPoint, float maxDistance)
    {
        var buttons = GetComponentsInChildren<Button>(true);
        Button best = null;
        var bestDistance = float.MaxValue;
        foreach (var button in buttons)
        {
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                continue;

            var rect = button.transform as RectTransform;
            if (rect == null)
                continue;

            var center = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.position);
            var distance = Vector2.Distance(screenPoint, center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = button;
            }
        }

        return bestDistance <= maxDistance ? best : null;
    }

    private bool TryClickAtWorldPosition(Vector3 worldPosition, out string clickedButtonName)
    {
        clickedButtonName = string.Empty;

        if (_raycaster == null)
            _raycaster = GetComponent<GraphicRaycaster>();
        if (_eventSystem == null)
            _eventSystem = EventSystem.current;

        if (!EnsureEventCamera())
            return false;

        var screenPoint = eventCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
            return false;

        // Primary path: standard UI raycast.
        if (_raycaster != null && _eventSystem != null)
        {
            _pointerEventData ??= new PointerEventData(_eventSystem);
            _pointerEventData.Reset();
            _pointerEventData.position = new Vector2(screenPoint.x, screenPoint.y);
            _pointerEventData.button = PointerEventData.InputButton.Left;

            var results = new System.Collections.Generic.List<RaycastResult>(8);
            _raycaster.Raycast(_pointerEventData, results);
            foreach (var result in results)
            {
                if (result.gameObject == null)
                    continue;

                var button = result.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                    continue;
                if (targetButton != null && button != targetButton)
                    continue;

                ExecuteEvents.Execute(button.gameObject, _pointerEventData, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, _pointerEventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, _pointerEventData, ExecuteEvents.pointerClickHandler);
                clickedButtonName = button.gameObject.name;
                return true;
            }
        }

        // Fallback path: nearest button by projected distance.
        var fallback = FindNearestVisibleButton(new Vector2(screenPoint.x, screenPoint.y), float.MaxValue);
        if (fallback != null)
        {
            fallback.onClick.Invoke();
            clickedButtonName = fallback.gameObject.name;
            return true;
        }

        return false;
    }

    private void EnsureDebugText()
    {
        if (!enableDebugOverlay || debugText != null)
            return;

        var debugObject = new GameObject("PinchDebugText", typeof(RectTransform));
        var rect = debugObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -170f);
        rect.sizeDelta = new Vector2(860f, 52f);

        debugText = debugObject.AddComponent<TextMeshProUGUI>();
        debugText.text = string.Empty;
        debugText.fontSize = 24f;
        debugText.alignment = TextAlignmentOptions.Center;
        debugText.color = new Color(1f, 0.95f, 0.42f, 0.95f);
        debugText.raycastTarget = false;
        debugText.gameObject.SetActive(false);
    }

    private void ShowDebug(string message)
    {
        if (!enableDebugOverlay || debugText == null)
            return;

        debugText.text = message;
        debugText.gameObject.SetActive(true);
        _debugHideAt = Time.time + debugMessageDuration;
    }

    private void UpdateDebugVisibility()
    {
        if (!enableDebugOverlay || debugText == null)
            return;

        if (debugText.gameObject.activeSelf && Time.time >= _debugHideAt)
            debugText.gameObject.SetActive(false);
    }

    private bool IsHandTracked(Component handComponent)
    {
        if (handComponent == null)
            return false;

        var tracked = ReadBoolProperty(handComponent, _isTrackedProperty, true);
        var highConfidence = ReadBoolProperty(handComponent, _isHighConfidenceProperty, true);
        return tracked && highConfidence;
    }

    private static Component FindOvrHandComponent(string[] anchorNames, out Transform anchor)
    {
        foreach (var component in UnityEngine.Object.FindObjectsOfType<Component>(true))
        {
            if (component != null && component.GetType().Name == "OVRHand")
            {
                var lowerName = component.name.ToLowerInvariant();
                if (anchorNames == null || anchorNames.Length == 0)
                {
                    anchor = component.transform;
                    return component;
                }

                foreach (var anchorName in anchorNames)
                {
                    if (lowerName.Contains(anchorName.ToLowerInvariant().Replace("anchor", string.Empty)))
                    {
                        anchor = component.transform;
                        return component;
                    }
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
        {
            foreach (var t in candidates)
            {
                var n = t.name;
                if (n.IndexOf("IndexTip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hand_IndexTip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    indexTip = t;
                    break;
                }
            }
        }

        if (thumbTip == null)
        {
            foreach (var t in candidates)
            {
                var n = t.name;
                if (n.IndexOf("ThumbTip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hand_ThumbTip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    thumbTip = t;
                    break;
                }
            }
        }
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

    private bool EnsureEventCamera()
    {
        if (eventCamera != null && eventCamera.gameObject.activeInHierarchy && eventCamera.enabled)
            return true;

        if (Camera.main != null && Camera.main.gameObject.activeInHierarchy && Camera.main.enabled)
        {
            eventCamera = Camera.main;
            return true;
        }

        var cameras = Camera.allCameras;
        for (var i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            if (cam == null || !cam.gameObject.activeInHierarchy || !cam.enabled)
                continue;

            eventCamera = cam;
            return true;
        }

        return false;
    }
}
