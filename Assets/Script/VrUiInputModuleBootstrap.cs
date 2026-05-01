using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

/// <summary>
/// Quest(Android)에서는 OVRInputModule을 우선 사용하고, 그 외 환경은 XRUIInputModule을 우선 사용합니다.
/// (YUYKIM 씬 EventSystem)
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class VrUiInputModuleBootstrap : MonoBehaviour
{
    private void Awake()
    {
        if (!ShouldPatchInputModule())
            return;

        var es = GetComponent<EventSystem>();
        if (es == null)
            return;

        var xrUiType = FindXrUiInputModuleType();
        var ovrType = FindOvrInputModuleType();
        var preferred = SelectPreferredInputModuleType(xrUiType, ovrType);
        if (preferred == null)
        {
            Debug.LogWarning(
                "[VrUiInputModuleBootstrap] XRUIInputModule / OVRInputModule 둘 다 찾지 못했습니다. XRI 패키지 또는 Meta XR SDK를 확인하세요.");
            return;
        }

        foreach (var module in es.gameObject.GetComponents<BaseInputModule>())
        {
            if (module != null && module.GetType() == preferred)
                return;
        }

        foreach (var module in es.gameObject.GetComponents<BaseInputModule>())
        {
            if (module != null)
                DestroyImmediate(module);
        }

        es.gameObject.AddComponent(preferred);
    }

    private static Type SelectPreferredInputModuleType(Type xrUiType, Type ovrType)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return ovrType ?? xrUiType;
#else
        return xrUiType ?? ovrType;
#endif
    }

    private static bool ShouldPatchInputModule()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return XRSettings.enabled;
#endif
    }

    private static Type FindXrUiInputModuleType()
    {
        var name = "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit";
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
                    if (et.Name == "XRUIInputModule" && et.IsSubclassOf(typeof(BaseInputModule)))
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

    private static Type FindOvrInputModuleType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEngine.EventSystems.OVRInputModule");
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
                    if (t.Name == "OVRInputModule" && t.IsSubclassOf(typeof(BaseInputModule)))
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
