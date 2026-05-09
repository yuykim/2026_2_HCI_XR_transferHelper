using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GazeRouteSelector : MonoBehaviour
{
    [SerializeField] private Camera eventCamera;
    [SerializeField] private Button routeAButton;
    [SerializeField] private Button routeBButton;
    [SerializeField] private Button routeCButton;
    [SerializeField] private float dwellSeconds = 1.0f;
    [SerializeField] private float selectionCooldownSeconds = 0.5f;
    [SerializeField] private float maxCenterDistancePixels = 2000f;
    [SerializeField] private TMP_Text debugText;

    private Button _currentTarget;
    private float _targetStartTime;
    private float _lastSelectionTime = -10f;

    public void SetReferences(Camera worldCamera, Button buttonA, Button buttonB, Button buttonC)
    {
        eventCamera = worldCamera;
        routeAButton = buttonA;
        routeBButton = buttonB;
        routeCButton = buttonC;
    }

    private void Awake()
    {
        EnsureEventCamera();
        EnsureDebugText();
        ApplyBaseButtonColors(null);
    }

    private void Update()
    {
        if (!EnsureEventCamera())
        {
            ShowStatus("No event camera.");
            return;
        }
        EnsureButtonReferences();
        EnsureDebugText();
        ApplyBaseButtonColors(_currentTarget);

        if (Time.time - _lastSelectionTime < selectionCooldownSeconds)
            return;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var nearest = FindNearestButton(center, out var nearestDistance);
        if (nearest == null)
        {
            _currentTarget = null;
            ApplyBaseButtonColors(null);
            ShowStatus("No route in center.");
            return;
        }

        if (_currentTarget != nearest)
        {
            _currentTarget = nearest;
            _targetStartTime = Time.time;
        }

        var progress = Mathf.Clamp01((Time.time - _targetStartTime) / Mathf.Max(0.01f, dwellSeconds));
        HighlightCurrentTarget(_currentTarget, progress);
        ShowStatus($"Look at {_currentTarget.name}: {(progress * 100f):0}% ({nearestDistance:0}px)");

        if (progress < 1f)
            return;

        _currentTarget.onClick.Invoke();
        _lastSelectionTime = Time.time;
        ShowStatus($"Selected: {_currentTarget.name}");
        _currentTarget = null;
    }

    private void EnsureButtonReferences()
    {
        if (routeAButton == null)
            routeAButton = FindButton("Destination_A_Button");
        if (routeBButton == null)
            routeBButton = FindButton("Destination_B_Button");
        if (routeCButton == null)
            routeCButton = FindButton("Destination_C_Button");
    }

    private Button FindButton(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private Button FindNearestButton(Vector2 screenCenter, out float nearestDistance)
    {
        Button best = null;
        var bestDistance = float.MaxValue;
        EvaluateButton(routeAButton, screenCenter, ref best, ref bestDistance);
        EvaluateButton(routeBButton, screenCenter, ref best, ref bestDistance);
        EvaluateButton(routeCButton, screenCenter, ref best, ref bestDistance);
        nearestDistance = bestDistance;
        return bestDistance <= maxCenterDistancePixels ? best : null;
    }

    private void EvaluateButton(Button button, Vector2 screenCenter, ref Button best, ref float bestDistance)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            return;

        var rect = button.transform as RectTransform;
        if (rect == null)
            return;

        var pos = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.position);
        var d = Vector2.Distance(screenCenter, pos);
        if (d < bestDistance)
        {
            bestDistance = d;
            best = button;
        }
    }

    private void ApplyBaseButtonColors(Button current)
    {
        SetButtonStyle(routeAButton, new Color(0.95f, 0.48f, 0.28f, 0.95f), current == routeAButton);
        SetButtonStyle(routeBButton, new Color(0.24f, 0.74f, 0.98f, 0.95f), current == routeBButton);
        SetButtonStyle(routeCButton, new Color(0.54f, 0.34f, 0.92f, 0.95f), current == routeCButton);
    }

    private void HighlightCurrentTarget(Button target, float progress)
    {
        if (target == null)
            return;

        var image = target.GetComponent<Image>();
        if (image == null)
            return;

        var pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 14f);
        image.color = Color.Lerp(image.color, Color.white, 0.55f + progress * 0.25f) * pulse;
        target.transform.localScale = Vector3.one * (1.03f + progress * 0.08f);
    }

    private static void SetButtonStyle(Button button, Color color, bool isCurrent)
    {
        if (button == null)
            return;
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = isCurrent ? Color.Lerp(color, Color.white, 0.55f) : color;
        button.transform.localScale = isCurrent ? Vector3.one * 1.08f : Vector3.one;
    }

    private void EnsureDebugText()
    {
        if (debugText != null)
            return;

        var debugObject = new GameObject("GazeDebugText", typeof(RectTransform));
        var rect = debugObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -165f);
        rect.sizeDelta = new Vector2(920f, 54f);

        debugText = debugObject.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 24f;
        debugText.alignment = TextAlignmentOptions.Center;
        debugText.color = new Color(1f, 0.95f, 0.4f, 0.96f);
        debugText.raycastTarget = false;
        debugText.text = "Look at A/B/C to select.";
    }

    private void ShowStatus(string message)
    {
        if (debugText != null)
            debugText.text = message;
    }

    private bool EnsureEventCamera()
    {
        if (eventCamera != null && eventCamera.enabled && eventCamera.gameObject.activeInHierarchy)
            return true;

        if (Camera.main != null && Camera.main.enabled && Camera.main.gameObject.activeInHierarchy)
        {
            eventCamera = Camera.main;
            return true;
        }

        var cameras = Camera.allCameras;
        for (var i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy)
                continue;
            eventCamera = cam;
            return true;
        }

        return false;
    }
}
