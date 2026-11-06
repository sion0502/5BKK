using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// 시연용 프롤로그 씬(Assets/Scenes/Demo/Prologue.unity)을 생성합니다. 메뉴: Tools > Story > Build Prologue Scene
/// 인트로가 병원 입구에서 암전한 뒤 이어지는 씬:
///   오프닝 컷씬(캠코더 화면) — 열린 정문으로 걸어 들어옴 → 대사 → 뒤에서 문이 끼익 움직이고 뒤돌아보면 쾅 닫힘
///   → 대사 → 캠코더를 내리며 같은 자리·시선에서 플레이 시작 (닫힌 정문은 E 해도 안 열림)
/// 플레이어·HUD·튜토리얼·일시정지 메뉴는 Chapter1 2 씬에서 그대로 옮겨 옴(그 씬 파일은 저장하지 않으므로 원본 유지).
/// 재실행하면 씬·Timeline을 덮어씀 — 팀원이 꾸미기 시작한 뒤엔 씬을 직접 수정할 것.
/// </summary>
public static class PrologueSceneBuilder
{
    private const string SceneDir = "Assets/Scenes/Demo";
    private const string ScenePath = SceneDir + "/Prologue.unity";
    private const string DataDir = "Assets/Story/Prologue";
    private const string GameplaySourceScene = "Assets/Scenes/Chapter/Chapter1 2.unity";
    private const string ViewfinderDir = "Assets/Sprites/Item/Camcorder/Viewfinder/";
    private const string FlashlightPrefab = "Assets/Prefab/Item/World_Item/Equip/World_Flashlight.prefab";

    // Chapter1 2에서 옮겨 올 루트 (나머지 — 타일맵·적·SceneStarter 등은 안 가져옴)
    private static readonly string[] GameplayRoots =
    {
        "Player", "Canvas", "EventSystem", "FadeImage", "Chapter1_GlobalVolume", "PauseMenu", "Tutorial",
    };

    // 플레이 시작 전까지 꺼 둘 것 (컷씬 중 ESC가 일시정지와 겹치지 않게, HUD·튜토리얼은 조작 시작과 함께)
    private static readonly string[] ActivateOnHandoff = { "Canvas", "PauseMenu", "Tutorial" };

    // 정문 바깥 → 로비 안쪽 (바닥 위치). 끝 지점이 곧 플레이 시작 위치
    private static readonly Vector3[] WalkPath =
    {
        new Vector3(5.1f, 0f, -3.4f),
        new Vector3(5.1f, 0f, -0.6f),
        new Vector3(5.5f, 0f, 2.4f),
        new Vector3(6.1f, 0f, 4.9f),
    };

    private static readonly Vector3 DeskLookPoint = new Vector3(2.2f, 1.3f, 9.2f);
    private static readonly Vector3 DoorLookPoint = new Vector3(5.1f, 1.45f, 0.3f);

    [MenuItem("Tools/Story/Build Prologue Scene")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Prologue 씬 다시 만들기",
                "Prologue 씬과 Timeline을 새로 만들어 덮어씁니다. 손으로 고친 내용은 사라집니다. 계속할까요?", "덮어쓰기", "취소"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        BuildWithoutPrompt();
    }

    /// <summary>확인창 없이 바로 생성 (자동화용 — 현재 열린 씬의 저장 안 된 변경은 버려짐).</summary>
    public static void BuildWithoutPrompt()
    {
        EnsureFolder("Assets/Story", "Prologue");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildLighting();

        Transform map = PrologueMapBuilder.Build();
        IntroEnvironmentBuilder.PowerOff(map); // 전기 끊김: 형광등 등 발광 재질 끔 (인트로와 같은 처리)
        Dictionary<string, GameObject> gameplay = ImportGameplayRig(scene);
        Physics.SyncTransforms();
        PlaceFlashlight();

        CutsceneDoorSwing door = SetupFrontDoor(PrologueMapBuilder.FrontDoor);

        GameObject player = gameplay.TryGetValue("Player", out GameObject p) ? p : null;
        Camera playerCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;

        // 조작 시작 후 동선: 간호사·E1·일지·트리거·사망·진행 감독 + NavMesh (PrologueEventBuilder)
        GameObject director = PrologueEventBuilder.Build(map, playerCamera, gameplay.TryGetValue("Tutorial", out GameObject tutorial) ? tutorial : null);

        Transform lookRoot = new GameObject("LookTargets").transform;
        Transform deskLook = CreatePoint("Look_Desk", DeskLookPoint, lookRoot);
        Transform doorLook = CreatePoint("Look_Door", DoorLookPoint, lookRoot);

        if (playerCamera != null)
        {
            // 하늘(skybox) 없음 → 창·정문 바깥은 안개색 어둠
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = RenderSettings.fogColor;
        }

        float eyeHeight = playerCamera != null ? playerCamera.transform.localPosition.y : 1.8f;
        Camera cam = BuildCamera(playerCamera);
        CutsceneCameraPath path = AddPath(cam, WalkPath, deskLook, eyeHeight);
        CutsceneCameraLookAt lookAt = cam.gameObject.AddComponent<CutsceneCameraLookAt>();
        SetRef(lookAt, "target", doorLook);
        CutsceneCameraShake shake = cam.gameObject.AddComponent<CutsceneCameraShake>();

        // 플레이 시작 = 컷씬 마지막 자리·시선 (문을 바라봄)
        Vector3 end = WalkPath[WalkPath.Length - 1];
        Transform startPoint = new GameObject("PlayerStart").transform;
        startPoint.position = end;
        startPoint.rotation = Quaternion.LookRotation(DoorLookPoint - (end + Vector3.up * eyeHeight), Vector3.up);

        CutsceneBlackScreen black = CreateOverlay<CutsceneBlackScreen>("Overlay_Black", 600, 1f);
        CutsceneCamcorderOverlay camcorder = CreateOverlay<CutsceneCamcorderOverlay>("Overlay_Camcorder", 500, 1f);
        SetRef(camcorder, "frameSprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Center_Frame.png"));
        SetRef(camcorder, "recordingDotSprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Recording_Dot.png"));
        SetRef(camcorder, "batterySprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Battery_4.png"));
        SetFloat(camcorder, "lowerOffsetOnFade", 700f);

        CutscenePlayerHandoff handoff = BuildHandoff(player, playerCamera, cam, startPoint, gameplay, black,
            new List<CutsceneBlendTarget> { door, lookAt, shake },
            new List<float> { 1f, 0f, 0f }, director);
        AddCamcorderLower(handoff, player, camcorder);

        BuildTimeline(path, lookAt, shake, door, black, camcorder, handoff);

        path.SetProgress(0f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AddToBuildSettings();

        Debug.Log($"[PrologueSceneBuilder] {ScenePath} 생성 완료 (걷는 경로 {path.TotalLength:F1}m)");
    }

    // ───────────────────────────── 환경 ─────────────────────────────

    // Chapter1 2와 같은 어두운 실내 기준 + 창·정문으로 들어오는 달빛
    private static void BuildLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.11f, 0.11f, 0.12f);
        RenderSettings.ambientEquatorColor = new Color(0.11f, 0.11f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.085f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.reflectionIntensity = 0.32f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.025f, 0.025f, 0.032f);
        RenderSettings.fogDensity = 0.018f;

        Light moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.63f, 0.82f);
        moon.intensity = 0.3f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(32f, 55f, 0f); // 남서쪽 하늘 → 서쪽 창·정문으로 들어옴
    }

    private static Dictionary<string, GameObject> ImportGameplayRig(Scene target)
    {
        var result = new Dictionary<string, GameObject>();
        Scene source = EditorSceneManager.OpenScene(GameplaySourceScene, OpenSceneMode.Additive);
        foreach (GameObject root in source.GetRootGameObjects())
        {
            if (System.Array.IndexOf(GameplayRoots, root.name) >= 0 && !result.ContainsKey(root.name))
            {
                SceneManager.MoveGameObjectToScene(root, target);
                result[root.name] = root;
            }
        }

        EditorSceneManager.CloseScene(source, true); // 저장 안 함 → Chapter1 2 원본 유지
        SceneManager.SetActiveScene(target);

        foreach (string name in GameplayRoots)
        {
            if (!result.ContainsKey(name))
            {
                Debug.LogWarning("[PrologueSceneBuilder] Chapter1 2에서 찾지 못함: " + name);
            }
        }

        return result;
    }

    // 접수대 위 (튜토리얼 '손전등 줍기' 단계)
    private static void PlaceFlashlight()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefab);
        if (prefab == null)
        {
            Debug.LogWarning("[PrologueSceneBuilder] 손전등 프리팹 없음: " + FlashlightPrefab);
            return;
        }

        Vector3 spot = PrologueLayout.FlashlightSpot;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "World_Flashlight (접수대)";
        go.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

        float top = 0f;
        if (Physics.Raycast(new Vector3(spot.x, 3f, spot.z), Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            top = hit.point.y;
        }

        // 월드 아이템은 루트가 모델 중앙 → 콜라이더 반 높이만큼 올림
        Collider col = go.GetComponentInChildren<Collider>();
        float half = col != null ? col.bounds.extents.y : 0.05f;
        go.transform.position = new Vector3(spot.x, top + half + 0.005f, spot.z);
    }

    private static CutsceneDoorSwing SetupFrontDoor(Transform doorTransform)
    {
        if (doorTransform == null)
        {
            Debug.LogWarning("[PrologueSceneBuilder] 정문 문짝(HH_Front_Door)을 찾지 못함");
            return null;
        }

        GameObject doorGo = doorTransform.gameObject;

        // 닫힌 상태 기준 문짝 크기로 상호작용 콜라이더
        Bounds bounds = new Bounds(doorTransform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in doorGo.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        BoxCollider box = doorGo.AddComponent<BoxCollider>();
        box.center = doorTransform.InverseTransformPoint(bounds.center);
        Vector3 size = doorTransform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Max(0.08f, Mathf.Abs(size.x)), Mathf.Abs(size.y), Mathf.Abs(size.z));

        SetFrontDoorMonologue(doorGo.AddComponent<SealedDoorInteract>());

        AudioSource audio = doorGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;

        CutsceneDoorSwing swing = doorGo.AddComponent<CutsceneDoorSwing>();
        swing.CaptureClosedRotation();
        SerializedObject so = new SerializedObject(swing);
        so.FindProperty("openAngle").floatValue = -95f; // 로비 안쪽(+z)으로 열려 있음 (정문 문짝: 경첩 x4.24, 닫히면 +x로 뻗음)
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
        swing.Blend = 0f;
        return swing;
    }

    // ───────────────────────────── 카메라·전환 ─────────────────────────────

    private static Camera BuildCamera(Camera playerCamera)
    {
        GameObject camGo = new GameObject("CutsceneCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = playerCamera != null ? playerCamera.fieldOfView : 60f;
        cam.nearClipPlane = playerCamera != null ? playerCamera.nearClipPlane : 0.05f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        cam.depth = 10f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return cam;
    }

    private static CutsceneCameraPath AddPath(Camera cam, Vector3[] points, Transform lookTarget, float eyeHeight)
    {
        Transform pathRoot = new GameObject("CameraPath_Entrance").transform;
        CutsceneCameraPath path = cam.gameObject.AddComponent<CutsceneCameraPath>();
        SerializedObject so = new SerializedObject(path);
        SerializedProperty array = so.FindProperty("waypoints");
        array.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = CreatePoint("Waypoint_" + i, points[i], pathRoot);
        }

        so.FindProperty("eyeHeight").floatValue = eyeHeight;
        so.FindProperty("lookTarget").objectReferenceValue = lookTarget;
        so.FindProperty("lookTargetStart").floatValue = 0.55f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return path;
    }

    private static CutscenePlayerHandoff BuildHandoff(GameObject player, Camera playerCamera, Camera cutsceneCamera,
        Transform startPoint, Dictionary<string, GameObject> gameplay, CutsceneBlackScreen black,
        List<CutsceneBlendTarget> finalTargets, List<float> finalValues, GameObject director)
    {
        CutscenePlayerHandoff handoff = new GameObject("PlayerHandoff").AddComponent<CutscenePlayerHandoff>();
        SerializedObject so = new SerializedObject(handoff);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("playerCamera").objectReferenceValue = playerCamera;
        so.FindProperty("startPoint").objectReferenceValue = startPoint;
        so.FindProperty("cutsceneCamera").objectReferenceValue = cutsceneCamera;
        so.FindProperty("blackScreen").objectReferenceValue = black;

        SerializedProperty targets = so.FindProperty("finalStateTargets");
        SerializedProperty values = so.FindProperty("finalStateValues");
        targets.arraySize = finalTargets.Count;
        values.arraySize = finalValues.Count;
        for (int i = 0; i < finalTargets.Count; i++)
        {
            targets.GetArrayElementAtIndex(i).objectReferenceValue = finalTargets[i];
            values.GetArrayElementAtIndex(i).floatValue = finalValues[i];
        }

        SerializedProperty activate = so.FindProperty("activateOnHandoff");
        activate.arraySize = 0;
        foreach (string name in ActivateOnHandoff)
        {
            if (gameplay.TryGetValue(name, out GameObject go))
            {
                activate.arraySize++;
                activate.GetArrayElementAtIndex(activate.arraySize - 1).objectReferenceValue = go;
            }
        }

        if (director != null)
        {
            activate.arraySize++;
            activate.GetArrayElementAtIndex(activate.arraySize - 1).objectReferenceValue = director;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        if (player != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(startPoint.position, Quaternion.Euler(0f, startPoint.eulerAngles.y, 0f));
            if (controller != null) controller.enabled = true;
        }

        return handoff;
    }

    /// <summary>전환 때 컷씬 캠코더 화면 → 손에 든 실제 캠코더로 바꿔 본편처럼 내림. 씬에 이미 있는 handoff에도 씀(리플렉션 호출).</summary>
    public static void AddCamcorderLower(CutscenePlayerHandoff handoff, GameObject player, CutsceneBlendTarget camcorderOverlay)
    {
        CutsceneCamcorderLower lower = handoff.GetComponent<CutsceneCamcorderLower>();
        if (lower == null)
        {
            lower = handoff.gameObject.AddComponent<CutsceneCamcorderLower>();
        }

        SetRef(lower, "player", player);
        SetRef(lower, "camcorderOverlay", camcorderOverlay);
        SetRef(handoff, "camcorderLower", lower);
    }

    // ───────────────────────────── Timeline ─────────────────────────────
    // 0      검은 화면에서 밝아짐 (캠코더 화면)
    // 0.5~9  열린 정문으로 걸어 들어와 접수대 쪽을 봄
    // 9.3    "…계세요? …당연히 없겠지."
    // 12.6~  뒤에서 문이 끼익 밀리기 시작 → 13.0~14.3 뒤돌아봄 → 14.6 쾅 (흔들림·화면 튐)
    // 15.8   "…바, 바람인가."
    // 21     조작 시작 — 손에 든 실제 캠코더를 본편처럼 내림 (CutsceneCamcorderLower)
    private static void BuildTimeline(CutsceneCameraPath path, CutsceneCameraLookAt lookAt, CutsceneCameraShake shake,
        CutsceneDoorSwing door, CutsceneBlackScreen black, CutsceneCamcorderOverlay camcorder, CutscenePlayerHandoff handoff)
    {
        const double end = 21.0;
        string timelinePath = DataDir + "/Prologue_Opening.playable";
        AssetDatabase.DeleteAsset(timelinePath);
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, timelinePath);

        GameObject directorGo = new GameObject("PrologueOpening");
        PlayableDirector director = directorGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;

        SubtitleTrack subtitles = timeline.CreateTrack<SubtitleTrack>(null, "Subtitles");
        AddSubtitle(subtitles, 9.3, 12.2, "나", "…계세요? …당연히 없겠지.", true);
        AddSubtitle(subtitles, 15.8, 19.2, "나", "…바, 바람인가.", true);

        CutsceneBlendTrack blackTrack = CreateBlendTrack(timeline, director, "Black Screen", black);
        AddBlendClip(blackTrack, 0, 1.6, 0, 1.6, "검은 화면 → 밝아짐");
        AddBlendClip(blackTrack, 14.62, 14.7, 0, 0.04, "화면 튐 1");
        AddBlendClip(blackTrack, 14.78, 14.83, 0, 0.03, "화면 튐 2");

        CutsceneBlendTrack camcorderTrack = CreateBlendTrack(timeline, director, "Camcorder Overlay", camcorder);
        // 끝까지 켜 둠 — 내리기는 전환 때 손에 든 실제 캠코더로 (CutsceneCamcorderLower)
        AddBlendClip(camcorderTrack, 0, end, 0, 0, "캠코더 화면");

        CameraPathTrack pathTrack = timeline.CreateTrack<CameraPathTrack>(null, "Camera Path");
        director.SetGenericBinding(pathTrack, path);
        AddPathClip(pathTrack, 0.5, 9.0, 0f, 1f, "정문 → 로비");
        AddPathClip(pathTrack, 9.0, end, 1f, 1f, "멈춰 섬 (LookAt·흔들림 기준)");

        CutsceneBlendTrack doorTrack = CreateBlendTrack(timeline, director, "Front Door", door);
        AddBlendClip(doorTrack, 12.6, end, 2.0, 0, "끼익… 쾅");

        CutsceneBlendTrack lookTrack = CreateBlendTrack(timeline, director, "Look Back", lookAt);
        AddBlendClip(lookTrack, 13.0, end, 1.3, 0, "뒤돌아봄");

        CutsceneBlendTrack shakeTrack = CreateBlendTrack(timeline, director, "Shake", shake);
        AddBlendClip(shakeTrack, 14.58, 15.5, 0.03, 0.85, "쾅 흔들림");

        EditorUtility.SetDirty(timeline);

        CutsceneDirector cutscene = directorGo.AddComponent<CutsceneDirector>();
        SerializedObject so = new SerializedObject(cutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("nextSceneName").stringValue = "";
        so.FindProperty("blackScreen").objectReferenceValue = black;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventBase onFinished = (UnityEventBase)typeof(CutsceneDirector)
            .GetField("onFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(cutscene);
        UnityEventTools.AddPersistentListener((UnityEvent)onFinished, handoff.Handoff);
        EditorUtility.SetDirty(cutscene);
    }

    private static void AddSubtitle(SubtitleTrack track, double start, double end, string speaker, string text, bool skipToClipEnd)
    {
        TimelineClip clip = track.CreateClip<SubtitleClip>();
        clip.start = start;
        clip.duration = end - start;
        SubtitleClip asset = (SubtitleClip)clip.asset;
        asset.speaker = speaker;
        asset.text = text;
        asset.waitForClick = true;
        asset.skipToClipEndOnClick = skipToClipEnd;
        clip.displayName = $"{speaker}: {text}";
    }

    private static void AddPathClip(CameraPathTrack track, double start, double end, float from, float to, string label)
    {
        TimelineClip clip = track.CreateClip<CameraPathClip>();
        clip.start = start;
        clip.duration = end - start;
        clip.displayName = label;
        CameraPathClip asset = (CameraPathClip)clip.asset;
        asset.startProgress = from;
        asset.endProgress = to;
    }

    private static CutsceneBlendTrack CreateBlendTrack(TimelineAsset timeline, PlayableDirector director, string name, CutsceneBlendTarget target)
    {
        CutsceneBlendTrack track = timeline.CreateTrack<CutsceneBlendTrack>(null, name);
        director.SetGenericBinding(track, target);
        return track;
    }

    private static void AddBlendClip(CutsceneBlendTrack track, double start, double end, double easeIn, double easeOut, string label)
    {
        TimelineClip clip = track.CreateClip<CutsceneBlendClip>();
        clip.start = start;
        clip.duration = end - start;
        clip.easeInDuration = easeIn;
        clip.easeOutDuration = easeOut;
        clip.displayName = label;
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private static T CreateOverlay<T>(string name, int sortingOrder, float initialAlpha) where T : CutsceneOverlayBase
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        T overlay = go.AddComponent<T>();
        SerializedObject so = new SerializedObject(overlay);
        so.FindProperty("sortingOrder").intValue = sortingOrder;
        so.FindProperty("initialAlpha").floatValue = initialAlpha;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<CanvasGroup>().alpha = initialAlpha;
        return overlay;
    }

    private static Transform CreatePoint(string name, Vector3 position, Transform parent)
    {
        Transform point = new GameObject(name).transform;
        point.SetParent(parent);
        point.position = position;
        return point;
    }

    /// <summary>쾅 닫힌 정문을 처음 열어 볼 때 독백. 씬에 이미 있는 정문에도 씀(메뉴 없이 리플렉션 호출).</summary>
    public static void SetFrontDoorMonologue(SealedDoorInteract door)
    {
        string[] lines =
        {
            "뭐야… 왜 안 열리지…? 문이 닫히는 충격으로 잠겨 버렸나?",
            "다른 길을 찾아봐야겠어…",
        };

        SerializedObject so = new SerializedObject(door);
        SerializedProperty list = so.FindProperty("firstTryLines");
        list.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty line = list.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speaker").stringValue = "나";
            line.FindPropertyRelative("text").stringValue = lines[i];
            line.FindPropertyRelative("holdSeconds").floatValue = 0f; // 글자 수에 맞춰 자동
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRef(Object target, string property, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string property, float value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(property).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    // Intro 바로 다음
    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            return;
        }

        int introIndex = scenes.FindIndex(s => s.path.EndsWith("Demo/Intro.unity"));
        scenes.Insert(introIndex >= 0 ? introIndex + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
