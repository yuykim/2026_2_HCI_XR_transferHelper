using System;
using System.Reflection;
using UnityEngine;

public sealed class PalmHudFollower : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool preferRightHand = true;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.05f, 0.02f);
    [SerializeField] private float smoothSpeed = 12f;
    [SerializeField] private bool hideWhenHandMissing = true;

    private Transform _handAnchor;
    private Component _ovrHandComponent;
    private PropertyInfo _isTrackedProperty;
    private PropertyInfo _isHighConfidenceProperty;
    private float _lastSeenHandTime;

    public void SetCamera(Transform targetCamera)
    {
        cameraTransform = targetCamera;
    }

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_handAnchor == null)
            ResolveHandAnchor();

        var hasTracking = _handAnchor != null && IsHandTracked();
        if (hasTracking)
            _lastSeenHandTime = Time.time;

        var shouldShow = hasTracking || !hideWhenHandMissing || Time.time - _lastSeenHandTime < 0.35f;
        gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        var targetPosition = _handAnchor != null
            ? _handAnchor.TransformPoint(localOffset)
            : transform.position;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);

        if (cameraTransform != null)
        {
            var toCamera = transform.position - cameraTransform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }
    }

    private void ResolveHandAnchor()
    {
        var rightNames = new[] { "RightHandAnchor", "RightHand", "RHandAnchor" };
        var leftNames = new[] { "LeftHandAnchor", "LeftHand", "LHandAnchor" };

        if (TryFindAnchorByNames(preferRightHand ? rightNames : leftNames, out var found))
        {
            BindPossibleOvrHand(found);
            _handAnchor = found;
            return;
        }

        if (TryFindAnchorByNames(preferRightHand ? leftNames : rightNames, out found))
        {
            BindPossibleOvrHand(found);
            _handAnchor = found;
        }
    }

    private bool TryFindAnchorByNames(string[] candidates, out Transform found)
    {
        foreach (var candidate in candidates)
        {
            var objectByName = GameObject.Find(candidate);
            if (objectByName == null)
                continue;
            found = objectByName.transform;
            return true;
        }

        found = null;
        return false;
    }

    private void BindPossibleOvrHand(Transform anchor)
    {
        _ovrHandComponent = null;
        _isTrackedProperty = null;
        _isHighConfidenceProperty = null;

        if (anchor == null)
            return;

        foreach (var component in anchor.GetComponentsInParent<Component>(true))
        {
            if (component == null)
                continue;

            var type = component.GetType();
            if (type.Name != "OVRHand")
                continue;

            _ovrHandComponent = component;
            _isTrackedProperty = type.GetProperty("IsTracked", BindingFlags.Public | BindingFlags.Instance);
            _isHighConfidenceProperty = type.GetProperty("IsDataHighConfidence", BindingFlags.Public | BindingFlags.Instance);
            return;
        }
    }

    private bool IsHandTracked()
    {
        if (_ovrHandComponent == null)
            return true;

        var tracked = ReadBoolProperty(_ovrHandComponent, _isTrackedProperty, true);
        var highConfidence = ReadBoolProperty(_ovrHandComponent, _isHighConfidenceProperty, true);
        return tracked && highConfidence;
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
}
