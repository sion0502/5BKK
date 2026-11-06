using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 프롤로그 조작 구간 연출 배치: NavMesh, 철문·비상구, 비상구 표시등, E1 캠코더로만 보이는 환자, 진료실 일지 #0,
/// 간호사(PrologueNurse), 트리거 구역(StoryZone), 경로 지점, 사망 연출, 진행 감독(PrologueDirector — 조작 시작 때 켜짐),
/// 튜토리얼 짧게(onlySteps). PrologueSceneBuilder가 맵·플레이어를 만든 뒤 호출. 좌표는 PrologueLayout.
/// </summary>
public static class PrologueEventBuilder
{
    private const string NursePrefab = "Assets/Model/Enemy/MaskGhost/Prefab/NurseGhost.prefab";
    private const string NavMeshPath = "Assets/Story/Prologue/Prologue_NavMesh.asset";
    private const string KeyPrefab = "Assets/Prefab/Item/World_Item/Active/World_Key.prefab";

    private const string JournalTitle = "기록 — 10월 3일";
    private const string JournalBody =
        "무당이 죽었다. 사고였다. 누구도 원하지 않았다.\n" +
        "숨이 끊어지기 전, 그 노인은 나를 똑바로 보며 말했다.\n\n" +
        "<i>\"이 섬에 발을 들였던 자는, 모두 죽는다.\"</i>\n\n" +
        "그날 밤부터 병동에서 소리가 난다.\n" +
        "3병동 간호사가 출근하지 않았다. 아니, 출근했다.\n" +
        "복도 끝에 서 있는 걸 봤다. 기록용 캠코더 화면 속에서만.";

    /// <returns>조작 시작 때 켤 진행 감독 오브젝트</returns>
    public static GameObject Build(Transform map, Camera playerCamera, GameObject tutorial)
    {
        Transform root = new GameObject("Prologue_Events").transform;

        CutsceneDoorSwing steelDoor = SetupSteelDoor(PrologueWingBuilder.SteelDoor);
        SetupExitDoor(PrologueWingBuilder.ExitDoor);
        BuildEmergencyExitLight(root);
        BakeNavMesh(map, steelDoor);

        ExcludeCamcorderOnlyLayer(playerCamera);
        CamcorderOnlyApparition apparition = BuildApparition(root);
        StoryDocumentPickup journal = BuildJournal(root);
        PlaceWardKey(root);
        PrologueNurse nurse = BuildNurse(root);

        Transform zones = Child(root, "Zones");
        StoryZone corridorA = Zone(zones, "Zone_E1_CorridorA", PrologueLayout.CorridorAEntryZone);
        StoryZone corridorBMid = Zone(zones, "Zone_CorridorB_Mid", PrologueLayout.CorridorBMidZone);
        StoryZone exitZone = Zone(zones, "Zone_ExitDoor", PrologueLayout.ExitDoorZone);

        Transform points = Child(root, "Points");
        Transform nurseSpawn = Point(points, "Nurse_Spawn", PrologueLayout.NurseSpawn, 180f);
        Transform corridorAMid = Point(points, "Nurse_CorridorAMid", new Vector3(6f, 0f, 26f), 180f);
        Transform wardEntrance = Point(points, "Nurse_WardEntrance", PrologueLayout.WardEntrance, 90f);
        // 숨는 곳(트롤리) 앞 지나가기: 왼쪽에서 들어와 트롤리 앞을 지나 병실 뒷문 → 긴 복도 B 안쪽에서 사라짐
        Transform hidePassStart = Point(points, "Nurse_PassStart", PrologueLayout.HidePassStart, 90f);
        Transform hidePassFront = Point(points, "Nurse_PassFront", PrologueLayout.HidePassFront, 90f);
        Transform hideSpotLookAt = Point(points, "Nurse_HideSpotLookAt", PrologueLayout.HideSpotPosition + Vector3.up * 0.5f, 0f);
        Transform wardBackDoor = Point(points, "Nurse_WardBackDoor", PrologueLayout.WardBackDoor, 90f);
        Transform nurseExit = Point(points, "Nurse_Exit", new Vector3(24f, 0f, 30f), 90f);

        // E2: 괴물 등장은 소리로만 (철문 쪽에서 들림, 클립은 팀이 나중에)
        GameObject e2Sound = new GameObject("E2_Sound");
        e2Sound.transform.SetParent(root, false);
        e2Sound.transform.position = PrologueLayout.NurseSpawn + Vector3.up * 1.5f;
        AudioSource e2Audio = AddAudio(e2Sound, 0.7f);
        e2Audio.maxDistance = 60f;
        Transform corridorBStart = Point(points, "Nurse_CorridorBStart", PrologueLayout.CorridorBStart, 90f);
        Transform exitFace = Point(points, "ExitDoorFace", PrologueLayout.ExitDoorFace, 90f);

        GameObject deathGo = new GameObject("PrologueDeath");
        deathGo.transform.SetParent(root, false);
        PrologueDeathSequence death = deathGo.AddComponent<PrologueDeathSequence>();
        SetRef(death, "audioSource", AddAudio(deathGo, 0f));

        GameObject directorGo = new GameObject("PrologueDirector");
        directorGo.transform.SetParent(root, false);
        PrologueDirector director = directorGo.AddComponent<PrologueDirector>();
        SerializedObject so = new SerializedObject(director);
        so.FindProperty("corridorAZone").objectReferenceValue = corridorA;
        so.FindProperty("corridorBMidZone").objectReferenceValue = corridorBMid;
        so.FindProperty("exitDoorZone").objectReferenceValue = exitZone;
        so.FindProperty("apparition").objectReferenceValue = apparition;
        so.FindProperty("journal").objectReferenceValue = journal;
        so.FindProperty("nurse").objectReferenceValue = nurse;
        so.FindProperty("steelDoor").objectReferenceValue = steelDoor;
        so.FindProperty("death").objectReferenceValue = death;
        so.FindProperty("nurseSpawn").objectReferenceValue = nurseSpawn;
        so.FindProperty("corridorAMid").objectReferenceValue = corridorAMid;
        so.FindProperty("wardEntrance").objectReferenceValue = wardEntrance;
        so.FindProperty("hidePassStart").objectReferenceValue = hidePassStart;
        so.FindProperty("hidePassFront").objectReferenceValue = hidePassFront;
        so.FindProperty("wardDoor").objectReferenceValue = PrologueWingBuilder.WardDoor;
        so.FindProperty("wardBackDoor").objectReferenceValue = wardBackDoor;
        so.FindProperty("e2AudioSource").objectReferenceValue = e2Audio;
        so.FindProperty("hideSpotLookAt").objectReferenceValue = hideSpotLookAt;
        so.FindProperty("nurseExit").objectReferenceValue = nurseExit;
        so.FindProperty("corridorBStart").objectReferenceValue = corridorBStart;
        so.FindProperty("exitDoorFace").objectReferenceValue = exitFace;
        so.ApplyModifiedPropertiesWithoutUndo();

        MakeTutorialCompact(tutorial);
        return directorGo;
    }

    // ───────────────────────────── 문·조명·NavMesh ─────────────────────────────

    private static CutsceneDoorSwing SetupSteelDoor(Transform door)
    {
        if (door == null)
        {
            Debug.LogWarning("[PrologueEventBuilder] 철문 문짝을 찾지 못함");
            return null;
        }

        AddDoorCollider(door);
        CutsceneDoorSwing swing = door.gameObject.AddComponent<CutsceneDoorSwing>();
        swing.CaptureClosedRotation();
        SerializedObject so = new SerializedObject(swing);
        so.FindProperty("openAngle").floatValue = 95f; // 복도 쪽(−z)으로 열림
        so.FindProperty("blend").floatValue = 1f;     // 처음엔 닫힘
        so.FindProperty("audioSource").objectReferenceValue = AddAudio(door.gameObject, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        swing.Blend = 1f;
        return swing;
    }

    private static void SetupExitDoor(Transform exit)
    {
        if (exit == null)
        {
            return;
        }

        SealedDoorInteract sealedDoor = exit.gameObject.AddComponent<SealedDoorInteract>();
        SerializedObject so = new SerializedObject(sealedDoor);
        SerializedProperty messages = so.FindProperty("messages");
        messages.arraySize = 2;
        messages.GetArrayElementAtIndex(0).stringValue = "…잠겨 있어.";
        messages.GetArrayElementAtIndex(1).stringValue = "열리지 않는다.";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 전기가 끊긴 병원에서 유일하게 켜진 초록 비상구 표시 — 긴 복도 끝으로 끌어당김
    private static void BuildEmergencyExitLight(Transform root)
    {
        Transform group = Child(root, "EmergencyExit");
        PrologueMapKit.Prop("HH_Exit_01", new Vector3(47.7f, 3.15f, 30f), 270f, group);

        Light light = new GameObject("ExitSign_Light").AddComponent<Light>();
        light.transform.SetParent(group, false);
        light.transform.position = new Vector3(47.0f, 2.9f, 30f);
        light.type = LightType.Point;
        light.color = new Color(0.25f, 1f, 0.45f);
        light.intensity = 1.6f;
        light.range = 7f;
        light.shadows = LightShadows.Soft;
    }

    /// <summary>
    /// 열린 프롤로그 씬의 지금 배치 그대로 NavMesh만 다시 구움 (씬을 다시 만들지 않음 — 손으로 옮긴 배치 유지).
    /// </summary>
    [MenuItem("Tools/Story/Rebake Prologue NavMesh")]
    public static void RebakeNavMeshInOpenScene()
    {
        GameObject mapGo = GameObject.Find("Prologue_Map");
        if (mapGo == null)
        {
            Debug.LogError("[PrologueEventBuilder] Prologue_Map이 없음 — Prologue 씬을 열고 실행");
            return;
        }

        CutsceneDoorSwing steelDoor = null;
        foreach (CutsceneDoorSwing swing in Object.FindObjectsByType<CutsceneDoorSwing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (swing.name.Contains("HH_Interior_Doors_02"))
            {
                steelDoor = swing;
            }
        }

        BakeNavMesh(mapGo.transform, steelDoor);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapGo.scene);
    }

    // 철문을 연 상태로 구워야 간호사가 대기실에서 복도로 나올 수 있음
    private static void BakeNavMesh(Transform map, CutsceneDoorSwing steelDoor)
    {
        float steelBlend = steelDoor != null ? steelDoor.Blend : 1f;
        if (steelDoor != null)
        {
            steelDoor.Blend = 0f;
        }

        // 문(DoorClick)은 닫힌 채로 구우면 길이 막힘 → 굽는 동안 문짝 콜라이더를 끔 (닫힌 문은 실행 중 NavMeshObstacle이 막음)
        var doorColliders = new System.Collections.Generic.List<Collider>();
        foreach (DoorClick door in map.GetComponentsInChildren<DoorClick>(true))
        {
            foreach (Collider col in door.GetComponentsInChildren<Collider>(true))
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    doorColliders.Add(col);
                }
            }
        }

        Physics.SyncTransforms();
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            surface = new GameObject("Prologue_NavMesh").AddComponent<NavMeshSurface>();
        }

        // 기본 복셀(0.167)이면 NavMesh 면이 바닥보다 ~7cm 떠서 괴물이 공중에 뜸 → 촘촘하게
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.05f;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~LayerMask.GetMask("Player", "Interactable", "Ignore Raycast", "UI", "PickupItem", "Door");
        surface.BuildNavMesh();

        AssetDatabase.DeleteAsset(NavMeshPath);
        if (surface.navMeshData != null)
        {
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        foreach (Collider col in doorColliders)
        {
            col.enabled = true;
        }

        if (steelDoor != null)
        {
            steelDoor.Blend = steelBlend;
        }
    }

    private static void ExcludeCamcorderOnlyLayer(Camera playerCamera)
    {
        int layer = LayerMask.NameToLayer(CamcorderOnlyApparition.LayerName);
        if (layer < 0)
        {
            Debug.LogWarning("[PrologueEventBuilder] 'CamcorderOnly' 레이어가 없음 — E1 형체가 맨눈에도 보임");
            return;
        }

        if (playerCamera != null)
        {
            playerCamera.cullingMask &= ~(1 << layer);
        }
    }

    // ───────────────────────────── 등장물 ─────────────────────────────

    private static CamcorderOnlyApparition BuildApparition(Transform root)
    {
        GameObject go = InstantiateNurse(root, "E1_Apparition", PrologueLayout.ApparitionSpot, 180f);
        StripAi(go, true);

        int layer = LayerMask.NameToLayer(CamcorderOnlyApparition.LayerName);
        if (layer >= 0)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        Transform focus = new GameObject("Focus").transform;
        focus.SetParent(go.transform, false);
        focus.localPosition = new Vector3(0f, 1.45f, 0f);

        CamcorderOnlyApparition apparition = go.AddComponent<CamcorderOnlyApparition>();
        SetRef(apparition, "focusPoint", focus);
        go.SetActive(false);
        return apparition;
    }

    private static PrologueNurse BuildNurse(Transform root)
    {
        GameObject go = InstantiateNurse(root, "Prologue_Nurse", PrologueLayout.NurseSpawn, 180f);
        StripAi(go, false);
        PrologueNurse nurse = go.AddComponent<PrologueNurse>();
        SetRef(nurse, "eyePoint", go.transform.Find("EyePoint"));
        SetRef(nurse, "audioSource", AddAudio(go, 1f));
        go.SetActive(false);
        return nurse;
    }

    private static GameObject InstantiateNurse(Transform root, string name, Vector3 position, float yaw)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NursePrefab), root);
        go.name = name;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    // 일반 적 AI는 꺼 둠(코드는 그대로). 형체(E1)는 이동·충돌도 필요 없어 전부 제거
    private static void StripAi(GameObject go, bool removeMovement)
    {
        foreach (EnemyBase enemy in go.GetComponents<EnemyBase>())
        {
            if (removeMovement)
            {
                Object.DestroyImmediate(enemy);
            }
            else
            {
                enemy.enabled = false;
            }
        }

        if (!removeMovement)
        {
            return;
        }

        Object.DestroyImmediate(go.GetComponent<NavMeshAgent>());
        Object.DestroyImmediate(go.GetComponent<Rigidbody>());
        foreach (Collider col in go.GetComponents<Collider>())
        {
            Object.DestroyImmediate(col);
        }
    }

    private static StoryDocumentPickup BuildJournal(Transform root)
    {
        Vector3 spot = PrologueLayout.JournalDeskSpot;
        float top = 0.8f;
        Physics.SyncTransforms();
        if (Physics.Raycast(new Vector3(spot.x, 3f, spot.z), Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            top = hit.point.y;
        }

        GameObject go = PrologueMapKit.Prop("HH_Hospital_Journal_01", new Vector3(spot.x, top + 0.005f, spot.z), 75f, root);
        go.name = "Journal_00";
        if (go.GetComponentInChildren<Collider>() == null)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                b.Encapsulate(r.bounds);
            }

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size = Vector3.Max(go.transform.InverseTransformVector(b.size), new Vector3(0.05f, 0.05f, 0.05f));
        }

        StoryDocumentPickup pickup = go.AddComponent<StoryDocumentPickup>();
        SerializedObject so = new SerializedObject(pickup);
        so.FindProperty("documentId").stringValue = "journal_00";
        so.FindProperty("title").stringValue = JournalTitle;
        so.FindProperty("body").stringValue = JournalBody;
        so.ApplyModifiedPropertiesWithoutUndo();
        return pickup;
    }

    // 일지 바로 옆 병실 열쇠 (본편 World_Key — E로 줍고, 잠긴 문 조준 + 좌클릭으로 염)
    private static void PlaceWardKey(Transform root)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefab);
        if (prefab == null)
        {
            Debug.LogWarning("[PrologueEventBuilder] 열쇠 프리팹 없음: " + KeyPrefab);
            return;
        }

        Vector3 spot = PrologueLayout.KeyDeskSpot;
        float top = 0.8f;
        Physics.SyncTransforms();
        if (Physics.Raycast(new Vector3(spot.x, 3f, spot.z), Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            top = hit.point.y;
        }

        GameObject key = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        key.name = "World_Key (병실)";
        key.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
        Collider col = key.GetComponentInChildren<Collider>();
        float half = col != null ? col.bounds.extents.y : 0.02f; // 월드 아이템은 루트가 모델 중앙
        key.transform.position = new Vector3(spot.x, top + half + 0.005f, spot.z);
    }

    private static void MakeTutorialCompact(GameObject tutorial)
    {
        TutorialManager manager = tutorial != null ? tutorial.GetComponentInChildren<TutorialManager>(true) : null;
        if (manager == null)
        {
            return;
        }

        TutorialCondition[] only =
        {
            TutorialCondition.Move, TutorialCondition.PickUpFlashlight, TutorialCondition.SelectFlashlight, TutorialCondition.FlashlightOn,
        };

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("onlySteps");
        list.arraySize = only.Length;
        for (int i = 0; i < only.Length; i++)
        {
            list.GetArrayElementAtIndex(i).enumValueIndex = (int)only[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private static void AddDoorCollider(Transform door)
    {
        if (door.GetComponent<Collider>() != null)
        {
            return;
        }

        Bounds bounds = new Bounds(door.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }

        BoxCollider box = door.gameObject.AddComponent<BoxCollider>();
        box.center = door.InverseTransformPoint(bounds.center);
        Vector3 size = door.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Max(0.08f, Mathf.Abs(size.x)), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    private static AudioSource AddAudio(GameObject go, float spatial)
    {
        AudioSource audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = spatial;
        return audio;
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

    private static Transform Point(Transform parent, string name, Vector3 position, float yaw)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return t;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static void SetRef(Object target, string property, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
