using TMPro;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NavigationPrototypeBuilder
{
    private const float RoutePointY = 0.10f;
    private const string TargetScenePath = "Assets/Scenes/YUYKIM2.unity";

    [MenuItem("Tools/XR Transfer Helper/Create Navigation Prototype")]
    public static void CreateNavigationPrototype()
    {
        CreateNavigationPrototypeInCurrentScene();
    }

    [MenuItem("Tools/XR Transfer Helper/Create YUYKIM2 VR UI Scene")]
    public static void CreateYuykim2VrUiScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var sourceScenePath = ResolveSourceScenePath();
        if (string.IsNullOrEmpty(sourceScenePath))
        {
            Debug.LogError("Cannot create YUYKIM2: no valid source scene found. Open a scene first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Single);
        DeleteIfExists("NavigationRoot");
        DeleteIfExists("RoutePoints");
        DeleteIfExists("LightRoute");
        DeleteIfExists("NavigationHUD");

        CreateNavigationPrototypeInCurrentScene();
        EditorSceneManager.SaveScene(scene, TargetScenePath);
        AssetDatabase.ImportAsset(TargetScenePath);
        EnsureYuykim2InBuildSettings();
        EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        Debug.Log("YUYKIM2 VR UI scene created at Assets/Scenes/YUYKIM2.unity.");
    }

    private static string ResolveSourceScenePath()
    {
        var activeScenePath = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
            return activeScenePath;

        const string fallbackMainScene = "Assets/Scenes/MAIN.unity";
        if (File.Exists(fallbackMainScene))
            return fallbackMainScene;

        return null;
    }

    private static void CreateNavigationPrototypeInCurrentScene()
    {
        var camera = Camera.main;

        var navigationRoot = GetOrCreate("NavigationRoot").transform;
        var routePointsRoot = GetOrCreate("RoutePoints", navigationRoot).transform;
        var routePoints = CreateRoutePoints(routePointsRoot);
        var lightRoute = GetOrCreate("LightRoute", navigationRoot);
        lightRoute.transform.localPosition = Vector3.zero;
        lightRoute.transform.localRotation = Quaternion.identity;
        lightRoute.transform.localScale = Vector3.one;
        SetupLineRenderer(lightRoute);
        var routePathRenderer = GetOrAddComponent<RoutePathRenderer>(lightRoute);

        AssignRoutePathRenderer(routePathRenderer, routePoints);

        var navigationController = GetOrAddComponent<RouteNavigationController>(navigationRoot.gameObject);
        var hud = CreateHud(camera);
        AssignNavigationController(navigationController, camera, routePoints, routePathRenderer, hud);
        CreatePalmHud(camera, hud);

        Selection.activeGameObject = navigationRoot.gameObject;
        EditorUtility.SetDirty(navigationRoot.gameObject);
        Debug.Log("Navigation prototype created. Press Play, choose Destination A, then follow LightRoute.");
    }

    private static void DeleteIfExists(string objectName)
    {
        var existing = GameObject.Find(objectName);
        if (existing != null)
            Object.DestroyImmediate(existing);
    }

    private static Transform[] CreateRoutePoints(Transform routePointsRoot)
    {
        var positions = new[]
        {
            new Vector3(0f, RoutePointY, 0f),
            new Vector3(0f, RoutePointY, 2.4f),
            new Vector3(0f, RoutePointY, 4.8f),
            new Vector3(2.2f, RoutePointY, 4.8f),
            new Vector3(4.4f, RoutePointY, 4.8f),
            new Vector3(4.4f, RoutePointY, 7.2f),
            new Vector3(4.4f, RoutePointY, 9.6f),
            new Vector3(6.6f, RoutePointY, 9.6f)
        };

        var points = new Transform[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var name = i == 0 ? "RP_00_Start" : $"RP_{i:00}";
            var point = GetOrCreate(name, routePointsRoot).transform;
            point.localPosition = positions[i];
            point.localRotation = Quaternion.identity;
            point.localScale = Vector3.one * 0.12f;

            var marker = point.GetComponent<SphereCollider>();
            if (marker == null)
                marker = point.gameObject.AddComponent<SphereCollider>();
            marker.isTrigger = true;
            marker.radius = 0.2f;

            points[i] = point;
        }

        return points;
    }

    private static LineRenderer SetupLineRenderer(GameObject lightRoute)
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(lightRoute);

        var lineRenderer = lightRoute.GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = Undo.AddComponent<LineRenderer>(lightRoute);
        if (lineRenderer == null)
        {
            Debug.LogError("Failed to add LineRenderer to LightRoute.");
            return null;
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.widthMultiplier = 0.08f;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.numCapVertices = 6;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.startColor = new Color(0.1f, 0.95f, 1f, 1f);
        lineRenderer.endColor = new Color(0.25f, 1f, 0.45f, 1f);

        var routeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/RouteMat.mat");
        if (routeMaterial != null)
            lineRenderer.sharedMaterial = routeMaterial;

        return lineRenderer;
    }

    private static HudReferences CreateHud(Camera camera)
    {
        var parent = (Transform)null;
        var hud = GetOrCreateUi("NavigationHUD", parent);
        var canvas = GetOrAddComponent<Canvas>(hud);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        if (hud.GetComponent<GraphicRaycaster>() == null)
            hud.AddComponent<GraphicRaycaster>();
        if (hud.GetComponent<CanvasInstallOvrRaycaster>() == null)
            hud.AddComponent<CanvasInstallOvrRaycaster>();

        var scaler = GetOrAddComponent<CanvasScaler>(hud);
        scaler.dynamicPixelsPerUnit = 14f;

        var canvasRect = hud.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1280f, 720f);
        if (camera != null)
        {
            var spawnPosition = camera.transform.position + camera.transform.forward * 1.4f;
            spawnPosition.y = camera.transform.position.y - 0.15f;
            hud.transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(hud.transform.position - camera.transform.position));
        }
        else
        {
            hud.transform.localPosition = new Vector3(0f, 1.4f, 1.8f);
            hud.transform.localRotation = Quaternion.identity;
        }
        hud.transform.localScale = Vector3.one * 0.0018f;

        var destinationPanel = CreatePanel("DestinationSelectPanel", canvasRect, new Vector2(0f, 240f), new Vector2(460f, 170f),
            new Color(0.02f, 0.04f, 0.06f, 0.78f));
        var destinationTitle = CreateText("Title", destinationPanel, "Destination Ready", 34, TextAlignmentOptions.Center);
        destinationTitle.fontStyle = FontStyles.Bold;
        Stretch(destinationTitle.rectTransform, 20f, 102f, 20f, 16f);
        var button = CreateButton("Destination_A_Button", destinationPanel, "Start Route A", new Vector2(0f, -34f), new Vector2(270f, 72f));

        var remainingPanel = CreatePanel("RemainingDistancePanel", canvasRect, new Vector2(-490f, -300f), new Vector2(360f, 112f),
            new Color(0f, 0f, 0f, 0f));
        var remainingText = CreateText("RemainingDistanceText", remainingPanel, "Remaining: 0m", 32, TextAlignmentOptions.MidlineLeft);
        remainingText.fontStyle = FontStyles.Bold;
        remainingText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        remainingText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        remainingText.rectTransform.pivot = new Vector2(0f, 0.5f);
        remainingText.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        remainingText.rectTransform.sizeDelta = new Vector2(360f, 50f);
        var etaText = CreateText("EtaText", remainingPanel, "Arrival: < 1min", 24, TextAlignmentOptions.MidlineLeft);
        etaText.fontStyle = FontStyles.Bold;
        etaText.color = new Color(1f, 1f, 1f, 0.92f);
        etaText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        etaText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        etaText.rectTransform.pivot = new Vector2(0f, 0.5f);
        etaText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        etaText.rectTransform.sizeDelta = new Vector2(360f, 38f);

        var statusPanel = CreatePanel("StatusPanel", canvasRect, new Vector2(-440f, -238f), new Vector2(440f, 56f),
            new Color(0f, 0f, 0f, 0f));
        var statusText = CreateText("StatusText", statusPanel, "Ready.", 24, TextAlignmentOptions.MidlineLeft);
        statusText.color = new Color(1f, 0.96f, 0.72f, 1f);
        Stretch(statusText.rectTransform, 0f, 0f, 0f, 0f);

        var arrowText = CreateText("DirectionArrow", canvasRect, "\u2191", 110, TextAlignmentOptions.Center);
        arrowText.enableAutoSizing = false;
        arrowText.color = new Color(1f, 0.95f, 0.45f, 0.9f);
        arrowText.fontStyle = FontStyles.Bold;
        arrowText.raycastTarget = false;
        arrowText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrowText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrowText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrowText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        arrowText.rectTransform.sizeDelta = new Vector2(180f, 180f);

        var warningPanel = CreatePanel("WarningPanel", canvasRect, new Vector2(0f, 88f), new Vector2(760f, 180f),
            new Color(0f, 0f, 0f, 0f));
        var warningGroup = GetOrAddComponent<CanvasGroup>(warningPanel.gameObject);
        var warningText = CreateText("WarningText", warningPanel, "OFF COURSE!\nPlease return to the path.", 48, TextAlignmentOptions.Center);
        warningText.color = new Color(1f, 0.08f, 0.04f, 1f);
        warningText.fontStyle = FontStyles.Bold;
        warningText.lineSpacing = -16f;
        Stretch(warningText.rectTransform, 0f, 0f, 0f, 0f);

        var progressPanel = CreatePanel("ProgressPanel", canvasRect, new Vector2(430f, -294f), new Vector2(506f, 120f),
            new Color(0f, 0f, 0f, 0f));
        var progressSlider = CreateProgressSlider(progressPanel);
        var runnerText = CreateText("RunnerIcon", progressPanel, "RUN", 24, TextAlignmentOptions.Center);
        runnerText.color = new Color(1f, 0.9f, 0.22f, 1f);
        runnerText.fontStyle = FontStyles.Bold;
        runnerText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        runnerText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        runnerText.rectTransform.anchoredPosition = new Vector2(38f, 32f);
        runnerText.rectTransform.sizeDelta = new Vector2(74f, 58f);
        var trainText = CreateText("TrainIcon", progressPanel, "TRAIN", 23, TextAlignmentOptions.Center);
        trainText.color = new Color(1f, 0.9f, 0.22f, 1f);
        trainText.fontStyle = FontStyles.Bold;
        trainText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        trainText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        trainText.rectTransform.anchoredPosition = new Vector2(-35f, 32f);
        trainText.rectTransform.sizeDelta = new Vector2(88f, 58f);
        var progressText = CreateText("ProgressText", progressPanel, "0%", 20, TextAlignmentOptions.Center);
        progressText.color = new Color(1f, 0.9f, 0.22f, 1f);
        progressText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        progressText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        progressText.rectTransform.sizeDelta = new Vector2(100f, 36f);

        var arrivalPanel = CreatePanel("ArrivalPanel", canvasRect, new Vector2(0f, 68f), new Vector2(560f, 250f),
            new Color(0f, 0f, 0f, 0f));
        var arrivalGroup = GetOrAddComponent<CanvasGroup>(arrivalPanel.gameObject);
        var arrivalText = CreateText("ArrivalText", arrivalPanel, "Next Train: 3 min\n\nARRIVED", 46, TextAlignmentOptions.Center);
        arrivalText.enableAutoSizing = false;
        arrivalText.color = new Color(0.66f, 0.9f, 0.2f, 1f);
        arrivalText.fontStyle = FontStyles.Bold;
        Stretch(arrivalText.rectTransform, 16f, 16f, 16f, 16f);

        EnsureEventSystem();

        return new HudReferences
        {
            DestinationPanel = destinationPanel.gameObject,
            DestinationButton = button,
            RemainingDistanceText = remainingText,
            EtaText = etaText,
            StatusText = statusText,
            WarningText = warningText,
            WarningGroup = warningGroup,
            ArrivalText = arrivalText,
            ArrivalGroup = arrivalGroup,
            ProgressText = progressText,
            DirectionArrowText = arrowText,
            DirectionArrowRect = arrowText.rectTransform,
            ProgressSlider = progressSlider
        };
    }

    private static void CreatePalmHud(Camera camera, HudReferences sourceHud)
    {
        var palmHudObject = GetOrCreateUi("PalmHUD", null);
        var palmCanvas = GetOrAddComponent<Canvas>(palmHudObject);
        palmCanvas.renderMode = RenderMode.WorldSpace;
        palmCanvas.worldCamera = camera;
        GetOrAddComponent<CanvasScaler>(palmHudObject).dynamicPixelsPerUnit = 20f;
        GetOrAddComponent<GraphicRaycaster>(palmHudObject).enabled = false;

        var rect = palmHudObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 160f);
        palmHudObject.transform.localScale = Vector3.one * 0.0009f;
        if (camera != null)
            palmHudObject.transform.position = camera.transform.position + camera.transform.forward * 0.6f;

        var panel = CreatePanel("PalmPanel", rect, Vector2.zero, new Vector2(320f, 130f), new Color(0.02f, 0.05f, 0.08f, 0.82f));
        var remaining = CreateText("PalmRemainingText", panel, "Remaining: 0m", 34, TextAlignmentOptions.Center);
        remaining.fontStyle = FontStyles.Bold;
        remaining.rectTransform.anchorMin = new Vector2(0.5f, 0.65f);
        remaining.rectTransform.anchorMax = new Vector2(0.5f, 0.65f);
        remaining.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        remaining.rectTransform.sizeDelta = new Vector2(300f, 60f);
        remaining.rectTransform.anchoredPosition = Vector2.zero;

        var status = CreateText("PalmStatusText", panel, "Ready.", 24, TextAlignmentOptions.Center);
        status.color = new Color(0.95f, 0.97f, 1f, 0.95f);
        status.rectTransform.anchorMin = new Vector2(0.5f, 0.3f);
        status.rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
        status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        status.rectTransform.sizeDelta = new Vector2(300f, 42f);
        status.rectTransform.anchoredPosition = Vector2.zero;

        var follower = GetOrAddComponent<PalmHudFollower>(palmHudObject);
        follower.SetCamera(camera != null ? camera.transform : null);

        var mirror = GetOrAddComponent<RouteHudMirror>(palmHudObject);
        mirror.SetSources(sourceHud.RemainingDistanceText, sourceHud.StatusText, remaining, status);
    }

    private static Slider CreateProgressSlider(RectTransform parent)
    {
        var sliderObject = GetOrCreateUi("ProgressSlider", parent);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, 0f);
        sliderRect.sizeDelta = new Vector2(336f, 32f);

        var slider = GetOrAddComponent<Slider>(sliderObject);
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;

        for (var i = 0; i < 30; i++)
        {
            var dot = GetOrCreateUi($"Dot_{i:00}", sliderRect);
            var dotRect = dot.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.anchoredPosition = new Vector2(10f + i * 11f, 0f);
            dotRect.sizeDelta = new Vector2(6f, 6f);
            var dotImage = GetOrAddComponent<Image>(dot);
            dotImage.color = new Color(1f, 0.85f, 0.14f, 0.85f);
            dotImage.raycastTarget = false;
        }

        var fill = GetOrCreateUi("ProgressFill", sliderRect);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 11f);
        var fillImage = GetOrAddComponent<Image>(fill);
        fillImage.color = new Color(1f, 0.85f, 0.14f, 0.35f);
        fillImage.raycastTarget = false;
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;

        return slider;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var panel = GetOrCreateUi(name, parent).GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;

        var image = GetOrAddComponent<Image>(panel.gameObject);
        image.color = color;
        image.raycastTarget = color.a > 0.01f;

        return panel;
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var textObject = GetOrCreateUi(name, parent);
        var tmp = GetOrAddComponent<TextMeshProUGUI>(textObject);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 14f;
        tmp.fontSizeMax = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var rect = CreatePanel(name, parent, anchoredPosition, size, new Color(0.04f, 0.42f, 0.9f, 0.95f));
        rect.GetComponent<Image>().raycastTarget = true;
        var button = GetOrAddComponent<Button>(rect.gameObject);
        var labelText = CreateText("Label", rect, label, 34, TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        Stretch(labelText.rectTransform, 8f, 8f, 8f, 8f);
        return button;
    }

    private static void EnsureYuykim2InBuildSettings()
    {
        var buildScenes = EditorBuildSettings.scenes;
        var hasTarget = false;
        foreach (var buildScene in buildScenes)
        {
            if (buildScene.path == TargetScenePath)
            {
                hasTarget = true;
                break;
            }
        }

        if (!hasTarget)
        {
            var updatedScenes = new EditorBuildSettingsScene[buildScenes.Length + 1];
            updatedScenes[0] = new EditorBuildSettingsScene(TargetScenePath, true);
            for (var i = 0; i < buildScenes.Length; i++)
                updatedScenes[i + 1] = buildScenes[i];
            EditorBuildSettings.scenes = updatedScenes;
            return;
        }

        var reordered = new EditorBuildSettingsScene[buildScenes.Length];
        var index = 0;
        reordered[index++] = new EditorBuildSettingsScene(TargetScenePath, true);
        foreach (var buildScene in buildScenes)
        {
            if (buildScene.path == TargetScenePath)
                continue;
            reordered[index++] = buildScene;
        }

        EditorBuildSettings.scenes = reordered;
    }

    private static GameObject GetOrCreate(string name, Transform parent = null)
    {
        if (parent != null)
        {
            var child = parent.Find(name);
            if (child != null)
                return child.gameObject;
        }

        var existing = GameObject.Find(name);
        if (existing != null && (parent == null || existing.transform.parent == parent))
            return existing;

        var created = new GameObject(name);
        if (parent != null)
            created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject GetOrCreateUi(string name, Transform parent)
    {
        if (parent != null)
        {
            var child = parent.Find(name);
            if (child != null)
                return child.gameObject;
        }

        var existing = GameObject.Find(name);
        if (existing != null && (parent == null || existing.transform.parent == parent))
            return existing;

        var created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void EnsureEventSystem()
    {
        var eventSystemObject = EventSystem.current != null
            ? EventSystem.current.gameObject
            : GetOrCreate("EventSystem");

        if (eventSystemObject.GetComponent<EventSystem>() == null)
            eventSystemObject.AddComponent<EventSystem>();
        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            eventSystemObject.AddComponent<StandaloneInputModule>();
        if (eventSystemObject.GetComponent<VrUiInputModuleBootstrap>() == null)
            eventSystemObject.AddComponent<VrUiInputModuleBootstrap>();
    }

    private static void AssignRoutePathRenderer(RoutePathRenderer renderer, Transform[] routePoints)
    {
        if (renderer == null)
        {
            Debug.LogError("RoutePathRenderer is missing.");
            return;
        }

        var serializedObject = new SerializedObject(renderer);
        serializedObject.FindProperty("routePoints").arraySize = routePoints.Length;
        for (var i = 0; i < routePoints.Length; i++)
            serializedObject.FindProperty("routePoints").GetArrayElementAtIndex(i).objectReferenceValue = routePoints[i];
        serializedObject.FindProperty("routeHeight").floatValue = RoutePointY;
        serializedObject.FindProperty("useRoutePointY").boolValue = true;
        serializedObject.ApplyModifiedProperties();
        renderer.Refresh();
        EditorUtility.SetDirty(renderer);
    }

    private static void AssignNavigationController(
        RouteNavigationController controller,
        Camera camera,
        Transform[] routePoints,
        RoutePathRenderer routePathRenderer,
        HudReferences hud)
    {
        var serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("userTransform").objectReferenceValue = camera != null ? camera.transform : null;
        serializedObject.FindProperty("routePoints").arraySize = routePoints.Length;
        for (var i = 0; i < routePoints.Length; i++)
            serializedObject.FindProperty("routePoints").GetArrayElementAtIndex(i).objectReferenceValue = routePoints[i];
        serializedObject.FindProperty("routePathRenderer").objectReferenceValue = routePathRenderer;
        serializedObject.FindProperty("destinationSelectPanel").objectReferenceValue = hud.DestinationPanel;
        serializedObject.FindProperty("remainingDistanceText").objectReferenceValue = hud.RemainingDistanceText;
        serializedObject.FindProperty("etaText").objectReferenceValue = hud.EtaText;
        serializedObject.FindProperty("statusText").objectReferenceValue = hud.StatusText;
        serializedObject.FindProperty("warningText").objectReferenceValue = hud.WarningText;
        serializedObject.FindProperty("arrivalText").objectReferenceValue = hud.ArrivalText;
        serializedObject.FindProperty("progressText").objectReferenceValue = hud.ProgressText;
        serializedObject.FindProperty("directionArrowText").objectReferenceValue = hud.DirectionArrowText;
        serializedObject.FindProperty("directionArrowRect").objectReferenceValue = hud.DirectionArrowRect;
        serializedObject.FindProperty("progressSlider").objectReferenceValue = hud.ProgressSlider;
        serializedObject.FindProperty("warningGroup").objectReferenceValue = hud.WarningGroup;
        serializedObject.FindProperty("arrivalGroup").objectReferenceValue = hud.ArrivalGroup;
        serializedObject.ApplyModifiedProperties();

        hud.DestinationButton.onClick.RemoveAllListeners();
        while (hud.DestinationButton.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(hud.DestinationButton.onClick, 0);
        UnityEventTools.AddPersistentListener(hud.DestinationButton.onClick, controller.StartNavigation);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(hud.DestinationButton);
    }

    private sealed class HudReferences
    {
        public GameObject DestinationPanel;
        public Button DestinationButton;
        public TMP_Text RemainingDistanceText;
        public TMP_Text EtaText;
        public TMP_Text StatusText;
        public TMP_Text WarningText;
        public CanvasGroup WarningGroup;
        public TMP_Text ArrivalText;
        public CanvasGroup ArrivalGroup;
        public TMP_Text ProgressText;
        public TMP_Text DirectionArrowText;
        public RectTransform DirectionArrowRect;
        public Slider ProgressSlider;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);

        var component = gameObject.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(gameObject);

        return component;
    }
}
