using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space Canvas: XRI의 TrackedDeviceGraphicRaycaster를 우선 설치하고,
/// 패키지가 없으면 OVRRaycaster로 폴백합니다. (기본 GraphicRaycaster는 제거)
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class CanvasInstallOvrRaycaster : MonoBehaviour
{
    private void Awake()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            return;

        var trackedType = FindTrackedDeviceGraphicRaycasterType();
        var ovrType = FindOvrRaycasterType();
        var preferred = trackedType ?? ovrType;
        if (preferred == null)
        {
            Debug.LogWarning(
                "[CanvasInstallOvrRaycaster] TrackedDeviceGraphicRaycaster / OVRRaycaster 둘 다 찾지 못했습니다.");
            return;
        }

        if (GetComponent(preferred) != null)
            return;

        foreach (var gr in GetComponents<GraphicRaycaster>())
        {
            if (gr != null)
                DestroyImmediate(gr);
        }

        gameObject.AddComponent(preferred);
    }

    private static Type FindTrackedDeviceGraphicRaycasterType()
    {
        var name = "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit";
        var t = Type.GetType(name);
        if (t != null)
            return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Unity.XR.Interaction.Toolkit")
                continue;
            try
            {
                foreach (var et in asm.GetExportedTypes())
                {
                    if (et.Name == "TrackedDeviceGraphicRaycaster" && et.IsSubclassOf(typeof(GraphicRaycaster)))
                        return et;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // ignore
            }
        }

        return null;
    }

    private static Type FindOvrRaycasterType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEngine.UI.OVRRaycaster");
            if (t != null)
                return t;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.FullName == null ||
                (!asm.FullName.Contains("Oculus") && !asm.FullName.Contains("Meta")))
                continue;

            try
            {
                foreach (var t in asm.GetExportedTypes())
                {
                    if (t.Name == "OVRRaycaster" && t.IsSubclassOf(typeof(GraphicRaycaster)))
                        return t;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // ignore
            }
        }

        return null;
    }
}
