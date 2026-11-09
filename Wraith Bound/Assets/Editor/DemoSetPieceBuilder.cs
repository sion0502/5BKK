using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 맵(Chapter1_Demo) 연출 배치. Tools > Story > Demo Set Pieces > ...
/// 각 연출은 Demo_Story 아래 자기 묶음만 지우고 다시 만듦.
/// </summary>
public static class DemoSetPieceBuilder
{
    private const string ScenePath = "Assets/Scenes/Demo/Chapter1_Demo.unity";
    private const string LampPrefab = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/Hospital_Environment/HH_Ceiling_Lamp_01.prefab";
    private const string OnMat = "Assets/ExternalAssets/The_Horror_Hospital/Materials/Hospital/HH_Ceiling_Armstrong.mat";
    private const string OffMat = "Assets/Story/Intro/PowerOff/HH_Ceiling_Armstrong_Off.mat";

    // 시작 방(+자 방 타일, 칸 (1,0)) 가운데 남북 통로·북쪽 출입구 복도의 x (실측: 출입구 복도 197.9~201.9, 방 통로 195~204.9)
    private const float HallX = 199.89f;

    [MenuItem("Tools/Story/Demo Set Pieces/1 Start Room")]
    public static void StartRoom()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[DemoSetPiece] " + ScenePath + " 를 열고 실행");
            return;
        }

        Physics.SyncTransforms();
        Transform root = Group(scene, "SetPiece1_StartRoom");

        // 형광등 7개: 방 가운데 통로 4 + 북쪽 복도 3, 입구 쪽부터 (로비를 가로지르는 Xenosaurid는 '짜친다'는 피드백으로 뺌)
        float[] zs = { -24f, -8f, 8f, 24f, 42f, 54f, 66f };
        var lights = new PrologueCeilingLight[zs.Length];
        for (int i = 0; i < zs.Length; i++)
        {
            // 방 통로(앞 4개)는 넓고(10m) 천장이 높아 더 세게
            lights[i] = Lamp(root, "Fluorescent_" + (i + 1), new Vector3(HallX, 0f, zs[i]), i < 4 ? 24f : 10f, i < 4 ? 14f : 10f);
        }

        // 남쪽 출입구 복도 끝 문(z≈−33) 안쪽 — 문 앞이면 불이 켜져도 닫힌 문에 가려 안 보였음
        StoryZone roomZone = Zone(root, "Zone_RoomEnter", new Bounds(new Vector3(HallX, 1f, -28f), new Vector3(10f, 2f, 4f)));

        DemoStartSequence seq = root.gameObject.AddComponent<DemoStartSequence>();
        var so = new SerializedObject(seq);
        so.FindProperty("roomZone").objectReferenceValue = roomZone;
        SetArray(so.FindProperty("lights"), lights);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[DemoSetPiece] ① 시작 방 — 형광등 " + lights.Length);
    }

    // 병동(3자 타일, 칸 (0,0), 중심 (99.89, 1.19)): 큰 홀 x 66~132·z −7~15, 동쪽 출입구(방에서 옴) z≈2, 북쪽 복도 x 97.5~102 → 수술실
    private const string FigureModel = "Assets/ExternalAssets/Zombie_Animations/Models/Android_SkeletalMesh.fbx";
    private const string FigureMat = "Assets/Story/Demo/M_ShadowFigure.mat";

    [MenuItem("Tools/Story/Demo Set Pieces/2 Ward")]
    public static void Ward()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[DemoSetPiece] " + ScenePath + " 를 열고 실행");
            return;
        }

        Physics.SyncTransforms();
        Transform root = Group(scene, "SetPiece2_Ward");
        StoryZone zone = Zone(root, "Zone_WardHall", new Bounds(new Vector3(128f, 1f, 2f), new Vector3(4f, 2f, 10f)));
        Transform target = Point(root, "PointTarget", new Vector3(99.89f, 2.3f, 27f));

        // 그림자 사람: 동쪽 입구에서 봤을 때 불 켜진 북쪽 복도 입구를 등지고(검은 실루엣이 드러남) 복도 쪽을 오른팔로 가리킴, 고개만 돌려 플레이어를 봄
        // (어두운 홀에 검은 몸만 두면 나이트비전에서도 안 보였음)
        GameObject holder = new GameObject("ShadowFigure");
        holder.transform.SetParent(root, false);
        Vector3 mouth = new Vector3(99.89f, 0f, 15f);
        Vector3 figurePos = Vector3.Lerp(new Vector3(129f, 0f, 2f), mouth, 0.6f); // 들어오는 자리에서 본 복도 입구(불 켜진 곳) 바로 앞
        figurePos.y = Floor(figurePos);
        Vector3 face = mouth - figurePos;
        face.y = 0f;
        holder.transform.SetPositionAndRotation(figurePos, Quaternion.LookRotation(face));
        // 바로 뒤 홀 바닥에 빛 웅덩이 + 복도 안 등 — 검은 몸이 밝은 바닥·벽 앞에 겹쳐 보이게
        PrologueCeilingLight[] backLights =
        {
            Lamp(root, "HallLamp", Vector3.Lerp(figurePos, mouth, 0.35f), 22f, 12f),
            Lamp(root, "CorridorLamp", new Vector3(99.89f, 0f, 19f), 16f, 10f),
        };
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FigureModel), holder.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 1.1f;
        Material mat = ShadowMaterial();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(mat, r.sharedMaterials.Length));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        int layer = LayerMask.NameToLayer(CamcorderOnlyApparition.LayerName);
        foreach (Transform t in holder.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }

        Animator anim = model.GetComponent<Animator>();
        anim.runtimeAnimatorController = null;
        anim.applyRootMotion = false;
        DemoPointingFigure pose = model.AddComponent<DemoPointingFigure>();
        var pso = new SerializedObject(pose);
        pso.FindProperty("animator").objectReferenceValue = anim;
        pso.FindProperty("pointTarget").objectReferenceValue = target;
        pso.ApplyModifiedPropertiesWithoutUndo();

        CamcorderOnlyApparition app = holder.AddComponent<CamcorderOnlyApparition>();
        var aso = new SerializedObject(app);
        aso.FindProperty("focusPoint").objectReferenceValue = anim.GetBoneTransform(HumanBodyBones.Chest);
        aso.ApplyModifiedPropertiesWithoutUndo();

        // 가리키는 복도의 문 (남→북 순)
        var doors = new System.Collections.Generic.List<DoorClick>();
        foreach (DoorClick d in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            Vector3 p = d.transform.position;
            if (p.x > 95f && p.x < 105f && p.z > 14f && p.z < 40f && d.GetComponentInParent<LockedDoor>() == null)
            {
                doors.Add(d);
            }
        }

        doors.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

        DemoWardSequence seq = root.gameObject.AddComponent<DemoWardSequence>();
        var so = new SerializedObject(seq);
        so.FindProperty("zone").objectReferenceValue = zone;
        so.FindProperty("figure").objectReferenceValue = app;
        SetArray(so.FindProperty("backLights"), backLights);
        SetArray(so.FindProperty("doorsToOpen"), doors.ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[DemoSetPiece] ② 병동 — 그림자 사람, 열릴 문 " + doors.Count);
    }

    private static Material ShadowMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(FigureMat);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Story/Demo");
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, FigureMat);
        }

        mat.SetColor("_BaseColor", new Color(0.035f, 0.035f, 0.04f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static float Floor(Vector3 p)
    {
        return Physics.Raycast(new Vector3(p.x, 2f, p.z), Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
    }

    // ── 공용 ─────────────────────────────────────────

    private static Transform Group(Scene scene, string name)
    {
        GameObject story = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "Demo_Story")
            {
                story = go;
            }
        }

        if (story == null)
        {
            story = new GameObject("Demo_Story");
            SceneManager.MoveGameObjectToScene(story, scene);
        }

        Transform old = story.transform.Find(name);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform t = new GameObject(name).transform;
        t.SetParent(story.transform, false);
        return t;
    }

    // 천장(위로 레이)에 붙인 형광등 + 아래로 넓게 비추는 스포트, 처음엔 꺼짐
    private static PrologueCeilingLight Lamp(Transform parent, string name, Vector3 floorPos, float intensity = 10f, float range = 10f)
    {
        float ceiling = Physics.Raycast(floorPos + Vector3.up * 1.5f, Vector3.up, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point.y : floorPos.y + 6f;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(floorPos.x, ceiling - 0.01f, floorPos.z);
        GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab), go.transform);
        mesh.transform.localRotation = Quaternion.Euler(0f, 90f, 180f); // 뒤집어 아래를 보게, 통로 방향(남북)으로 길게
        // 메쉬 기준점이 모서리라 실제 모양 중심이 등 위치에 오게 옮김
        Bounds mb = mesh.GetComponentInChildren<Renderer>().bounds;
        Vector3 shift = go.transform.position - mb.center;
        mesh.transform.position += new Vector3(shift.x, 0f, shift.z);
        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        light.type = LightType.Spot;
        light.spotAngle = 130f;
        light.innerSpotAngle = 70f;
        light.color = new Color(0.86f, 0.94f, 1f);
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Hard;
        light.enabled = false;

        AudioSource audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.maxDistance = 30f;
        PrologueCeilingLight ceilingLight = go.AddComponent<PrologueCeilingLight>();
        var so = new SerializedObject(ceilingLight);
        so.FindProperty("lightSource").objectReferenceValue = light;
        SetArray(so.FindProperty("renderers"), mesh.GetComponentsInChildren<Renderer>(true));
        so.FindProperty("onMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(OnMat);
        so.FindProperty("offMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(OffMat);
        so.FindProperty("startOn").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach (Renderer r in mesh.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(OffMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return ceilingLight;
    }

    private static StoryZone Zone(Transform parent, string name, Bounds bounds)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = bounds.center;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = bounds.size;
        return go.AddComponent<StoryZone>();
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        return t;
    }

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
