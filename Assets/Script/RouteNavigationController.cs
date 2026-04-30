using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RouteNavigationController : MonoBehaviour
{
    [Header("Route")]
    [SerializeField] private Transform userTransform;
    [SerializeField] private Transform[] routePoints;
    [SerializeField] private RoutePathRenderer routePathRenderer;
    [SerializeField] private float pointReachRadius = 0.35f;
    [SerializeField] private float routeDeviationThreshold = 0.75f;
    [SerializeField] private float walkingSpeedMetersPerSecond = 1.2f;

    [Header("UI")]
    [SerializeField] private GameObject destinationSelectPanel;
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
    }

    private void Start()
    {
        SetWarning(false);
        SetArrived(false);
        SetStatus("Choose destination A.");
        UpdateRemainingDistanceText(0f);
        UpdateProgress(0f);
        SetDirectionArrow(false, false, 0f);
    }

    private void Update()
    {
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
        if (routePoints == null || routePoints.Length < 2)
        {
            SetStatus("Route points are not assigned.");
            return;
        }

        _navigationStarted = true;
        _arrived = false;
        _targetIndex = 1;
        _totalRouteDistance = CalculateRouteDistance();

        if (destinationSelectPanel != null)
            destinationSelectPanel.SetActive(false);

        SetArrived(false);
        SetWarning(false);
        SetStatus("Follow the yellow arrow.");
        SetDirectionArrow(true, false, 0f);

        if (routePathRenderer != null)
            routePathRenderer.Refresh();
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
            remainingDistanceText.text = $"Remaining: {Mathf.CeilToInt(meters)}m";

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
