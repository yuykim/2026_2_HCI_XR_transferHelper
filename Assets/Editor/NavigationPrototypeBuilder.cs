using TMPro;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NavigationPrototypeBuilder
{
    private const float RoutePointY = 0.10f;
    private const string TargetScenePath = "Assets/Scenes/YUYKIM2.unity";
    private const string UiVersionLabel = "v2026.05.01";
    private const string OffRouteSoundPath = "Assets/sound/off_the_course_sound.mp3";
    private const string ArrivalSoundPath = "Assets/sound/arrive sound.mp3";

    [MenuItem("Tools/XR Transfer Helper/Create Navigation Prototype")]
    public static void CreateNavigationPrototype()
    {
        CreateNavigationPrototypeInCurrentScene();
    }

    [MenuItem("Tools/XR Transfer Helper/Apply Navigation Sounds")]
    public static void ApplyNavigationSounds()
    {
        var controller = Object.FindObjectOfType<RouteNavigationController>();
        if (controller == null)
        {
            Debug.LogError("RouteNavigationController was not found in the current scene.");
            return;
        }

        AssignNavigationSounds(controller);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Debug.Log("Navigation sounds assigned.");
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
        var routeOptions = CreateRoutePoints(routePointsRoot);
        var lightRoute = GetOrCreate("LightRoute", navigationRoot);
        lightRoute.transform.localPosition = Vector3.zero;
        lightRoute.transform.localRotation = Quaternion.identity;
        lightRoute.transform.localScale = Vector3.one;
        SetupLineRenderer(lightRoute);
        var routePathRenderer = GetOrAddComponent<RoutePathRenderer>(lightRoute);

        AssignRoutePathRenderer(routePathRenderer, System.Array.Empty<Transform>());

        var navigationController = GetOrAddComponent<RouteNavigationController>(navigationRoot.gameObject);
        var hud = CreateHud(camera);
        AssignNavigationController(navigationController, camera, routeOptions, routePathRenderer, hud);
        DeleteIfExists("PalmHUD");

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

    private static RouteOptions CreateRoutePoints(Transform routePointsRoot)
    {
        var routeA = CreateRouteSequence(routePointsRoot, "RouteA", new[]
        {
            new Vector3(0f, RoutePointY, 0f),
            new Vector3(0f, RoutePointY, 2.4f),
            new Vector3(0f, RoutePointY, 4.8f),
            new Vector3(2.2f, RoutePointY, 4.8f),
            new Vector3(4.4f, RoutePointY, 4.8f),
            new Vector3(4.4f, RoutePointY, 7.2f),
            new Vector3(4.4f, RoutePointY, 9.6f),
            new Vector3(6.6f, RoutePointY, 9.6f)
        });

        var routeB = CreateRouteSequence(routePointsRoot, "RouteB", new[]
        {
            new Vector3(0f, RoutePointY, 0f),
            new Vector3(-1.8f, RoutePointY, 1.8f),
            new Vector3(-3.6f, RoutePointY, 3.6f),
            new Vector3(-3.6f, RoutePointY, 6.2f),
            new Vector3(-2.2f, RoutePointY, 8.4f),
            new Vector3(0f, RoutePointY, 10.4f),
            new Vector3(2.4f, RoutePointY, 11.2f)
        });

        var routeC = CreateRouteSequence(routePointsRoot, "RouteC", new[]
        {
            new Vector3(0f, RoutePointY, 0f),
            new Vector3(1.8f, RoutePointY, 1.6f),
            new Vector3(3.8f, RoutePointY, 2.8f),
            new Vector3(6.2f, RoutePointY, 2.8f),
            new Vector3(8.8f, RoutePointY, 4.2f),
            new Vector3(9.6f, RoutePointY, 6.8f),
            new Vector3(8.2f, RoutePointY, 9.2f)
        });

        return new RouteOptions
        {
            RouteRoot = routePointsRoot,
            RouteA = routeA,
            RouteB = routeB,
            RouteC = routeC
        };
    }

    private static Transform[] CreateRouteSequence(Transform routePointsRoot, string routeName, Vector3[] positions)
    {
        var group = GetOrCreate(routeName, routePointsRoot).transform;
        var points = new Transform[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var point = GetOrCreate($"{routeName}_RP_{i:00}", group).transform;
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
        lineRenderer.startColor = new Color(0.55f, 1f, 1f, 1f);
        lineRenderer.endColor = new Color(0.05f, 0.92f, 1f, 1f);

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
        var hudFollower = GetOrAddComponent<HeadLockedHudFollower>(hud);
        hudFollower.SetTarget(camera != null ? camera.transform : null, 1.25f, -0.08f, 12f);

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

        var destinationPanel = CreatePanel("DestinationSelectPanel", canvasRect, new Vector2(0f, 110f), new Vector2(900f, 250f),
            new Color(0.02f, 0.04f, 0.06f, 0.78f));
        var destinationTitle = CreateText("Title", destinationPanel, "Choose Route", 38, TextAlignmentOptions.Center);
        destinationTitle.fontStyle = FontStyles.Bold;
        SetRect(destinationTitle.rectTransform, new Vector2(0f, 72f), new Vector2(840f, 58f));
        var buttonA = CreateButton("Destination_A_Button", destinationPanel, "Route A", new Vector2(-280f, -52f), new Vector2(250f, 82f));
        var buttonB = CreateButton("Destination_B_Button", destinationPanel, "Route B", new Vector2(0f, -52f), new Vector2(250f, 82f));
        var buttonC = CreateButton("Destination_C_Button", destinationPanel, "Route C", new Vector2(280f, -52f), new Vector2(250f, 82f));

        var remainingPanel = CreatePanel("RemainingDistancePanel", canvasRect, new Vector2(0f, 303f), new Vector2(620f, 82f),
            new Color(0.01f, 0.02f, 0.03f, 0.42f));
        var remainingText = CreateText("RemainingDistanceText", remainingPanel, "To TRAIN: 0m", 36, TextAlignmentOptions.Center);
        remainingText.fontStyle = FontStyles.Bold;
        SetRect(remainingText.rectTransform, Vector2.zero, new Vector2(590f, 60f));
        var etaText = CreateText("EtaText", remainingPanel, "Arrival: < 1min", 24, TextAlignmentOptions.MidlineLeft);
        etaText.gameObject.SetActive(false);
        var versionText = CreateText("VersionText", remainingPanel, UiVersionLabel, 16, TextAlignmentOptions.MidlineLeft);
        versionText.gameObject.SetActive(false);

        var statusPanel = CreatePanel("StatusPanel", canvasRect, new Vector2(0f, 58f), new Vector2(1120f, 106f),
            new Color(0f, 0f, 0f, 0f));
        var statusText = CreateText("StatusText", statusPanel, "Ready.", 36, TextAlignmentOptions.Center);
        statusText.color = new Color(0.82f, 0.96f, 1f, 1f);
        statusText.fontStyle = FontStyles.Bold;
        Stretch(statusText.rectTransform, 24f, 12f, 24f, 12f);

        var arrowText = CreateText("DirectionArrow", canvasRect, "\u2191", 96, TextAlignmentOptions.Center);
        arrowText.enableAutoSizing = false;
        arrowText.color = new Color(1f, 0.95f, 0.45f, 0.9f);
        arrowText.fontStyle = FontStyles.Bold;
        arrowText.raycastTarget = false;
        SetRect(arrowText.rectTransform, new Vector2(0f, -40f), new Vector2(160f, 160f));

        var warningPanel = CreatePanel("WarningPanel", canvasRect, new Vector2(0f, 150f), new Vector2(760f, 104f),
            new Color(0f, 0f, 0f, 0f));
        var warningGroup = GetOrAddComponent<CanvasGroup>(warningPanel.gameObject);
        var warningText = CreateText("WarningText", warningPanel, "OFF COURSE!\nPlease return to the path.", 38, TextAlignmentOptions.Center);
        warningText.color = new Color(1f, 0.08f, 0.04f, 1f);
        warningText.fontStyle = FontStyles.Bold;
        warningText.lineSpacing = -4f;
        Stretch(warningText.rectTransform, 0f, 0f, 0f, 0f);

        var progressPanel = CreatePanel("ProgressPanel", canvasRect, new Vector2(0f, -292f), new Vector2(620f, 118f),
            new Color(0f, 0f, 0f, 0f));
        var progressSlider = CreateProgressSlider(progressPanel);
        var runnerText = CreateText("RunnerIcon", progressPanel, "RUN", 20, TextAlignmentOptions.Center);
        runnerText.color = new Color(1f, 0.9f, 0.22f, 1f);
        runnerText.fontStyle = FontStyles.Bold;
        SetRect(runnerText.rectTransform, new Vector2(-240f, 44f), new Vector2(82f, 32f));
        var trainText = CreateText("TrainIcon", progressPanel, "TRAIN", 20, TextAlignmentOptions.Center);
        trainText.color = new Color(1f, 0.9f, 0.22f, 1f);
        trainText.fontStyle = FontStyles.Bold;
        SetRect(trainText.rectTransform, new Vector2(240f, 44f), new Vector2(96f, 32f));
        var progressText = CreateText("ProgressText", progressPanel, "0%", 20, TextAlignmentOptions.Center);
        progressText.color = new Color(1f, 0.9f, 0.22f, 1f);
        SetRect(progressText.rectTransform, new Vector2(0f, -42f), new Vector2(120f, 30f));
        remainingPanel.gameObject.SetActive(false);
        progressPanel.gameObject.SetActive(false);

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
            DestinationButtonA = buttonA,
            DestinationButtonB = buttonB,
            DestinationButtonC = buttonC,
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

    private static Slider CreateProgressSlider(RectTransform parent)
    {
        var sliderObject = GetOrCreateUi("ProgressSlider", parent);
        var sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, -8f);
        sliderRect.sizeDelta = new Vector2(360f, 28f);

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
        button.transition = Selectable.Transition.None;
        button.targetGraphic = rect.GetComponent<Image>();
        var labelText = CreateText("Label", rect, label, 40, TextAlignmentOptions.Center);
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
        serializedObject.FindProperty("upcomingRouteStartColor").colorValue = new Color(0.55f, 1f, 1f, 1f);
        serializedObject.FindProperty("upcomingRouteEndColor").colorValue = new Color(0.05f, 0.92f, 1f, 1f);
        serializedObject.FindProperty("routeUnderlayColor").colorValue = new Color(0.05f, 0.55f, 1f, 0.32f);
        serializedObject.FindProperty("routeUnderlayWidthMultiplier").floatValue = 2.8f;
        serializedObject.FindProperty("traveledRouteColor").colorValue = new Color(0.02f, 0.36f, 0.16f, 0.95f);
        serializedObject.ApplyModifiedProperties();
        renderer.Refresh();
        EditorUtility.SetDirty(renderer);
    }

    private static void AssignNavigationController(
        RouteNavigationController controller,
        Camera camera,
        RouteOptions routeOptions,
        RoutePathRenderer routePathRenderer,
        HudReferences hud)
    {
        var serializedObject = new SerializedObject(controller);
        serializedObject.FindProperty("userTransform").objectReferenceValue = camera != null ? camera.transform : null;
        serializedObject.FindProperty("routePoints").arraySize = 0;
        serializedObject.FindProperty("routePointsA").arraySize = routeOptions.RouteA.Length;
        for (var i = 0; i < routeOptions.RouteA.Length; i++)
            serializedObject.FindProperty("routePointsA").GetArrayElementAtIndex(i).objectReferenceValue = routeOptions.RouteA[i];
        serializedObject.FindProperty("routePointsB").arraySize = routeOptions.RouteB.Length;
        for (var i = 0; i < routeOptions.RouteB.Length; i++)
            serializedObject.FindProperty("routePointsB").GetArrayElementAtIndex(i).objectReferenceValue = routeOptions.RouteB[i];
        serializedObject.FindProperty("routePointsC").arraySize = routeOptions.RouteC.Length;
        for (var i = 0; i < routeOptions.RouteC.Length; i++)
            serializedObject.FindProperty("routePointsC").GetArrayElementAtIndex(i).objectReferenceValue = routeOptions.RouteC[i];
        ConfigureRouteInstructions(serializedObject.FindProperty("routeInstructionsA"), routeOptions.RouteA, new[]
        {
            new InstructionSeed(1, "Continue straight for 5m."),
            new InstructionSeed(3, "Turn right and continue.", true)
        });
        ConfigureRouteInstructions(serializedObject.FindProperty("routeInstructionsB"), routeOptions.RouteB, System.Array.Empty<InstructionSeed>());
        ConfigureRouteInstructions(serializedObject.FindProperty("routeInstructionsC"), routeOptions.RouteC, System.Array.Empty<InstructionSeed>());
        serializedObject.FindProperty("routePathRenderer").objectReferenceValue = routePathRenderer;
        serializedObject.FindProperty("alignRoutesToStartupView").boolValue = true;
        serializedObject.FindProperty("routePointsRoot").objectReferenceValue = routeOptions.RouteRoot;
        serializedObject.FindProperty("pointReachRadius").floatValue = 0.75f;
        serializedObject.FindProperty("pointPassRadius").floatValue = 1.4f;
        serializedObject.FindProperty("routeDeviationThreshold").floatValue = 1.4f;
        serializedObject.FindProperty("preferHandRay").boolValue = true;
        serializedObject.FindProperty("allowGazeRay").boolValue = false;
        serializedObject.FindProperty("allowControllerTriggerFallback").boolValue = true;
        serializedObject.FindProperty("allowControllerRay").boolValue = true;
        serializedObject.FindProperty("maxSelectionRayDistance").floatValue = 12f;
        serializedObject.FindProperty("selectionHitPadding").floatValue = 80f;
        serializedObject.FindProperty("gazeScreenFallbackMaxPixels").floatValue = 520f;
        serializedObject.FindProperty("showSelectionRay").boolValue = true;
        serializedObject.FindProperty("hudFollowDistance").floatValue = 1.25f;
        serializedObject.FindProperty("hudFollowVerticalOffset").floatValue = -0.08f;
        serializedObject.FindProperty("hudFollowSmooth").floatValue = 12f;
        serializedObject.FindProperty("worldArrowHeightOffset").floatValue = -0.65f;
        serializedObject.FindProperty("worldArrowNormalColor").colorValue = new Color(0.45f, 1f, 1f, 1f);
        serializedObject.FindProperty("showAdditionalInfoWithBButton").boolValue = true;
        serializedObject.FindProperty("demoNextTrainArrival").stringValue = "In 2 min";
        serializedObject.FindProperty("demoNextTrainDetail").stringValue = "Line 2 - City Hall";
        serializedObject.FindProperty("demoNearestExitName").stringValue = "Exit 3";
        serializedObject.FindProperty("demoNearestExitDistance").stringValue = "24m";
        serializedObject.FindProperty("destinationSelectPanel").objectReferenceValue = hud.DestinationPanel;
        serializedObject.FindProperty("routeAButton").objectReferenceValue = hud.DestinationButtonA;
        serializedObject.FindProperty("routeBButton").objectReferenceValue = hud.DestinationButtonB;
        serializedObject.FindProperty("routeCButton").objectReferenceValue = hud.DestinationButtonC;
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
        AssignNavigationSounds(serializedObject, controller);
        serializedObject.ApplyModifiedProperties();

        ConfigureRouteButton(hud.DestinationButtonA, controller.StartRouteA);
        ConfigureRouteButton(hud.DestinationButtonB, controller.StartRouteB);
        ConfigureRouteButton(hud.DestinationButtonC, controller.StartRouteC);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(hud.DestinationButtonA);
        EditorUtility.SetDirty(hud.DestinationButtonB);
        EditorUtility.SetDirty(hud.DestinationButtonC);
    }

    private static void AssignNavigationSounds(RouteNavigationController controller)
    {
        var serializedObject = new SerializedObject(controller);
        AssignNavigationSounds(serializedObject, controller);
        serializedObject.ApplyModifiedProperties();
    }

    private static void AssignNavigationSounds(SerializedObject serializedObject, RouteNavigationController controller)
    {
        var audioSource = controller.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = Undo.AddComponent<AudioSource>(controller.gameObject);

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        serializedObject.FindProperty("navigationAudioSource").objectReferenceValue = audioSource;
        serializedObject.FindProperty("offRouteWarningClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>(OffRouteSoundPath);
        serializedObject.FindProperty("arrivalClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>(ArrivalSoundPath);
        serializedObject.FindProperty("offRouteSoundCooldownSeconds").floatValue = 2.5f;
        serializedObject.FindProperty("navigationSoundVolume").floatValue = 1f;
        EditorUtility.SetDirty(audioSource);
    }

    private static void ConfigureRouteInstructions(SerializedProperty instructionsProperty, Transform[] route, InstructionSeed[] seeds)
    {
        instructionsProperty.arraySize = seeds.Length;
        for (var i = 0; i < seeds.Length; i++)
        {
            var seed = seeds[i];
            var routeIndex = Mathf.Clamp(seed.RouteIndex, 0, route.Length - 1);
            var element = instructionsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("point").objectReferenceValue = route[routeIndex];
            element.FindPropertyRelative("message").stringValue = seed.Message;
            element.FindPropertyRelative("triggerRadius").floatValue = 1.0f;
            element.FindPropertyRelative("showUntilExitRadius").boolValue = false;
            element.FindPropertyRelative("displaySeconds").floatValue = 3.0f;
            element.FindPropertyRelative("requireLookDirection").boolValue = seed.RequireLookDirection;
            element.FindPropertyRelative("lookTarget").objectReferenceValue =
                seed.RequireLookDirection && routeIndex < route.Length - 1 ? route[routeIndex + 1] : null;
            element.FindPropertyRelative("localLookDirection").vector3Value = Vector3.forward;
            element.FindPropertyRelative("requiredLookAngle").floatValue = 45f;
        }
    }

    private static void ConfigureRouteButton(Button button, UnityAction action)
    {
        button.transition = Selectable.Transition.None;
        button.targetGraphic = button.GetComponent<Image>();
        button.onClick.RemoveAllListeners();
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private sealed class HudReferences
    {
        public GameObject DestinationPanel;
        public Button DestinationButtonA;
        public Button DestinationButtonB;
        public Button DestinationButtonC;
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

    private sealed class RouteOptions
    {
        public Transform RouteRoot;
        public Transform[] RouteA;
        public Transform[] RouteB;
        public Transform[] RouteC;
    }

    private struct InstructionSeed
    {
        public int RouteIndex;
        public string Message;
        public bool RequireLookDirection;

        public InstructionSeed(int routeIndex, string message, bool requireLookDirection = false)
        {
            RouteIndex = routeIndex;
            Message = message;
            RequireLookDirection = requireLookDirection;
        }
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
