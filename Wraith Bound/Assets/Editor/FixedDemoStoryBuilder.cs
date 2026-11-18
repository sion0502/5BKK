using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 씬 Chapter1_FixedDemo(팀원 이충섭 제작)에 이야기 요소 배치. Tools > Story > Fixed Demo > Build Story Objects.
/// 씬 루트 `FixedDemo_Story` 하나만 지우고 다시 만듦 — 팀원 오브젝트는 읽기만 함(수정 없음).
///  · 부적 7장(E로 태움) = 팀원이 타일 프리팹에 찍어 둔 배치 표시(노란 공 `Key`, 재질 YellowColor) 자리마다, 그 아래 표면(책상·침대·바닥)에 눕혀 놓음.
///    아무 3장이면 출구가 열림(DemoSealProgress). 노란 공은 실행 중에만 숨김(DemoHideOnPlay — 프리팹은 그대로).
///  · 연출 ③: 수술실 타일 1층 남동쪽 방 침대 부적 — 태우면 무영등 소등·붉은 경보등·탈춤귀신(비활성으로 숨겨 둠)이 북쪽 문을 열고 들이닥침
///  · 출구 대문: 부적 3장으로 봉인 — 맵의 부적을 태울 때마다 한 장씩 같이 타고 붉은 봉인 빛이 약해짐, 셋이면 문 열림 (DemoSealGate)
/// NavMesh는 씬의 LobbyEnemySpawner가 실행 중에 구움(팀원 방식 그대로).
/// </summary>
public static class FixedDemoStoryBuilder
{
    private const string ScenePath = "Assets/Scenes/Demo/Chapter1_FixedDemo.unity";
    private const string RootName = "FixedDemo_Story";
    private const string SurgicalLampModel = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/Hospital_Environment/HH_Surgical_Lamp.prefab";
    private const string MaskGhostPrefab = "Assets/Prefab/Enemy/Monster/MaskGhost.prefab";
    private const string MarkerName = "Key";
    private const string MarkerMaterial = "YellowColor";

    [MenuItem("Tools/Story/Fixed Demo/Build Story Objects")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[FixedDemoStory] " + ScenePath + " 를 열고 실행");
            return;
        }

        Physics.SyncTransforms();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName)
            {
                Object.DestroyImmediate(go);
            }
        }

        GameObject rootGo = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootGo, scene);
        Transform root = rootGo.transform;
        var log = new System.Text.StringBuilder();

        // 배치 표시(노란 공) 찾기 — 순서는 남→북, 서→동 (번호 = 봉인 번호)
        var markers = new System.Collections.Generic.List<Transform>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name == MarkerName && r.sharedMaterial != null && r.sharedMaterial.name == MarkerMaterial)
            {
                markers.Add(r.transform);
            }
        }

        markers.Sort((a, b) => Mathf.Abs(a.position.z - b.position.z) > 1f ? a.position.z.CompareTo(b.position.z) : a.position.x.CompareTo(b.position.x));
        log.AppendLine("배치 표시 " + markers.Count + "개");

        Transform talismans = new GameObject("Talismans").transform;
        talismans.SetParent(root, false);
        DemoTalisman operatingRoomTalisman = null;
        for (int i = 0; i < markers.Count; i++)
        {
            DemoTalisman t = PlaceTalismanAtMarker(talismans, i, markers[i], log);
            if (IsUnder(markers[i], "Tile_3Way (OperatingRoom)"))
            {
                operatingRoomTalisman = t;
            }
        }

        // 로비 대기 의자 위 부적 (2026-11-19 사용자 결정 — ⑤ Xenosaurid를 로비로 옮겨 루트 단축). 번호 = 배치 표시 다음(8번째)
        PlaceTalismanAt(talismans, markers.Count, LobbyTalismanAbove, "로비 대기 의자", log);

        DemoHideOnPlay hider = talismans.gameObject.AddComponent<DemoHideOnPlay>();
        var hso = new SerializedObject(hider);
        DemoSetPieceBuilder.SetArray(hso.FindProperty("targets"), markers.ConvertAll(m => (Object)m.gameObject).ToArray());
        hso.ApplyModifiedPropertiesWithoutUndo();

        if (operatingRoomTalisman != null)
        {
            OperatingRoom(root, operatingRoomTalisman, log);
        }
        else
        {
            log.AppendLine("수술실 타일 배치 표시 없음 — 연출 ③ 생략");
        }

        root.gameObject.AddComponent<DemoCreatureTuning>(); // 괴물 추격 속도 = 플레이어 달리기 (탈춤귀신 ×1.1)
        FixedDemoSetPieces.Build(root, log); // 시연 동선: 잠긴 문 풀기·열쇠·④ 얼굴 없는 귀신·⑤ Xenosaurid
        FixedDemoHideSpots.Build(root, log); // ③ 수술실 방 숨는 곳(트롤리) — 사용자 요청 2개 이상
        FixedDemoRoamers.Build(root, log); // 맵을 돌아다니는 괴물 4종(무작위 타일)
        SealGate(root, log);
        FixedDemoNotes.Build(root, log); // 스토리 쪽지 20장 후보 자리
        FixedDemoGuide.Build(root, log); // 길잡이 혼불 루트(연출 순서) — 열쇠·부적·④⑤ 구역·출구 뒤에 만들어야 함

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FixedDemoStory] 배치 완료\n" + log);
    }

    private static bool IsUnder(Transform t, string ancestorName)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (p.name == ancestorName)
            {
                return true;
            }
        }

        return false;
    }

    // 연출 ③ — 수술실 타일 1층 남동쪽 방(x 309~333, z 51~81, 북쪽 문 (321.3, 0, 81.7)) 침대 위 부적.
    // 탈춤귀신은 북쪽 문 너머 홀 (325, 0, 88)(침대에서 안 보이고 경로 약 27m ≈ 7초)에 숨겨 두고, 부적을 태우면 그 문을 열고 들이닥침
    // (닫힌 문 앞에선 NavMesh 경로가 끊겨 추격을 포기하므로 문을 열어 둠 — 2층 수술대 버전에서 겪은 문제)
    private static void OperatingRoom(Transform parent, DemoTalisman talisman, System.Text.StringBuilder log)
    {
        Transform root = new GameObject("SetPiece3_OperatingRoom").transform;
        root.SetParent(parent, false);
        Vector3 top = talisman.transform.position;
        int sealIndex = new SerializedObject(talisman).FindProperty("index").intValue;
        log.AppendLine("연출 ③ = 부적 " + (sealIndex + 1) + " " + top.ToString("F1"));

        float ceiling = Physics.Raycast(top + Vector3.up * 0.5f, Vector3.up, out RaycastHit ch, 20f, ~0, QueryTriggerInteraction.Ignore) ? ch.point.y : top.y + 5f;
        GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SurgicalLampModel), root);
        lamp.name = "SurgicalLamp";
        lamp.transform.position = new Vector3(top.x - 0.6f, ceiling, top.z);
        lamp.transform.localScale = Vector3.one * 1.3f;
        Light spot = new GameObject("SurgicalLight").AddComponent<Light>();
        spot.transform.SetParent(root, false);
        spot.transform.position = new Vector3(top.x, ceiling - 1.6f, top.z);
        spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        spot.type = LightType.Spot;
        spot.spotAngle = 95f;
        spot.innerSpotAngle = 50f;
        spot.range = 8f;
        spot.intensity = 26f;
        spot.color = new Color(0.92f, 0.97f, 1f);
        spot.shadows = LightShadows.Hard;
        log.AppendLine("무영등 천장 y " + ceiling.ToString("F2"));

        Vector3[] redPos = { new Vector3(313f, 4.5f, 57f), new Vector3(328f, 4.5f, 58f), new Vector3(320f, 4.5f, 74f) };
        var reds = new Light[redPos.Length];
        for (int i = 0; i < redPos.Length; i++)
        {
            Light l = new GameObject("RedLight_" + (i + 1)).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = redPos[i];
            l.type = LightType.Point;
            l.color = new Color(1f, 0.07f, 0.04f);
            l.range = 15f;
            l.intensity = 0f;
            l.shadows = i == 0 ? LightShadows.Hard : LightShadows.None;
            l.enabled = false;
            reds[i] = l;
        }

        Vector3 spawn = new Vector3(325f, 0f, 88f);
        if (Physics.Raycast(spawn + Vector3.up * 2f, Vector3.down, out RaycastHit fh, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawn = fh.point;
        }

        Transform ghostSpawn = DemoSetPieceBuilder.Point(root, "GhostSpawn", spawn);
        ghostSpawn.rotation = Quaternion.LookRotation(Vector3.back);

        DoorClick roomDoor = null;
        foreach (DoorClick d in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            if (Vector3.Distance(d.transform.position, new Vector3(321.3f, 0f, 81.7f)) < 1.5f)
            {
                roomDoor = d;
            }
        }

        log.AppendLine("방 북쪽 문 " + (roomDoor != null ? "찾음" : "못 찾음!"));

        GameObject ghostGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MaskGhostPrefab), root);
        ghostGo.name = "MaskGhost";
        ghostGo.transform.SetPositionAndRotation(spawn, ghostSpawn.rotation);
        ghostGo.transform.localScale = FixedDemoSetPieces.CreatureScale(MaskGhostPrefab); // 프리팹 크기 × 플레이어 크기 (EnemyScene 비율, 2026-11-19)
        ghostGo.SetActive(false); // 부적을 태우기 전엔 숨김 → DemoOperatingRoomSequence가 켬
        EnemyBase ghost = ghostGo.GetComponent<EnemyBase>();

        // 놓친 뒤 서성일 순찰 지점: 북쪽 홀 · 방 안 두 곳 (지점 없으면 제자리에 멈춰 섬)
        Transform zone = new GameObject("PatrolZone_Mask").transform;
        zone.SetParent(root, false);
        PatrolPointZone ppz = zone.gameObject.AddComponent<PatrolPointZone>();
        Vector3[] patrol = { new Vector3(325f, 0f, 96f), new Vector3(318f, 0f, 66f), new Vector3(312f, 0f, 76f) };
        var points = new Object[patrol.Length];
        for (int i = 0; i < patrol.Length; i++)
        {
            Transform p = DemoSetPieceBuilder.Point(zone, "PatrolPoint (" + i + ")", patrol[i]);
            points[i] = p.gameObject.AddComponent<PatrolPoint>();
        }

        var zso = new SerializedObject(ppz);
        DemoSetPieceBuilder.SetArray(zso.FindProperty("points"), points);
        zso.ApplyModifiedPropertiesWithoutUndo();
        if (ghost != null)
        {
            var eso = new SerializedObject(ghost);
            SerializedProperty zp = eso.FindProperty("patrolPointZone");
            if (zp != null)
            {
                zp.objectReferenceValue = ppz;
                eso.ApplyModifiedPropertiesWithoutUndo();
            }
        }


        DemoOperatingRoomSequence seq = root.gameObject.AddComponent<DemoOperatingRoomSequence>();
        AudioSource audio = root.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = true;
        audio.spatialBlend = 0.6f;
        var so = new SerializedObject(seq);
        so.FindProperty("sealIndex").intValue = sealIndex;
        DemoSetPieceBuilder.SetArray(so.FindProperty("openOnRelease"), new Object[] { roomDoor });
        so.FindProperty("surgicalLight").objectReferenceValue = spot;
        DemoSetPieceBuilder.SetArray(so.FindProperty("redLights"), reds);
        so.FindProperty("ghost").objectReferenceValue = ghost;
        so.FindProperty("ghostSpawn").objectReferenceValue = ghostSpawn;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("탈춤귀신 등장 " + spawn.ToString("F1") + (ghost != null ? "" : " (EnemyBase 없음!)"));
    }

    // 맵에 놓는 부적 크기 (종이 약 0.34×0.92m — 플레이어 1.3배 맵에서 눈에 띄게)
    private const float TalismanScale = 0.8f;

    // 배치 표시(노란 공) 바로 아래 표면(책상·침대·진열장·바닥)에 눕혀 놓음. 방향은 번호마다 조금씩 비뚤게
    internal static readonly Vector3 LobbyTalismanAbove = new Vector3(183f, 3f, 81f); // 로비 대기 의자(HH_Public_Seating_01) 위 — 안내 데스크는 막힌 카운터 안이라 걸어서 못 닿았음

    private static DemoTalisman PlaceTalismanAt(Transform parent, int index, Vector3 above, string label, System.Text.StringBuilder log)
    {
        RaycastHit[] hits = Physics.RaycastAll(above, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Vector3 pos = hits.Length > 0 ? hits[0].point + Vector3.up * 0.008f : above;
        float yaw = (index * 67f + 20f) % 360f;
        DemoTalisman t = TalismanBuilder.CreateInteractable(parent, index, pos, Quaternion.Euler(90f, yaw, 0f), TalismanScale);
        log.AppendLine("부적" + (index + 1) + " " + label + " → " + (hits.Length > 0 ? hits[0].collider.name : "표면 없음") + " " + pos.ToString("F2"));
        return t;
    }

    private static DemoTalisman PlaceTalismanAtMarker(Transform parent, int index, Transform marker, System.Text.StringBuilder log)
    {
        Vector3 origin = marker.position;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Vector3 pos = origin;
        string surface = "없음(표시 위치 그대로)";
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform == marker || h.collider.name == MarkerName)
            {
                continue;
            }

            pos = h.point + Vector3.up * 0.008f;
            surface = h.collider.name;
            break;
        }

        float yaw = (index * 67f + 20f) % 360f;
        DemoTalisman t = TalismanBuilder.CreateInteractable(parent, index, pos, Quaternion.Euler(90f, yaw, 0f), TalismanScale);
        log.AppendLine("부적" + (index + 1) + " " + UnityEditor.AnimationUtility.CalculateTransformPath(marker, null) + " → " + surface + " " + pos.ToString("F2"));
        return t;
    }

    // 출구 타일: 팀원 SealTest(문 2짝 + 봉인 구슬 5개·에너지 줄기) — 구슬·줄기는 숨기고, 문짝 앞면(남쪽, z 320.59)에 봉인 부적 3장
    private static void SealGate(Transform parent, System.Text.StringBuilder log)
    {
        SealTest test = Object.FindFirstObjectByType<SealTest>(FindObjectsInactive.Include);
        if (test == null || test.leftDoor == null || test.rightDoor == null)
        {
            log.AppendLine("SealTest·문 없음 — 출구 봉인 생략");
            return;
        }

        GameObject go = new GameObject("SealGate");
        go.transform.SetParent(parent, false);
        DemoSealGate gate = go.AddComponent<DemoSealGate>();

        Bounds doors = new Bounds(test.leftDoor.position, Vector3.zero);
        foreach (Renderer r in test.leftDoor.GetComponentsInChildren<Renderer>())
        {
            doors.Encapsulate(r.bounds);
        }

        foreach (Renderer r in test.rightDoor.GetComponentsInChildren<Renderer>())
        {
            doors.Encapsulate(r.bounds);
        }

        float face = doors.min.z - 0.012f; // 문짝 앞면 바로 앞
        float midX = doors.center.x;
        float h = doors.size.y;
        // 부적 순서 = 태우는 순서: 오른쪽 문 → 왼쪽 문 → 두 문을 가로지르는 마지막 봉인
        float y0 = doors.min.y;
        Vector3[] spots = { new Vector3(midX + 1.25f, y0 + h * 0.5f, face), new Vector3(midX - 1.3f, y0 + h * 0.56f, face - 0.004f), new Vector3(midX, y0 + h * 0.66f, face - 0.008f) };
        float[] tilt = { 8f, -6f, 0f };
        var gateTalismans = new Object[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            TalismanBurnEffect t = TalismanBuilder.Create(go.transform, "GateTalisman_" + (i + 1), spots[i], Quaternion.Euler(0f, 0f, tilt[i]), i == 2 ? 1.25f : 1f, false);
            gateTalismans[i] = t;
        }

        // 봉인 빛: 대문 앞 붉은 빛 — 부적이 탈 때마다 약해지고 불안하게 깜빡임
        Light seal = new GameObject("SealLight").AddComponent<Light>();
        seal.transform.SetParent(go.transform, false);
        seal.transform.position = new Vector3(midX, y0 + h * 0.6f, face - 1.8f);
        seal.type = LightType.Point;
        seal.color = new Color(1f, 0.3f, 0.18f); // 순수 빨강이면 노란 종이가 안 보여서 주황빛 섞음
        seal.range = 8f;
        seal.intensity = 6f;
        seal.shadows = LightShadows.None;

        // 대문을 위에서 은은히 비추는 차가운 스포트 — 어두운 맵에서 부적 붙은 대문이 보이게 (항상 켜짐)
        Light spot = new GameObject("GateSpot").AddComponent<Light>();
        spot.transform.SetParent(go.transform, false);
        spot.transform.position = new Vector3(midX, y0 + 5.6f, face - 5.6f);
        spot.transform.rotation = Quaternion.LookRotation(new Vector3(midX, y0 + 2.6f, face) - spot.transform.position);
        spot.type = LightType.Spot;
        spot.spotAngle = 65f;
        spot.innerSpotAngle = 30f;
        spot.range = 9f;
        spot.intensity = 3.5f;
        spot.color = new Color(0.8f, 0.88f, 1f);
        spot.shadows = LightShadows.None;

        // 원래 봉인 구슬 5개 + 에너지 줄기 — 2026-11-19 사용자 결정으로 씬에서 삭제함(부적 봉인만 씀). 남아 있으면 숨김
        var hidden = new System.Collections.Generic.List<Object>();
        foreach (SealOrb o in test.seals)
        {
            if (o != null)
            {
                hidden.Add(o.gameObject);
            }
        }

        Transform exitTile = test.leftDoor.root;
        Transform line = FindIn(exitTile, "EnerygyLine");
        if (line != null)
        {
            hidden.Add(line.gameObject);
        }

        // 봉인문 바로 앞(남쪽) 구역 — 부적이 남아 있으면 안내
        GameObject zoneGo = new GameObject("ApproachZone");
        zoneGo.transform.SetParent(go.transform, false);
        zoneGo.transform.position = new Vector3(midX, 2f, face - 6f);
        zoneGo.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10f, 4f, 10f);
        StoryZone zone = zoneGo.AddComponent<StoryZone>();

        var so = new SerializedObject(gate);
        so.FindProperty("gate").objectReferenceValue = test;
        DemoSetPieceBuilder.SetArray(so.FindProperty("gateTalismans"), gateTalismans);
        DemoSetPieceBuilder.SetArray(so.FindProperty("hiddenObjects"), hidden.ToArray());
        so.FindProperty("sealLight").objectReferenceValue = seal;
        so.FindProperty("sealIntensity").floatValue = seal.intensity;
        so.FindProperty("approachZone").objectReferenceValue = zone;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("출구 대문 봉인 부적 3장 (문 x " + doors.min.x.ToString("F1") + "~" + doors.max.x.ToString("F1") + ", 앞면 z " + face.ToString("F2") + "), 숨긴 오브젝트 " + hidden.Count);
    }

    private static Transform FindIn(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
