using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 허브 씬(팀원 이충섭 제작, 맵 수정까지 허락받음 2026-11-09)에 '귀문의 방' 구성. Tools > Story > Setup Hub Story.
/// 기존 큰 홀(70×70m·2배 크기 모듈)이 불필요하게 넓어서, 홀 바깥 빈 곳(RoomCenter)에 **12×12m·천장 4m 작은 방**을 일반 크기 모듈로 새로 지음:
/// · 문 세 개 = 왼쪽 벽(열린 문 → 12m 복도 끝 심연 — 기존 `Chapter1 In`(AbyssSinker)을 옮겨 폭만 맞춤) / 정면 벽(봉인) / 오른쪽 벽(봉인), 봉인 문엔 부적
/// · 가운데 기 제단, 뒤쪽에서 깨어남(플레이어 시작 위치를 옮김), 바로 앞 바닥에 기록자의 쪽지·캠코더
/// · 깨어남(HubAwakening)·진행(HubStoryDirector)
/// · HUD·일시정지 메뉴는 Chapter1 2에서 옮겨 옴(처음 한 번, 그 씬은 저장 안 함 — 원본 유지), 플레이어는 캠코더 없이 시작
/// 기존 홀은 건드리지 않고 그대로 둠(새 방 밖이라 안 보임). 다시 실행하면 Hub_Story 묶음만 지우고 새로 만듦.
/// </summary>
public static class HubSceneSetup
{
    private const string GameplaySourceScene = "Assets/Scenes/Chapter/Chapter1 2.unity";
    private const string AssetFolder = "Assets/Story/Hub";
    private const string LampPrefab = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/Hospital_Environment/HH_Ceiling_Lamp_01.prefab";

    // 방: x 94~106, z −6~6 (가운데 100,0) / 복도: 왼쪽 문(x94, z−2~2)에서 x 82~94, 끝 x82~86이 심연
    public static readonly Vector3 RoomCenter = new Vector3(100f, 0f, 0f);
    private const float Half = 6f;
    private const float CorridorLength = 12f;

    public const string NoteTitle = "누군가의 쪽지";
    public const string NoteBody =
        "이 쪽지를 읽고 있다면, 당신도 죽었다가 이곳에서 눈을 떴겠지.\n\n" +
        "여기는 귀문(鬼門)의 방. 이 섬에서 죽은 자는 이곳으로 돌아온다. 몇 번이든.\n\n" +
        "문은 셋. 지금 열리는 건 하나뿐이다.\n" +
        "그 너머 병원 어딘가에 봉인을 푸는 열쇠 세 개가 있다.\n\n" +
        "죽으면 손에 쥔 것은 모두 잃는다. 내가 남긴 기록만이 남는다.\n\n" +
        "— 기록자";

    [MenuItem("Tools/Story/Setup Hub Story")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject player = GameObject.FindWithTag("Player");
        if (scene.name != "Hub" || player == null)
        {
            Debug.LogError("[HubSceneSetup] Hub 씬을 열고 실행");
            return;
        }

        ImportGameplayUi(scene, player);
        UndoOldHallChanges();

        GameObject old = GameObject.Find("Hub_Story");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        Transform root = new GameObject("Hub_Story").transform;
        Material paper = EnsureMaterial("M_Talisman_Paper", new Color(0.93f, 0.79f, 0.36f), 0.1f);

        BuildRoom(Child(root, "Room"), paper);
        BuildCorridor(Child(root, "Corridor"));
        MoveAbyss();

        // 깨어나는 자리: 방 뒤쪽, 정면 문을 봄
        player.transform.SetPositionAndRotation(RoomCenter + new Vector3(0f, 0f, -3.4f), Quaternion.identity);
        var inv = new SerializedObject(player.GetComponent<InventoryManager>());
        inv.FindProperty("grantCamcorderOnStart").boolValue = false; // 빈손으로 깨어나 바닥의 캠코더를 주움
        inv.ApplyModifiedPropertiesWithoutUndo();

        StoryDocumentPickup note = BuildNote(root, player);
        PlaceCamcorderByNote(player);
        BuildTraitAltar(root, paper);

        Transform zones = Child(root, "Zones");
        StoryZone openDoorZone = Zone(zones, "Zone_OpenDoor",
            new Bounds(RoomCenter + new Vector3(-Half - 3f, 1f, 0f), new Vector3(4f, 2f, 4f)));

        GameObject canvas = FindRoot(scene, "Canvas");
        GameObject awakeGo = new GameObject("HubAwakening");
        awakeGo.transform.SetParent(root, false);
        HubAwakening awakening = awakeGo.AddComponent<HubAwakening>();
        AudioSource audio = awakeGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        var aso = new SerializedObject(awakening);
        SetArray(aso.FindProperty("hideDuringWake"), canvas != null ? new Object[] { canvas } : new Object[0]);
        aso.FindProperty("audioSource").objectReferenceValue = audio;
        aso.ApplyModifiedPropertiesWithoutUndo();

        GameObject directorGo = new GameObject("HubStoryDirector");
        directorGo.transform.SetParent(root, false);
        HubStoryDirector director = directorGo.AddComponent<HubStoryDirector>();
        var dso = new SerializedObject(director);
        dso.FindProperty("awakening").objectReferenceValue = awakening;
        dso.FindProperty("doorsZone").objectReferenceValue = null; // 작은 방이라 깨어나자마자 문 세 개가 보임 → 깨어난 직후 독백
        dso.FindProperty("openDoorZone").objectReferenceValue = openDoorZone;
        dso.FindProperty("note").objectReferenceValue = note;
        dso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[HubSceneSetup] 완료 — 작은 방(문 3개)·복도·심연, 쪽지·캠코더·기 제단, 깨어남·진행");
    }

    // 예전 버전이 기존 홀에 했던 변경 되돌림: 껐던 벽 다시 켬, 격리 테이프 문에 붙인 봉인 상호작용 제거
    private static void UndoOldHallChanges()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "HH_Hospital_PartitionWall_4x4m (13)" && t.parent == null)
            {
                t.gameObject.SetActive(true);
            }
        }

        GameObject soft = GameObject.Find("HH_Softwall_Isolation_Barrier_No_Passage");
        if (soft != null)
        {
            foreach (SealedDoorInteract s in soft.GetComponents<SealedDoorInteract>())
            {
                Object.DestroyImmediate(s);
            }
        }
    }

    // ── 방 ─────────────────────────────────────────────

    // 벽은 시계 방향으로 이어 붙임(안쪽이 진행 방향 오른쪽 — PrologueMapKit.Along 규칙)
    private static void BuildRoom(Transform parent, Material paper)
    {
        float x0 = RoomCenter.x - Half, x1 = RoomCenter.x + Half, z0 = RoomCenter.z - Half, z1 = RoomCenter.z + Half;
        GameObject wall = PrologueMapKit.Wall4, doorWall = PrologueMapKit.DoorWall;

        // 왼쪽(x0): 가운데 칸 = 열린 문
        PrologueMapKit.Along(wall, new Vector3(x0, 0f, z0), Vector3.forward, parent);
        GameObject open = PrologueMapKit.Along(doorWall, new Vector3(x0, 0f, z0 + 4f), Vector3.forward, parent);
        open.name = "Door_Hospital_Open";
        StripScripts(open);
        foreach (Transform t in open.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("HH_Interior_Doors_02"))
            {
                t.gameObject.SetActive(false); // 문짝 없음 = 열려 있음
            }
        }

        PrologueMapKit.Along(wall, new Vector3(x0, 0f, z0 + 8f), Vector3.forward, parent);

        // 정면(z1): 가운데 칸 = 봉인
        PrologueMapKit.Along(wall, new Vector3(x0, 0f, z1), Vector3.right, parent);
        SealedDoor(PrologueMapKit.Along(doorWall, new Vector3(x0 + 4f, 0f, z1), Vector3.right, parent), "Door_Front_Sealed", Vector3.back, paper);
        PrologueMapKit.Along(wall, new Vector3(x0 + 8f, 0f, z1), Vector3.right, parent);

        // 오른쪽(x1): 가운데 칸 = 봉인
        PrologueMapKit.Along(wall, new Vector3(x1, 0f, z1), Vector3.back, parent);
        SealedDoor(PrologueMapKit.Along(doorWall, new Vector3(x1, 0f, z1 - 4f), Vector3.back, parent), "Door_Right_Sealed", Vector3.left, paper);
        PrologueMapKit.Along(wall, new Vector3(x1, 0f, z1 - 8f), Vector3.back, parent);

        // 뒤(z0): 막힌 벽
        for (int i = 0; i < 3; i++)
        {
            PrologueMapKit.Along(wall, new Vector3(x1 - i * 4f, 0f, z0), Vector3.left, parent);
        }

        GameObject floor = PrologueMapKit.Load(PrologueMapKit.Interior + "HH_Flooring_Concrete_4x4m_04.prefab");
        GameObject ceiling = PrologueMapKit.Load(PrologueMapKit.Interior + "HH_Ceiling_Armstrong_4х4m_02.prefab");
        for (int ix = 0; ix < 3; ix++)
        {
            for (int iz = 0; iz < 3; iz++)
            {
                PrologueMapKit.FloorTile(floor, x0 + ix * 4f, z0 + iz * 4f, parent);
                PrologueMapKit.CeilingTile(ceiling, x0 + ix * 4f, z0 + iz * 4f, parent);
            }
        }

        // 조명: 천장 형광등 하나(희미) + 열린 문 옆 붉은 등
        Lamp(parent, RoomCenter + new Vector3(0f, 3.99f, -1f), new Color(0.85f, 0.92f, 1f), 5f, 9f);
        RedLight(parent, new Vector3(x0 + 0.35f, 2.9f, RoomCenter.z + 2.6f));
    }

    private static void SealedDoor(GameObject door, string name, Vector3 inward, Material paper)
    {
        door.name = name;
        StripScripts(door);
        Seal(door);
        Bounds leaf = Bounds(door);
        foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("HH_Interior_Doors_02"))
            {
                leaf = r.bounds;
            }
        }

        // 문짝 앞면(방 안쪽)에 부적
        Vector3 face = leaf.center + Vector3.Scale(inward, leaf.extents) + inward * 0.012f;
        face.y = 0f;
        Talismans(door.transform, face, Quaternion.LookRotation(-inward), 0.5f, paper);
    }

    private static void StripScripts(GameObject go)
    {
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(mb); // DoorClick 등 — 이 문들은 여닫지 않음
        }
    }

    // ── 복도·심연 ─────────────────────────────────────────

    private static void BuildCorridor(Transform parent)
    {
        float xDoor = RoomCenter.x - Half, xEnd = xDoor - CorridorLength, z0 = RoomCenter.z - 2f, z1 = RoomCenter.z + 2f;
        GameObject wall = PrologueMapKit.Wall4;
        for (float x = xEnd; x < xDoor - 0.1f; x += 4f)
        {
            PrologueMapKit.Along(wall, new Vector3(x, 0f, z1), Vector3.right, parent);           // 북쪽
        }

        for (float x = xDoor; x > xEnd + 0.1f; x -= 4f)
        {
            PrologueMapKit.Along(wall, new Vector3(x, 0f, z0), Vector3.left, parent);            // 남쪽
        }

        PrologueMapKit.Along(wall, new Vector3(xEnd, 0f, z0), Vector3.forward, parent);          // 끝

        GameObject floor = PrologueMapKit.Load(PrologueMapKit.Interior + "HH_Flooring_Concrete_4x4m_04.prefab");
        GameObject ceiling = PrologueMapKit.Load(PrologueMapKit.Interior + "HH_Ceiling_Armstrong_4х4m_02.prefab");
        for (float x = xEnd; x < xDoor - 0.1f; x += 4f)
        {
            if (x > xEnd + 0.1f)
            {
                PrologueMapKit.FloorTile(floor, x, z0, parent); // 맨 끝 칸은 바닥 없음 = 심연
            }

            PrologueMapKit.CeilingTile(ceiling, x, z0, parent);
        }

        // 심연 칸 위에 보이지 않는 바닥 — 트리거에 닿기 전에 물리로 떨어지지 않게(떨어지는 건 AbyssSinker 연출이 맡음)
        GameObject lid = new GameObject("AbyssLid");
        lid.transform.SetParent(parent, false);
        lid.transform.position = new Vector3(xEnd + 2f, -0.05f, RoomCenter.z);
        lid.AddComponent<BoxCollider>().size = new Vector3(4f, 0.1f, 4f);

        RedLight(parent, new Vector3(xDoor - 4f, 2.9f, z1 - 0.3f));
        RedLight(parent, new Vector3(xEnd + 5f, 2.9f, z0 + 0.3f));
    }

    // 기존 심연(검은 기둥 메쉬 + AbyssSinker 트리거)을 복도 끝 칸으로 옮기고 폭을 4m로 (높이·윗면 높이는 그대로)
    private static void MoveAbyss()
    {
        GameObject abyss = GameObject.Find("Chapter1 In");
        if (abyss == null)
        {
            Debug.LogWarning("[HubSceneSetup] Chapter1 In(심연)을 못 찾음");
            return;
        }

        Transform t = abyss.transform;
        float xEnd = RoomCenter.x - Half - CorridorLength;
        t.position = new Vector3(xEnd + 2f, t.position.y, RoomCenter.z);
        t.localScale = new Vector3(4f, t.localScale.y, 4f);
    }

    // ── 소품·조명 ─────────────────────────────────────────

    private static void Lamp(Transform parent, Vector3 center, Color color, float intensity, float range)
    {
        GameObject go = new GameObject("CeilingLamp");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab), go.transform);
        mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 180f); // 천장 모듈처럼 뒤집어야 아래를 봄
        mesh.transform.localPosition = new Vector3(1f, 0f, 0.5f);       // 기준점이 모서리
        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        light.type = LightType.Spot;
        light.spotAngle = 130f;
        light.innerSpotAngle = 70f;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void RedLight(Transform parent, Vector3 position)
    {
        Light light = new GameObject("RedLight").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = new Color(1f, 0.16f, 0.12f);
        light.intensity = 1.6f;
        light.range = 6f;
    }

    private static void Seal(GameObject door)
    {
        SealedDoorInteract seal = door.AddComponent<SealedDoorInteract>();
        var so = new SerializedObject(seal);
        SerializedProperty first = so.FindProperty("firstTryLines");
        first.arraySize = 1;
        SerializedProperty line = first.GetArrayElementAtIndex(0);
        line.FindPropertyRelative("speaker").stringValue = "나";
        line.FindPropertyRelative("text").stringValue = "부적이 덕지덕지 붙어 있어… 꿈쩍도 안 해.";
        line.FindPropertyRelative("holdSeconds").floatValue = 2.6f;
        SerializedProperty messages = so.FindProperty("messages");
        messages.arraySize = 1;
        messages.GetArrayElementAtIndex(0).stringValue = "…열리지 않는다.";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 문짝 앞면(face, 바닥 높이)에 부적 여러 장 — 노란 종이에 붓글씨 주문 急急如律令(실행 중 제목 폰트). scale 1 = 2배 크기 문 기준
    private static void Talismans(Transform parent, Vector3 face, Quaternion facing, float scale, Material paper)
    {
        Vector3[] spots = { new Vector3(-0.15f, 2.6f, 0f), new Vector3(0.55f, 3.3f, 0.004f), new Vector3(-0.9f, 3.5f, 0.008f), new Vector3(0.9f, 1.9f, 0.012f), new Vector3(-0.6f, 1.6f, 0.016f) };
        float[] tilt = { -4f, 11f, -13f, 7f, -9f };
        for (int i = 0; i < spots.Length; i++)
        {
            GameObject t = new GameObject("Talisman_" + (i + 1));
            t.transform.SetParent(parent, true);
            Vector3 local = new Vector3(spots[i].x * scale, spots[i].y * scale, -spots[i].z);
            t.transform.position = face + facing * local;
            t.transform.rotation = facing * Quaternion.Euler(0f, 0f, tilt[i]);
            // 부모(문 모듈) 크기와 무관하게 월드 크기 = scale
            Vector3 parentScale = parent.lossyScale;
            t.transform.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, scale / parentScale.z);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Paper";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(t.transform, false);
            quad.transform.localScale = new Vector3(0.42f, 1.15f, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = paper;

            GameObject textGo = new GameObject("Glyphs");
            textGo.transform.SetParent(t.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            var tmp = textGo.AddComponent<TMPro.TextMeshPro>();
            tmp.text = "急\n急\n如\n律\n令"; // 부적 주문 — 제목 붓글씨 폰트에 門·勅이 없어서
            tmp.fontSize = 1.8f;
            tmp.lineSpacing = -25f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.7f, 0.06f, 0.05f);
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 1.1f);
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            textGo.AddComponent<TitleFontText>();
        }
    }

    // ── 쪽지·캠코더·제단 ─────────────────────────────────────

    private static StoryDocumentPickup BuildNote(Transform root, GameObject player)
    {
        Vector3 p = player.transform.position + player.transform.forward * 1.5f + player.transform.right * -0.7f;
        GameObject note = GameObject.CreatePrimitive(PrimitiveType.Quad);
        note.name = "Hub_Note";
        note.transform.SetParent(root, false);
        note.transform.position = new Vector3(p.x, 0.012f, p.z);
        note.transform.rotation = Quaternion.Euler(90f, player.transform.eulerAngles.y + 17f, 0f);
        note.transform.localScale = new Vector3(0.32f, 0.44f, 1f);
        note.GetComponent<MeshRenderer>().sharedMaterial = EnsureMaterial("M_Hub_Note", new Color(0.86f, 0.84f, 0.78f), 0.05f);
        Object.DestroyImmediate(note.GetComponent<Collider>());
        BoxCollider box = note.AddComponent<BoxCollider>();
        box.size = new Vector3(1.4f, 1.4f, 0.3f); // 바닥에 납작하게 놓여 조준하기 어려워서 판정은 넉넉히
        StoryDocumentPickup pickup = note.AddComponent<StoryDocumentPickup>();
        var so = new SerializedObject(pickup);
        so.FindProperty("documentId").stringValue = "hub_note";
        so.FindProperty("prompt").stringValue = "[E] 쪽지 읽기";
        so.FindProperty("title").stringValue = NoteTitle;
        so.FindProperty("body").stringValue = NoteBody;
        so.FindProperty("removeAfterReading").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return pickup;
    }

    // 원래 뒤쪽 창고 선반에 있던 World_Camcorder를 깨어난 자리 앞, 쪽지 옆 바닥에 옆으로 쓰러진 채 둠 (사용자 결정 2026-11-09)
    private static void PlaceCamcorderByNote(GameObject player)
    {
        ItemObject camcorder = null;
        foreach (ItemObject item in Object.FindObjectsByType<ItemObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.name.StartsWith("World_Camcorder"))
            {
                camcorder = item;
            }
        }

        if (camcorder == null)
        {
            Debug.LogWarning("[HubSceneSetup] World_Camcorder를 못 찾음");
            return;
        }

        Transform t = camcorder.transform;
        Vector3 p = player.transform.position + player.transform.forward * 1.25f + player.transform.right * 0.55f;
        t.SetPositionAndRotation(new Vector3(p.x, 0f, p.z), Quaternion.Euler(0f, player.transform.eulerAngles.y - 35f, 82f));
        Bounds b = Bounds(t.gameObject);
        t.position += Vector3.up * (0.005f - b.min.y); // 바닥에 닿게
    }

    // 기 제단 — 방 가운데 오른쪽 앞, 앞면이 깨어난 자리 쪽. 탁자 위 노란 종이 깃발(기)·양초(불꽃 셰이더가 URP에서 깨져 몸통만 + 촛불 조명), E로 기 고르기(TraitAltar)
    private static void BuildTraitAltar(Transform root, Material paper)
    {
        const string props = "Assets/ExternalAssets/Abandoned_Psychiatric_Hospitals/Prefabs/Props/";
        Transform group = Child(root, "TraitAltar");
        // 정가운데면 깨어난 자리에서 정면 문을 가려서 오른쪽 앞으로 비켜 둠, 앞면(-z)이 깨어난 자리를 향함
        group.position = RoomCenter + new Vector3(2.8f, 0f, 0.8f);
        Vector3 wake = RoomCenter + new Vector3(0f, 0f, -3.4f);
        Vector3 away = group.position - wake;
        away.y = 0f;
        group.rotation = Quaternion.LookRotation(away);

        GameObject table = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(props + "Directors_Office/Bedside_Table.prefab"), group);
        table.transform.localPosition = Vector3.zero;
        table.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Bounds tb = Bounds(table);
        float top = tb.max.y;

        // 깃발(기) — 가는 막대에 세로로 긴 노란 종이, 탁자 뒤쪽에 나란히
        for (int i = 0; i < 5; i++)
        {
            float x = -0.5f + i * 0.25f;
            GameObject flag = new GameObject("Flag_" + (i + 1));
            flag.transform.SetParent(group, false);
            flag.transform.localPosition = new Vector3(x, top, 0.18f);
            flag.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 2) * 4f);
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.name = "Pole";
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localScale = new Vector3(0.012f, 0.32f, 0.012f);
            pole.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = EnsureMaterial("M_Altar_Pole", new Color(0.18f, 0.12f, 0.08f), 0.2f);
            GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(cloth.GetComponent<Collider>());
            cloth.name = "Paper";
            cloth.transform.SetParent(flag.transform, false);
            cloth.transform.localPosition = new Vector3(0.06f, 0.44f, -0.002f);
            cloth.transform.localScale = new Vector3(0.11f, 0.34f, 1f);
            cloth.GetComponent<MeshRenderer>().sharedMaterial = paper;
        }

        // 양초 + 촛불 조명
        string[] candles = { "Candle_01", "Candle_03", "Candle_02" };
        float[] cx = { -0.55f, 0.55f, 0.35f };
        for (int i = 0; i < candles.Length; i++)
        {
            GameObject candle = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(props + candles[i] + ".prefab"), group);
            candle.transform.localPosition = new Vector3(cx[i], top, -0.12f);
            foreach (Renderer r in candle.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Candle_Flame"))
                {
                    r.enabled = false;
                }
            }
        }

        Light glow = new GameObject("CandleLight").AddComponent<Light>();
        glow.transform.SetParent(group, false);
        glow.transform.localPosition = new Vector3(0f, top + 0.45f, -0.25f);
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.62f, 0.3f);
        glow.intensity = 1.6f;
        glow.range = 4f;
        glow.shadows = LightShadows.Soft;

        BoxCollider box = group.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, (top + 0.8f) * 0.5f, 0f);
        box.size = new Vector3(1.6f, top + 0.8f, 1.0f);
        group.gameObject.AddComponent<TraitAltar>();
    }

    // ── HUD 가져오기 ─────────────────────────────────────

    // Chapter1 2의 Canvas(HUD·인벤토리)·PauseMenu를 옮겨 오고, 그 씬 플레이어를 가리키던 참조는 허브 플레이어의 같은 컴포넌트로 바꿈
    private static void ImportGameplayUi(Scene hub, GameObject hubPlayer)
    {
        GameObject existing = FindRoot(hub, "Canvas");
        if (existing != null && existing.transform.Find("QuickInventoryHud") != null)
        {
            return;
        }

        Scene source = EditorSceneManager.OpenScene(GameplaySourceScene, OpenSceneMode.Additive);
        try
        {
            GameObject sourcePlayer = FindRoot(source, "Player");
            var moved = new List<GameObject>();
            foreach (string name in new[] { "Canvas", "PauseMenu" })
            {
                GameObject go = FindRoot(source, name);
                if (go == null || FindRoot(hub, name) != null)
                {
                    continue;
                }

                SceneManager.MoveGameObjectToScene(go, hub);
                moved.Add(go);
            }

            foreach (GameObject go in moved)
            {
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(c);
                    SerializedProperty it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null)
                        {
                            continue;
                        }

                        Object remapped = Remap(it.objectReferenceValue, source, sourcePlayer, hubPlayer);
                        if (remapped != it.objectReferenceValue)
                        {
                            it.objectReferenceValue = remapped;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            Debug.Log("[HubSceneSetup] Chapter1 2에서 HUD·일시정지 메뉴를 옮겨 옴: " + moved.Count + "개");
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true); // 저장 안 함 → Chapter1 2 원본 유지
        }
    }

    private static Object Remap(Object target, Scene source, GameObject sourcePlayer, GameObject hubPlayer)
    {
        GameObject go = target as GameObject ?? (target as Component)?.gameObject;
        if (go == null || go.scene != source || sourcePlayer == null || !go.transform.IsChildOf(sourcePlayer.transform))
        {
            return target;
        }

        string path = AnimationUtility.CalculateTransformPath(go.transform, sourcePlayer.transform);
        Transform match = string.IsNullOrEmpty(path) ? hubPlayer.transform : hubPlayer.transform.Find(path);
        if (match == null)
        {
            return null;
        }

        return target is GameObject ? match.gameObject : (Object)match.GetComponent(target.GetType());
    }

    // ── 공용 ─────────────────────────────────────────────

    private static Material EnsureMaterial(string name, Color color, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Story", "Hub");
        }

        string path = AssetFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Cull", 0f); // 양면
        EditorUtility.SetDirty(m);
        return m;
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

    private static Bounds Bounds(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return b;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name)
            {
                return go;
            }
        }

        return null;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
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
