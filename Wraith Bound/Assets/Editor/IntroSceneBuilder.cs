using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

/// <summary>
/// 시연용 C1 인트로 씬(Assets/Scenes/Demo/Intro.unity)을 처음 한 번 생성합니다.
/// 메뉴: Tools > Story > Build Intro Scene
/// 이미 씬이 있으면 덮어쓸지 묻습니다 — 덮어쓰면 손으로 꾸민 배치·Timeline 수정이 사라지니 주의.
/// </summary>
public static class IntroSceneBuilder
{
    private const string SceneDir = "Assets/Scenes/Demo";
    private const string ScenePath = SceneDir + "/Intro.unity";
    private const string DataDir = "Assets/Story/Intro";
    private const string NextScene = "Prologue";

    private const string APH = "Assets/ExternalAssets/Abandoned_Psychiatric_Hospitals/Prefabs/";
    private const string HH = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/";
    private const string ViewfinderDir = "Assets/Sprites/Item/Camcorder/Viewfinder/";

    // HH_Demo 좌표 기준. 컷 A: 검문소 바깥 → 게이트 앞(여기서 기획서), 컷 B(캠코더 컷 편집): 주차장 → 병원 입구 캐노피
    private static readonly Vector3[] PathA =
    {
        new Vector3(41f, 0f, 104f),
        new Vector3(40.6f, 0f, 98f),
        new Vector3(40.2f, 0f, 92.5f),
        new Vector3(39.8f, 0f, 89.5f),
    };

    private static readonly Vector3[] PathB =
    {
        new Vector3(33f, 0f, 19f),
        new Vector3(27f, 0f, 10.5f),
        new Vector3(19f, 0f, 6.2f),
        new Vector3(11f, 0f, 5.6f),
        new Vector3(6.5f, 0f, 5.5f),
    };

    private static readonly Vector3 HospitalLookPoint = new Vector3(8f, 10f, 40f);
    private static readonly Vector3 EntranceLookPoint = new Vector3(0.5f, 1.7f, 5.5f);

    [MenuItem("Tools/Story/Build Intro Scene")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Intro 씬 다시 만들기",
                "Intro 씬과 Timeline을 새로 만들어 덮어씁니다. 손으로 고친 내용은 사라집니다. 계속할까요?", "덮어쓰기", "취소"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder("Assets/Scenes", "Demo");
        EnsureFolder("Assets", "Story");
        EnsureFolder("Assets/Story", "Intro");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Color fogColor = new Color(0.06f, 0.07f, 0.09f);
        BuildLighting(fogColor);

        // 배경: HH 데모 외부 맵을 가져와 전기 끊긴 폐병원으로
        Transform env = IntroEnvironmentBuilder.ImportHospitalDemo(scene);
        Material grassMaterial = GetOrCreateGrassMaterial();
        IntroEnvironmentBuilder.PowerOff(env);
        Material hedgeMaterial = AssetDatabase.LoadAssetAtPath<Material>(HH.Replace("Prefabs/", "Materials/") + "Vegetation/HH_Bushes_01.mat");
        IntroEnvironmentBuilder.FixBrokenFoliage(env, grassMaterial, hedgeMaterial);
        IntroEnvironmentBuilder.ScatterOvergrowth(env, LoadGrass(), LoadBushes(), grassMaterial,
            new Rect(0f, -5f, 56f, 118f), 1400, 90, DistanceToPaths);

        Transform lookRoot = new GameObject("LookTargets").transform;
        Transform hospitalLook = CreatePoint("Look_Hospital", HospitalLookPoint, lookRoot);
        Transform entranceLook = CreatePoint("Look_Entrance", EntranceLookPoint, lookRoot);

        Camera cam = BuildCamera(fogColor);
        CutsceneCameraPath pathA = AddPath(cam, "CameraPath_A_Checkpoint", PathA, hospitalLook, 0.55f);
        CutsceneCameraPath pathB = AddPath(cam, "CameraPath_B_Entrance", PathB, entranceLook, 0.5f);
        BuildPostVolume();

        CutsceneHeldDocument document = BuildHeldDocument(cam.transform);

        CutsceneBlackScreen black = CreateOverlay<CutsceneBlackScreen>("Overlay_Black", 600, 1f);
        CutsceneCamcorderOverlay camcorder = CreateOverlay<CutsceneCamcorderOverlay>("Overlay_Camcorder", 500, 0f);
        SetRef(camcorder, "frameSprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Center_Frame.png"));
        SetRef(camcorder, "recordingDotSprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Recording_Dot.png"));
        SetRef(camcorder, "batterySprite", AssetDatabase.LoadAssetAtPath<Sprite>(ViewfinderDir + "Battery_4.png"));

        BuildTimeline(pathA, pathB, black, document, camcorder);

        pathA.SetProgress(0f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AddToBuildSettings();

        Debug.Log($"[IntroSceneBuilder] {ScenePath} 생성 완료 (경로 A {pathA.TotalLength:F1}m, B {pathB.TotalLength:F1}m)");
    }

    private static void BuildLighting(Color fogColor)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.14f);
        // HH 아스팔트 등은 Smoothness 1이라 환경 반사 1이면 바닥이 눈처럼 하얗게 뜸
        RenderSettings.reflectionIntensity = 0.45f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = 0.018f; // 검문소에서 70m 떨어진 병원 건물이 안개 속에 흐릿하게 보이는 정도

        Light moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.63f, 0.82f);
        moon.intensity = 0.35f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
    }

    private static GameObject[] LoadGrass()
    {
        return new[]
        {
            Load(APH + "Vegetation/Grass_01.prefab"), Load(APH + "Vegetation/Grass_02.prefab"),
            Load(APH + "Vegetation/Grass_03.prefab"), Load(APH + "Vegetation/Grass_04.prefab"),
        };
    }

    // Abandoned 팩 덤불은 셰이더가 URP에서 분홍색으로 깨져서 HH 덤불만 사용
    private static GameObject[] LoadBushes()
    {
        return new[]
        {
            Load(HH + "Vegetation/HH_Bushes_01.prefab"), Load(HH + "Vegetation/HH_Bushes_02.prefab"),
            Load(HH + "Vegetation/HH_Bushes_03.prefab"), Load(HH + "Vegetation/HH_Bushes_04.prefab"),
        };
    }

    /// <summary>
    /// Abandoned 팩 풀(Leaves.shader)은 URP에서 분홍색으로 깨짐 → HH 덤불 머티리얼(HH_Leaves 셰이더)을 복제해
    /// 풀 텍스처만 바꾼 머티리얼을 만들어 씀.
    /// </summary>
    private static Material GetOrCreateGrassMaterial()
    {
        string path = DataDir + "/Intro_Grass.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Material source = AssetDatabase.LoadAssetAtPath<Material>(HH.Replace("Prefabs/", "Materials/") + "Vegetation/HH_Bushes_01.mat");
        Material grassSource = AssetDatabase.LoadAssetAtPath<Material>(APH.Replace("Prefabs/", "Materials/") + "Vegetation/Grass_01.mat");
        if (source == null || grassSource == null)
        {
            Debug.LogWarning("[IntroSceneBuilder] 풀 머티리얼 원본을 찾지 못함");
            return null;
        }

        material = new Material(source);
        material.SetTexture("_Base_Color", grassSource.GetTexture("_Base_Color"));
        material.SetTexture("_Normal", grassSource.GetTexture("_Normal"));
        material.SetColor("_Color", new Color(0.62f, 0.68f, 0.55f, 1f));
        material.SetFloat("_AlphaCutoff", 0.3f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Camera BuildCamera(Color fogColor)
    {
        GameObject camGo = new GameObject("IntroCamera") { tag = "MainCamera" };
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 58f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fogColor;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return cam;
    }

    /// <summary>카메라에 경로 컴포넌트를 하나 더 붙임 (컷마다 하나, Timeline 트랙이 각각 바인딩).</summary>
    private static CutsceneCameraPath AddPath(Camera cam, string name, Vector3[] points, Transform lookTarget, float lookStart)
    {
        Transform pathRoot = new GameObject(name).transform;
        Transform[] waypoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            waypoints[i] = CreatePoint("Waypoint_" + i, points[i], pathRoot);
        }

        CutsceneCameraPath path = cam.gameObject.AddComponent<CutsceneCameraPath>();
        SerializedObject so = new SerializedObject(path);
        SerializedProperty array = so.FindProperty("waypoints");
        array.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        }

        so.FindProperty("lookTarget").objectReferenceValue = lookTarget;
        so.FindProperty("lookTargetStart").floatValue = lookStart;
        so.ApplyModifiedPropertiesWithoutUndo();
        return path;
    }

    private static Transform CreatePoint(string name, Vector3 position, Transform parent)
    {
        Transform point = new GameObject(name).transform;
        point.SetParent(parent);
        point.position = position;
        return point;
    }

    private static void BuildPostVolume()
    {
        string profilePath = DataDir + "/Intro_Volume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            // 캠코더 녹화 느낌: 비네트·그레인·색수차·채도 낮춤
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.45f);
            profile.Add<ChromaticAberration>(true).intensity.Override(0.18f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.6f);
            color.saturation.Override(-35f);
            color.contrast.Override(12f);

            foreach (VolumeComponent component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
        }

        Volume volume = new GameObject("PostVolume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    /// <summary>
    /// 카메라 자식으로 손에 든 기획서(종이 + 3D 글자) + 종이 조명 + 배경 블러(DoF) Volume을 만듭니다.
    /// 종이 크기는 A4(0.21×0.297m). 글자는 종이 자식 TextMeshPro라 씬에서 바로 고칠 수 있음.
    /// </summary>
    private static CutsceneHeldDocument BuildHeldDocument(Transform cameraTransform)
    {
        GameObject root = new GameObject("HeldDocument");
        root.transform.SetParent(cameraTransform, false);

        // 종이
        GameObject paper = new GameObject("Paper");
        paper.transform.SetParent(root.transform, false);

        string paperMatPath = DataDir + "/Intro_Paper.mat";
        Material paperMat = AssetDatabase.LoadAssetAtPath<Material>(paperMatPath);
        if (paperMat == null)
        {
            paperMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            paperMat.SetColor("_BaseColor", new Color(0.86f, 0.82f, 0.72f));
            paperMat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(paperMat, paperMatPath);
        }

        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "Sheet";
        Object.DestroyImmediate(sheet.GetComponent<Collider>());
        sheet.transform.SetParent(paper.transform, false);
        sheet.transform.localScale = new Vector3(0.21f, 0.297f, 1f);
        Renderer sheetRenderer = sheet.GetComponent<Renderer>();
        sheetRenderer.sharedMaterial = paperMat;
        sheetRenderer.shadowCastingMode = ShadowCastingMode.Off;

        TMPro.TMP_FontAsset font = MenuUIUtility.LoadPolFont();
        Color ink = new Color(0.12f, 0.1f, 0.09f);
        CreatePaperText(paper.transform, "Title", font, "출입 금지된 섬의\n폐병원에서 3일 버티기", 0.016f, ink,
            new Vector3(0f, 0.105f, -0.001f), new Vector2(0.18f, 0.06f), TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        CreatePaperText(paper.transform, "Body", font,
            "준비물: 손전등, 식량, 핸드폰, 캠코더, 라이터\n\n촬영지: 지도에 없는 섬,\n20년 전 폐쇄된 병원\n\n제보: 익명\n<i>\"그 병원은 아직 문이 열려 있습니다.\"</i>",
            0.0105f, ink, new Vector3(0f, -0.03f, -0.001f), new Vector2(0.17f, 0.19f), TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Normal);

        // 종이 조명 (주인공 손전등 느낌, 들고 있을 때만 켜짐)
        Light light = new GameObject("PaperLight").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0.12f, 0.12f, 0.05f);
        light.type = LightType.Point;
        light.range = 1.2f;
        light.color = new Color(1f, 0.9f, 0.75f);
        light.shadows = LightShadows.None;
        light.intensity = 0f;

        // 배경 블러 Volume (Gaussian DoF: 0.8m 너머부터 흐려짐 → 손에 든 종이는 선명)
        string dofPath = DataDir + "/Intro_DocumentBlur.asset";
        VolumeProfile dofProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dofPath);
        if (dofProfile == null)
        {
            dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(dofProfile, dofPath);
            DepthOfField dof = dofProfile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0.8f);
            dof.gaussianEnd.Override(3f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(true);
            dofProfile.Add<Vignette>(true).intensity.Override(0.5f);
            foreach (VolumeComponent component in dofProfile.components)
            {
                AssetDatabase.AddObjectToAsset(component, dofProfile);
            }

            EditorUtility.SetDirty(dofProfile);
        }

        Volume blur = new GameObject("DocumentBlurVolume").AddComponent<Volume>();
        blur.isGlobal = true;
        blur.priority = 10f;
        blur.weight = 0f;
        blur.sharedProfile = dofProfile;

        CutsceneHeldDocument held = root.AddComponent<CutsceneHeldDocument>();
        SerializedObject so = new SerializedObject(held);
        so.FindProperty("paper").objectReferenceValue = paper.transform;
        so.FindProperty("blurVolume").objectReferenceValue = blur;
        so.FindProperty("paperLight").objectReferenceValue = light;
        so.ApplyModifiedPropertiesWithoutUndo();

        paper.SetActive(false);
        return held;
    }

    private static void CreatePaperText(Transform parent, string name, TMPro.TMP_FontAsset font, string text, float size, Color color,
        Vector3 position, Vector2 area, TMPro.TextAlignmentOptions alignment, TMPro.FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        TMPro.TextMeshPro label = go.AddComponent<TMPro.TextMeshPro>();
        ((RectTransform)go.transform).sizeDelta = area;
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size * 10f; // TextMeshPro(3D)는 fontSize 1 ≈ 0.1m
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = style;
        label.textWrappingMode = TMPro.TextWrappingModes.Normal;
        label.lineSpacing = 10f;
    }

    private static void BuildTimeline(CutsceneCameraPath pathA, CutsceneCameraPath pathB, CutsceneBlackScreen black,
        CutsceneHeldDocument document, CutsceneCamcorderOverlay camcorder)
    {
        string timelinePath = DataDir + "/Intro_Timeline.playable";
        AssetDatabase.DeleteAsset(timelinePath);
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, timelinePath);

        GameObject directorGo = new GameObject("IntroCutscene");
        PlayableDirector director = directorGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;

        // 대사는 전부 클릭으로 넘김. 검은 화면·멈춰 선 구간은 클릭 시 다음으로 바로 건너뛰고(skip=true),
        // 카메라가 움직이는 구간은 건너뛰지 않음(skip=false, 클립 끝까지 클릭 없으면 그 자리에서 대기).
        SubtitleTrack subtitles = timeline.CreateTrack<SubtitleTrack>(null, "Subtitles");
        AddSubtitle(subtitles, 0.8, 4.6, "선장", "…여기까지요. 더는 못 들어갑니다.", true);
        AddSubtitle(subtitles, 5.0, 10.4, "선장", "저 섬엔 이십 년 동안 아무도 안 들어갔소. 들어간 사람은… 안 나왔고.", true);
        AddSubtitle(subtitles, 10.8, 15.6, "선장", "사흘 뒤 이 시간에 오겠소. 그때 선착장에 없으면, 안 기다리오.", true);
        AddSubtitle(subtitles, 16.2, 19.8, "나", "자, 녹화 들어갑니다. 여러분, 드디어 왔습니다.", true);
        AddSubtitle(subtitles, 32.0, 39.0, "나", "'출입 금지된 섬의 폐병원에서 3일 버티기.' 제보 메일 하나 믿고 여기까지 왔어요.", true);
        AddSubtitle(subtitles, 43.5, 49.5, "나", "…생각보다 조용하네요. 새 소리도 안 들려요.", false);
        AddSubtitle(subtitles, 54.0, 59.0, "나", "…문이 열려 있네. 제보대로야.", false);

        CutsceneBlendTrack blackTrack = CreateBlendTrack(timeline, director, "Black Screen", black);
        AddBlendClip(blackTrack, 0, 21.5, 0, 1.5, "검은 화면");
        AddBlendClip(blackTrack, 40.6, 41.4, 0.15, 0.25, "컷 (캠코더 편집)");
        AddBlendClip(blackTrack, 59.8, 62.3, 1.8, 0, "암전 (병원 입구)");

        CutsceneBlendTrack camcorderTrack = CreateBlendTrack(timeline, director, "Camcorder Overlay", camcorder);
        AddBlendClip(camcorderTrack, 20.0, 62.3, 0.2, 0, "캠코더 화면");

        // 검문소 게이트 앞에서 멈춰 서서 기획서를 들어 보여줌 — 배경 블러
        CutsceneBlendTrack documentTrack = CreateBlendTrack(timeline, director, "Held Document", document);
        AddBlendClip(documentTrack, 30.5, 40.0, 1.0, 1.0, "기획서 들기");

        CameraPathTrack pathTrackA = timeline.CreateTrack<CameraPathTrack>(null, "Camera Path A (검문소)");
        director.SetGenericBinding(pathTrackA, pathA);
        AddPathClip(pathTrackA, 20.0, 30.0, 0f, 1f, "검문소 게이트까지");

        CameraPathTrack pathTrackB = timeline.CreateTrack<CameraPathTrack>(null, "Camera Path B (병원 입구)");
        director.SetGenericBinding(pathTrackB, pathB);
        AddPathClip(pathTrackB, 41.0, 61.0, 0f, 1f, "주차장 → 병원 입구");

        EditorUtility.SetDirty(timeline);

        CutsceneDirector cutscene = directorGo.AddComponent<CutsceneDirector>();
        SerializedObject so = new SerializedObject(cutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("nextSceneName").stringValue = NextScene;
        so.FindProperty("blackScreen").objectReferenceValue = black;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        clip.displayName = $"{speaker}: {(text.Length > 14 ? text.Substring(0, 14) + "…" : text)}";
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

    private static void SetRef(Object target, string property, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float DistanceToPaths(Vector3 p)
    {
        return Mathf.Min(DistanceToPolyline(p, PathA), DistanceToPolyline(p, PathB));
    }

    private static float DistanceToPolyline(Vector3 p, Vector3[] points)
    {
        p.y = 0f;
        float best = float.MaxValue;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 ab = points[i + 1] - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
        }

        return best;
    }

    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[IntroSceneBuilder] 프리팹 없음: " + path);
        }

        return prefab;
    }

    private static GameObject Place(GameObject prefab, Vector3 position, float yaw, float scale, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            return;
        }

        int menuIndex = scenes.FindIndex(s => s.path.EndsWith("MainMenu.unity"));
        scenes.Insert(menuIndex >= 0 ? menuIndex + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
