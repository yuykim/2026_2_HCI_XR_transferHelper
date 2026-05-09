using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class RoutePathRenderer : MonoBehaviour
{
    [SerializeField] private Transform[] routePoints;
    [SerializeField] private float routeHeight = 0.10f;
    [SerializeField] private bool useRoutePointY = true;

    private LineRenderer _lineRenderer;

    public Transform[] RoutePoints => routePoints;

    private void Awake()
    {
        EnsureLineRenderer(true);
        Refresh();
    }

    private void OnValidate()
    {
        EnsureLineRenderer(false);
        Refresh();
    }

    public void SetRoutePoints(Transform[] points)
    {
        routePoints = points;
        Refresh();
    }

    public void Refresh()
    {
        var lineRenderer = GetLineRenderer(true);
        if (lineRenderer == null)
            return;

        if (routePoints == null || routePoints.Length == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = routePoints.Length;

        for (var i = 0; i < routePoints.Length; i++)
        {
            if (routePoints[i] == null)
            {
                lineRenderer.SetPosition(i, transform.position);
                continue;
            }

            var position = routePoints[i].position;
            if (!useRoutePointY)
                position.y = routeHeight;

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
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
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
}
