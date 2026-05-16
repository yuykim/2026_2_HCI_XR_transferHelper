using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class RoutePathRenderer : MonoBehaviour
{
    [SerializeField] private Transform[] routePoints;
    [SerializeField] private float routeHeight = 0.10f;
    [SerializeField] private bool useRoutePointY = true;
    [SerializeField] private Color upcomingRouteStartColor = new Color(0.55f, 1f, 1f, 1f);
    [SerializeField] private Color upcomingRouteEndColor = new Color(0.05f, 0.92f, 1f, 1f);
    [SerializeField] private Color routeUnderlayColor = new Color(0.05f, 0.55f, 1f, 0.32f);
    [SerializeField] private float routeUnderlayWidthMultiplier = 2.8f;
    [SerializeField] private Color traveledRouteColor = new Color(0.02f, 0.36f, 0.16f, 0.95f);

    private LineRenderer _lineRenderer;
    private LineRenderer _underlayLineRenderer;
    private LineRenderer _traveledLineRenderer;
    private Material _underlayMaterial;
    private Material _traveledMaterial;
    private bool _hasProgressSplit;
    private int _progressTargetIndex = 1;
    private Vector3 _progressUserPosition;

    public Transform[] RoutePoints => routePoints;

    private void Awake()
    {
        EnsureLineRenderer(true);
        Refresh();
    }

    private void OnValidate()
    {
        EnsureLineRenderer(false);
        Refresh(false);
    }

    public void SetRoutePoints(Transform[] points)
    {
        routePoints = points;
        ClearProgress();
        Refresh();
    }

    public void SetProgress(int targetIndex, Vector3 userPosition)
    {
        _progressTargetIndex = Mathf.Max(1, targetIndex);
        _progressUserPosition = userPosition;
        _hasProgressSplit = true;
        Refresh();
    }

    public void ClearProgress()
    {
        _hasProgressSplit = false;
        if (_traveledLineRenderer != null)
            _traveledLineRenderer.positionCount = 0;
    }

    public void Refresh()
    {
        Refresh(true);
    }

    private void Refresh(bool createIfMissing)
    {
        var lineRenderer = GetLineRenderer(createIfMissing);
        if (lineRenderer == null)
            return;

        EnsureLineRenderer(createIfMissing);
        EnsureUnderlayLineRenderer(createIfMissing);
        EnsureTraveledLineRenderer(createIfMissing && _hasProgressSplit);

        if (routePoints == null || routePoints.Length == 0)
        {
            lineRenderer.positionCount = 0;
            if (_underlayLineRenderer != null)
                _underlayLineRenderer.positionCount = 0;
            if (_traveledLineRenderer != null)
                _traveledLineRenderer.positionCount = 0;
            return;
        }

        if (!_hasProgressSplit || routePoints.Length < 2)
        {
            var positions = BuildRoutePositions(0, routePoints.Length - 1);
            SetLinePositions(lineRenderer, positions);
            SetLinePositions(_underlayLineRenderer, positions);
            if (_traveledLineRenderer != null)
                _traveledLineRenderer.positionCount = 0;
            return;
        }

        RefreshSplitRoute(lineRenderer);
    }

    private void RefreshSplitRoute(LineRenderer upcomingRenderer)
    {
        if (_traveledLineRenderer == null)
            return;

        var lastIndex = routePoints.Length - 1;
        var targetIndex = Mathf.Clamp(_progressTargetIndex, 1, lastIndex);
        var segmentStartIndex = Mathf.Clamp(targetIndex - 1, 0, lastIndex - 1);
        var segmentEndIndex = segmentStartIndex + 1;
        var segmentStart = GetRoutePosition(segmentStartIndex);
        var segmentEnd = GetRoutePosition(segmentEndIndex);
        var splitPosition = ProjectPointOnSegmentXZ(_progressUserPosition, segmentStart, segmentEnd);

        var traveledPositions = new List<Vector3>(segmentStartIndex + 2);
        for (var i = 0; i <= segmentStartIndex; i++)
            traveledPositions.Add(GetRoutePosition(i));
        traveledPositions.Add(splitPosition);

        var upcomingPositions = new List<Vector3>(routePoints.Length - segmentStartIndex);
        upcomingPositions.Add(splitPosition);
        for (var i = segmentEndIndex; i < routePoints.Length; i++)
            upcomingPositions.Add(GetRoutePosition(i));

        SetLinePositions(_traveledLineRenderer, traveledPositions);
        SetLinePositions(upcomingRenderer, upcomingPositions);
        SetLinePositions(_underlayLineRenderer, upcomingPositions);
    }

    private List<Vector3> BuildRoutePositions(int startIndex, int endIndex)
    {
        var positions = new List<Vector3>(Mathf.Max(0, endIndex - startIndex + 1));
        for (var i = startIndex; i <= endIndex; i++)
            positions.Add(GetRoutePosition(i));
        return positions;
    }

    private Vector3 GetRoutePosition(int index)
    {
        if (routePoints[index] == null)
            return transform.position;

        var position = routePoints[index].position;
        if (!useRoutePointY)
            position.y = routeHeight;

        return position;
    }

    private void SetLinePositions(LineRenderer lineRenderer, List<Vector3> positions)
    {
        if (lineRenderer == null)
            return;

        if (positions == null || positions.Count < 2 || CalculatePathLength(positions) <= 0.01f)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = positions.Count;
        for (var i = 0; i < positions.Count; i++)
        {
            var position = positions[i];
            if (!lineRenderer.useWorldSpace)
                position = transform.InverseTransformPoint(position);

            lineRenderer.SetPosition(i, position);
        }
    }

    private void EnsureLineRenderer(bool createIfMissing)
    {
        var lineRenderer = GetLineRenderer(createIfMissing);
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = false;
        lineRenderer.widthMultiplier = Mathf.Max(lineRenderer.widthMultiplier, 0.08f);
        lineRenderer.numCornerVertices = Mathf.Max(lineRenderer.numCornerVertices, 6);
        lineRenderer.numCapVertices = Mathf.Max(lineRenderer.numCapVertices, 6);
        lineRenderer.colorGradient = CreateGradient(upcomingRouteStartColor, upcomingRouteEndColor);
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    private void EnsureUnderlayLineRenderer(bool createIfMissing)
    {
        if (_underlayLineRenderer == null)
        {
            var existing = transform.Find("RouteUnderlayLine");
            if (existing != null)
                _underlayLineRenderer = existing.GetComponent<LineRenderer>();
        }

        if (_underlayLineRenderer == null && createIfMissing)
        {
            var underlayObject = new GameObject("RouteUnderlayLine");
            underlayObject.transform.SetParent(transform, false);
            underlayObject.transform.SetAsFirstSibling();
            _underlayLineRenderer = underlayObject.AddComponent<LineRenderer>();
        }

        if (_underlayLineRenderer == null)
            return;

        CopyLineRendererStyle(_underlayLineRenderer, GetLineRenderer(false));
        _underlayLineRenderer.widthMultiplier *= Mathf.Max(1f, routeUnderlayWidthMultiplier);
        _underlayLineRenderer.colorGradient = CreateSolidGradient(routeUnderlayColor);
        _underlayLineRenderer.sharedMaterial = GetLineMaterial(ref _underlayMaterial, routeUnderlayColor);
        _underlayLineRenderer.sortingOrder = -1;
        _underlayLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _underlayLineRenderer.receiveShadows = false;
        _underlayLineRenderer.loop = false;
    }

    private void EnsureTraveledLineRenderer(bool createIfMissing)
    {
        if (_traveledLineRenderer == null)
        {
            var existing = transform.Find("TraveledRouteLine");
            if (existing != null)
                _traveledLineRenderer = existing.GetComponent<LineRenderer>();
        }

        if (_traveledLineRenderer == null && createIfMissing)
        {
            var traveledObject = new GameObject("TraveledRouteLine");
            traveledObject.transform.SetParent(transform, false);
            _traveledLineRenderer = traveledObject.AddComponent<LineRenderer>();
        }

        if (_traveledLineRenderer == null)
            return;

        CopyLineRendererStyle(_traveledLineRenderer, GetLineRenderer(false));
        _traveledLineRenderer.sharedMaterial = GetLineMaterial(ref _traveledMaterial, traveledRouteColor);
        _traveledLineRenderer.colorGradient = CreateSolidGradient(traveledRouteColor);
        _traveledLineRenderer.sortingOrder = 1;
        _traveledLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _traveledLineRenderer.receiveShadows = false;
        _traveledLineRenderer.loop = false;
    }

    private LineRenderer GetLineRenderer(bool createIfMissing)
    {
        // Cached references can become stale after scene rebuild/editor object replacement.
        if (_lineRenderer == null || _lineRenderer.gameObject != gameObject)
            _lineRenderer = GetComponent<LineRenderer>();

        if (_lineRenderer == null && createIfMissing)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();

        return _lineRenderer;
    }

    private static void CopyLineRendererStyle(LineRenderer target, LineRenderer source)
    {
        if (target == null || source == null)
            return;

        target.useWorldSpace = source.useWorldSpace;
        target.widthMultiplier = Mathf.Max(source.widthMultiplier, 0.08f);
        target.widthCurve = source.widthCurve;
        target.numCornerVertices = source.numCornerVertices;
        target.numCapVertices = source.numCapVertices;
        target.alignment = source.alignment;
        target.textureMode = source.textureMode;
        target.textureScale = source.textureScale;
    }

    private static Material GetLineMaterial(ref Material material, Color color)
    {
        if (material != null)
        {
            material.color = color;
            return material;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader);
        material.name = "Runtime Route Line";
        material.color = color;
        material.renderQueue = 3000;
        return material;
    }

    private static Vector3 ProjectPointOnSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
    {
        var point2 = new Vector2(point.x, point.z);
        var start2 = new Vector2(start.x, start.z);
        var end2 = new Vector2(end.x, end.z);
        var segment = end2 - start2;

        if (segment.sqrMagnitude <= Mathf.Epsilon)
            return start;

        var t = Mathf.Clamp01(Vector2.Dot(point2 - start2, segment) / segment.sqrMagnitude);
        return Vector3.Lerp(start, end, t);
    }

    private static float CalculatePathLength(List<Vector3> positions)
    {
        var length = 0f;
        for (var i = 0; i < positions.Count - 1; i++)
            length += Vector3.Distance(positions[i], positions[i + 1]);
        return length;
    }

    private static Gradient CreateSolidGradient(Color color)
    {
        return CreateGradient(color, color);
    }

    private static Gradient CreateGradient(Color start, Color end)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(end.a, 1f)
            });
        return gradient;
    }
}
