using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chapter1_FixedDemo 시연 동선(2026-11-18 확정)용 배치 — FixedDemoStoryBuilder.Build가 부름.
///  · 잠긴 문: 레버 문 3개(시작 복도 겹문·③ 침대 방)는 시작할 때 풂, 시작 복도 겹문은 열어 둠 (DemoUnlockDoors)
///  · 열쇠: 열쇠 문 6개마다 World_Key ×4 + 작은 빛. 부적7 방 열쇠는 ③ 침대 위(부적4 옆), 나머지는 문 앞쪽(복도 쪽) 바닥
///  · ④ 남동 계단2: 부적1을 태우고 북쪽 복도로 나오면 얼굴 없는 귀신이 서쪽 연결 복도에서 문을 열고 따라옴
///  · ⑤ 북동 계단1: 부적7을 태우고 1층 계단실 앞으로 나오면 왼쪽 방 문을 Xenosaurid가 두드리다 부수고 들이닥침
/// </summary>
public static class FixedDemoSetPieces
{
    private const string KeyPrefab = "Assets/Prefab/Item/World_Item/Active/World_Key.prefab";
    private const string FacelessPrefab = "Assets/Prefab/Enemy/Monster/Non-FaceGhost.prefab";
    private const string XenoPrefab = "Assets/Prefab/Enemy/Monster/Xenosaurid.prefab";
    private const string XenoRoarClip = "Assets/Sound/Dinosaur roar.mp3"; // 팀원 사운드 — ⑤ 예고 포효
    private const string FacelessScreamClip = "Assets/Sound/NonFaceGhostScream.wav"; // 팀원 사운드(얼굴 없는 귀신 추격 비명)

    internal static void Build(Transform parent, System.Text.StringBuilder log)
    {
        Doors(parent, log);
        Keys(parent, log);
        Faceless(parent, log);
        Xenosaurid(parent, log);
    }

    // ── 잠긴 문 ───────────────────────────────────

    private static void Doors(Transform parent, System.Text.StringBuilder log)
    {
        var levers = new List<Object>();
        var open = new List<Object>();
        foreach (LockedDoor d in Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None))
        {
            if (d.UnlockMethod != LockedDoor.UnlockType.Lever)
            {
                continue;
            }

            levers.Add(d);
            // 시작 복도 겹문(199, 0, 1~5)은 열어 둠 — ③ 방 문은 풀기만(플레이어가 E로 염)
            if (Vector3.Distance(d.transform.position, new Vector3(198.6f, 0f, 3f)) < 4f)
            {
                DoorClick click = d.GetComponent<DoorClick>();
                if (click != null)
                {
                    open.Add(click);
                }
            }
        }

        GameObject go = new GameObject("UnlockDoors");
        go.transform.SetParent(parent, false);
        var so = new SerializedObject(go.AddComponent<DemoUnlockDoors>());
        DemoSetPieceBuilder.SetArray(so.FindProperty("unlock"), levers.ToArray());
        DemoSetPieceBuilder.SetArray(so.FindProperty("open"), open.ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("레버 문 " + levers.Count + "개 풂, 그중 " + open.Count + "개 열어 둠");
    }

    // ── 열쇠 ─────────────────────────────────────

    private static void Keys(Transform parent, System.Text.StringBuilder log)
    {
        Transform root = new GameObject("Keys").transform;
        root.SetParent(parent, false);
        DemoTalisman t4 = NearestTalisman(new Vector3(320.8f, 1.2f, 61.4f));
        var surface = BakeReachability();
        int n = 0;
        foreach (LockedDoor d in Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None))
        {
            if (d.UnlockMethod != LockedDoor.UnlockType.Key)
            {
                continue;
            }

            Vector3 pos;
            string where;
            if (Vector3.Distance(d.transform.position, new Vector3(332.5f, 7f, 208.9f)) < 2f && t4 != null)
            {
                // 부적7 방 열쇠 → ③ 침대 위 부적4 옆 (동선 앞쪽에서 챙기게)
                pos = t4.transform.position + new Vector3(0f, 0.01f, -0.9f);
                where = "③ 침대 위";
            }
            else if (!ApproachSide(d.transform.position, out pos))
            {
                log.AppendLine("열쇠 자리 못 찾음: 문 " + d.transform.position.ToString("F0"));
                continue;
            }
            else
            {
                where = "문 앞";
            }

            Key(root, "Key_" + (++n), pos);
            log.AppendLine("열쇠" + n + " (" + where + (Reachable(pos) ? "" : ", 시작에서 경로 확인 안 됨") + ") " + pos.ToString("F1") + " → 문 " + d.transform.position.ToString("F0"));
        }

        // 참고 로그: 열쇠 없이(열쇠 문 닫힌 채) 시작에서 닿는 부적
        foreach (DemoTalisman t in Object.FindObjectsByType<DemoTalisman>(FindObjectsSortMode.None))
        {
            bool ok = false;
            for (int a = 0; a < 12 && !ok; a++)
            {
                Vector3 c = t.transform.position + new Vector3(Mathf.Cos(a * 0.5236f), 0f, Mathf.Sin(a * 0.5236f)) * 1.4f;
                ok = Floor(c, out Vector3 f) && Reachable(f);
            }

            log.AppendLine("  " + t.name + " " + t.transform.position.ToString("F0") + (ok ? " — 열쇠 없이 닿음" : " — 열쇠 문 너머(또는 경로 확인 안 됨)"));
        }

        ClearReachability(surface);
    }

    private static void Key(Transform parent, string name, Vector3 floor)
    {
        GameObject key = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefab), parent);
        key.name = name;
        key.transform.position = floor + Vector3.up * 0.01f;
        key.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        key.transform.localScale = Vector3.one * 4f; // 원본 9cm — 1.3배 플레이어 맵에서 보이게

        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(parent, false);
        glow.transform.position = floor + Vector3.up * 0.4f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.55f);
        glow.intensity = 0.7f;
        glow.range = 1.6f;
        glow.shadows = LightShadows.None;
        glow.transform.SetParent(key.transform, true); // 주우면 같이 사라지게
    }

    // 문 앞뒤(±x·±z 1.6~3.2m) 중 바닥이 있고 막히지 않은 쪽 — 시작 지점에서 실제로 걸어서 닿는 쪽(잠긴 방 반대편).
    // 닿는지 여부는 임시로 구운 NavMesh(일반·레버 문 열림, 열쇠 문 닫힘)로 판단 (BakeReachability).
    // (처음엔 '넓은 쪽'·'부적에서 먼 쪽'으로 골랐더니 방이 복도보다 넓거나 부적이 방 밖에 있을 때 잠긴 방 안에 놓였음)
    private static readonly Vector3 StartPoint = new Vector3(200f, 0f, -40f);

    private static bool Reachable(Vector3 floor)
    {
        if (!UnityEngine.AI.NavMesh.SamplePosition(StartPoint, out UnityEngine.AI.NavMeshHit a, 4f, UnityEngine.AI.NavMesh.AllAreas)
            || !UnityEngine.AI.NavMesh.SamplePosition(floor, out UnityEngine.AI.NavMeshHit b, 0.8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return false;
        }

        var path = new UnityEngine.AI.NavMeshPath();
        return UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path)
            && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
    }

    /// <summary>임시 NavMesh: 일반 문·레버 문 충돌체를 잠깐 끄고 구움 → 판단 끝나면 ClearReachability.</summary>
    internal static Unity.AI.Navigation.NavMeshSurface BakeReachability(bool openKeyDoors = false)
    {
        GameObject mg = GameObject.Find("MapGenerator");
        var surface = mg != null ? mg.GetComponent<Unity.AI.Navigation.NavMeshSurface>() : null;
        if (surface == null)
        {
            return null;
        }

        var off = new List<Collider>();
        var obstacles = new List<UnityEngine.AI.NavMeshObstacle>();
        foreach (DoorClick d in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            LockedDoor locked = d.GetComponentInParent<LockedDoor>();
            if (d.name.Contains("Wardrobe") || (!openKeyDoors && locked != null && locked.UnlockMethod == LockedDoor.UnlockType.Key))
            {
                continue;
            }

            foreach (Collider c in d.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger)
                {
                    c.enabled = false;
                    off.Add(c);
                }
            }

            foreach (var o in d.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            {
                if (o.enabled)
                {
                    o.enabled = false;
                    obstacles.Add(o);
                }
            }
        }

        surface.BuildNavMesh();
        off.ForEach(c => c.enabled = true);
        obstacles.ForEach(o => o.enabled = true);
        return surface;
    }

    internal static void ClearReachability(Unity.AI.Navigation.NavMeshSurface surface)
    {
        if (surface != null)
        {
            surface.RemoveData();
            surface.navMeshData = null; // 실행 중엔 팀원 LobbyEnemySpawner가 다시 구움
        }
    }

    private static bool ApproachSide(Vector3 door, out Vector3 best)
    {
        best = door;
        float bestArea = -1f;
        DemoTalisman inside = NearestTalisman(door, 40f);
        Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (Vector3 dir in dirs)
        {
            for (int k = 0; k < 9; k++)
            {
                float along = (k % 3 - 1) * 0.6f;
                float dist = 1.6f + (k / 3) * 0.8f; // 1.6·2.4·3.2m — 좁은 복도는 가까운 쪽만 비어 있음
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 c = door + dir * dist + side * along;
                if (!Floor(c, out Vector3 f) || Blocked(f))
                {
                    continue;
                }

                // 닿는 쪽이 최우선(+1000), 그다음 부적에서 먼 쪽 / 트인 쪽
                float area = (Reachable(f) ? 1000f : 0f) + (inside != null ? Vector3.Distance(f, inside.transform.position) : OpenArea(f) * 0.1f);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = f;
                }
            }
        }

        return bestArea >= 0f;
    }

    private static bool Floor(Vector3 p, out Vector3 floor)
    {
        floor = p;
        if (Physics.Raycast(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, out RaycastHit h, 2.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            floor = h.point;
            return true;
        }

        return false;
    }

    private static bool Blocked(Vector3 floor)
    {
        return Physics.CheckCapsule(floor + Vector3.up * 0.6f, floor + Vector3.up * 2f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
    }

    // 주변 12m를 16방향으로 레이 — 트인 거리 합 (복도·홀일수록 큼)
    private static float OpenArea(Vector3 floor)
    {
        float sum = 0f;
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            sum += Physics.Raycast(floor + Vector3.up * 1.2f, dir, out RaycastHit h, 12f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 12f;
        }

        return sum;
    }

    // ── ④ 얼굴 없는 귀신 (남동 계단2) ───────────────────

    private static void Faceless(Transform parent, System.Text.StringBuilder log)
    {
        DemoTalisman t1 = NearestTalisman(new Vector3(331.2f, 0.8f, -24.7f));
        Transform root = new GameObject("SetPiece4_Faceless").transform;
        root.SetParent(parent, false);

        // 부적 방에서 나와 북쪽 복도(x 302.5~306)로 올라가면 발동
        // 북쪽으로 가는 실제 복도(x 298~302) — 예전 (304.4, 폭 4.5)는 Wall (13) 동쪽의 벽으로 막힌 빈 공간이라 플레이어가 못 들어가 ④가 절대 안 터졌음(2026-11-19 확인)
        StoryZone zone = Zone(root, "Trigger", new Vector3(300.0f, 1.5f, 15f), new Vector3(3.6f, 3f, 14f));
        // 서쪽 연결 복도(시작 타일에서 들어온 길) — 플레이어 뒤에서 문을 열고 나타남
        Vector3 spawnPos = Ground(new Vector3(257f, 0f, 2.5f));
        Transform spawn = DemoSetPieceBuilder.Point(root, "Spawn", spawnPos);
        spawn.rotation = Quaternion.LookRotation(Vector3.right);
        // 지나올 문 2개(타일 입구·복도 앞)를 연출이 열어 줌 — 닫힌 문 앞에서 귀신이 멈췄음(실측 (291.9, 0, 2.3)에서 정지)
        var doors = new List<Object>();
        foreach (Vector3 p in new[] { new Vector3(265.5f, 0f, 2f), new Vector3(293.9f, 0f, 3.7f) })
        {
            DoorClick d = NearestDoor(p, 3f);
            if (d != null)
            {
                doors.Add(d);
            }
        }

        // 복도 앞 문(293.9, 0, 3.7) 자리는 실행 중 NavMesh를 문이 닫힌 채 구워 문틀에 길이 없음 → 문을 열어도 귀신이 x 291에서 멈춤.
        // 문틀을 건너는 NavMeshLink로 이어 줌 (실측: x 293만 NavMesh 없음, 양쪽 291·295는 있음)
        NavLink(root, "DoorLink", new Vector3(291.2f, 0f, 3f), new Vector3(296f, 0f, 3f), 1.2f);

        DoorClick westDoor = doors.Count > 0 ? (DoorClick)doors[0] : null;
        EnemyBase ghost = Creature(root, FacelessPrefab, "FacelessGhost", spawn, 1f);
        Ambush(root, t1, zone, ghost, spawn, doors.ToArray(), new Object[0], 0.4f,
            string.Empty, string.Empty); // 대사 없이 연출만 (2026-11-19 사용자 결정)
        // 나타나기 전, 구역 뒤쪽 남서 문(얼굴 없는 귀신이 지나올 문) 근처에서 비명 한 번 — 플레이어에게 붙이지 않고 그 자리 3D 소리 (2026-11-19 사용자 요청)
        Transform scream = DemoSetPieceBuilder.Point(root, "ForeshadowScream", new Vector3(293.9f, 1.6f, 3.7f));
        var fso = new SerializedObject(root.GetComponent<DemoAmbushSequence>());
        fso.FindProperty("foreshadowClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FacelessScreamClip);
        fso.FindProperty("foreshadowPoint").objectReferenceValue = scream;
        fso.ApplyModifiedPropertiesWithoutUndo();
        // 안 보이는 곳에서 오면 다가오는 걸 몰랐음 → 멀리 보이는 곳에 멈춰 섰다가 달려옴 + 쫓기 시작하면 바로 추격 연출
        var facelessSo = new SerializedObject(root.GetComponent<DemoAmbushSequence>());
        facelessSo.FindProperty("revealInSight").boolValue = true;
        facelessSo.FindProperty("chaseEffectOnStart").boolValue = true;
        facelessSo.FindProperty("leaveAfterHide").boolValue = true; // 숨어서 버티면 나타났던 자리로 돌아가 사라짐
        facelessSo.FindProperty("revealLine").stringValue = string.Empty; // 대사 없이 연출만 (위 결정과 같게)
        facelessSo.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("④ 얼굴 없는 귀신: 부적" + Index(t1) + " 뒤, 등장 " + spawnPos.ToString("F1") + ", 열어 줄 문 " + doors.Count + "개" + (westDoor != null ? "" : " (서쪽 문 못 찾음)"));
    }

    // ── ⑤ Xenosaurid (북동 계단1) ─────────────────────

    // ⑤ 로비 (2026-11-19 사용자 결정 '루트대로 가면 Xeno가 안 나옴 → 로비 부적에 Xeno 연출'): 로비 대기 의자 부적을 태우면
    // 로비 모퉁이 너머(부적에서 안 보이는 18~30m, 걸어서 45m 안)에서 Xenosaurid가 튀어나와 추격. 로비 1층엔 부서지는 문이 없어 문 없이 등장.
    // 예전 ⑤(북동 계단1 2층 부적7 → 1층 계단실 옆방 문 부수기)는 루트에서 가장 먼 구간(약 320m)이라 뺌 — 부적7은 예비 부적으로 남음.
    private static void Xenosaurid(Transform parent, System.Text.StringBuilder log)
    {
        DemoTalisman seal = NearestTalisman(FixedDemoStoryBuilder.LobbyTalismanAbove);
        Transform root = new GameObject("SetPiece5_Xenosaurid").transform;
        root.SetParent(parent, false);

        Vector3 from = seal != null ? seal.transform.position : FixedDemoStoryBuilder.LobbyTalismanAbove;
        Vector3 spawnPos = from;
        float bestLen = float.MaxValue;
        var surface = BakeReachability(openKeyDoors: true);
        try
        {
            NavMesh.SamplePosition(from, out NavMeshHit start, 4f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            for (float x = 150f; x <= 256f; x += 2f)
            {
                for (float z = 51f; z <= 151f; z += 2f)
                {
                    var c = new Vector3(x, 0.3f, z);
                    float flat = Vector2.Distance(new Vector2(x, z), new Vector2(from.x, from.z));
                    if (flat < 18f || flat > 30f || !NavMesh.SamplePosition(c, out NavMeshHit h, 0.8f, NavMesh.AllAreas) || h.position.y > 1f)
                    {
                        continue;
                    }

                    if (NavMesh.FindClosestEdge(h.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < 1.2f)
                    {
                        continue; // 큰 몸이 끼지 않게 트인 곳
                    }

                    if (!Physics.Linecast(from + Vector3.up * 1.5f, h.position + Vector3.up * 2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue; // 부적 자리에서 보이면 안 됨 — 모퉁이 너머
                    }

                    if (!NavMesh.CalculatePath(h.position, start.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    {
                        continue;
                    }

                    float len = 0f;
                    for (int k = 1; k < path.corners.Length; k++)
                    {
                        len += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                    }

                    if (len <= 45f && len < bestLen)
                    {
                        bestLen = len;
                        spawnPos = h.position;
                    }
                }
            }
        }
        finally
        {
            ClearReachability(surface);
        }

        Transform spawn = DemoSetPieceBuilder.Point(root, "Spawn", spawnPos);
        Vector3 look = from - spawnPos;
        look.y = 0f;
        spawn.rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

        EnemyBase xeno = Creature(root, XenoPrefab, "Xenosaurid", spawn, 1f);
        // 구역 없음 → 부적을 태우면(준비되면) 바로 발동, delay 1.2초 뒤 등장
        Ambush(root, seal, null, xeno, spawn, new Object[0], new Object[0], 0f, string.Empty, string.Empty);
        var so = new SerializedObject(root.GetComponent<DemoAmbushSequence>());
        so.FindProperty("delay").floatValue = 1.2f;
        // 숨을 시간 (2026-11-19 사용자 요청): 부적을 태우면 등장 자리(모퉁이 너머)에서 포효가 먼저 울리고 5초 뒤 달려옴 —
        // 대기 의자 바로 옆(2.4m)에 로비 트롤리 숨는 곳이 있음, 숨으면 사냥(huntSeconds)도 플레이어를 못 찾음
        so.FindProperty("foreshadowClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(XenoRoarClip);
        so.FindProperty("foreshadowPoint").objectReferenceValue = spawn;
        so.FindProperty("foreshadowLead").floatValue = 5f;
        so.FindProperty("foreshadowMaxDistance").floatValue = 50f;
        so.ApplyModifiedPropertiesWithoutUndo();
        // 1.3배였을 때 몸 캡슐 키가 3.9m라 문 위 벽에 걸려 못 지나갔음 → 크기 1배 + 캡슐 키 2.1m 상한 (실행 중 DemoAmbushSequence가 적용)
        // 예고는 위 포효(foreshadow)가 맡으므로 '멀리서 다가오는 예고 소리'는 끔 (둘 다 켜면 등장이 10초 가까이 늦어짐)
        var fitSo = new SerializedObject(root.GetComponent<DemoAmbushSequence>());
        fitSo.FindProperty("creatureScale").floatValue = 1f;
        fitSo.FindProperty("maxBodyHeight").floatValue = 2.1f;
        fitSo.FindProperty("approachCue").boolValue = false;
        fitSo.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("⑤ Xenosaurid(로비): 부적" + Index(seal) + " 뒤, 등장 " + spawnPos.ToString("F1") + (bestLen < float.MaxValue ? ", 부적까지 " + bestLen.ToString("F0") + "m" : " — 조건 맞는 자리 없음!"));
    }

    // ── 공용 ─────────────────────────────────────

    private static void Ambush(Transform root, DemoTalisman seal, StoryZone zone, EnemyBase creature, Transform spawn,
        Object[] openDoors, Object[] breakDoors, float breakDelay, string before, string after)
    {
        DemoAmbushSequence seq = root.gameObject.AddComponent<DemoAmbushSequence>();
        // 소리는 크리처 자리에서 나게 별도 자식에 (루트를 옮기면 구역·등장 자리까지 같이 밀림)
        GameObject audioGo = new GameObject("Audio");
        audioGo.transform.SetParent(root, false);
        audioGo.transform.position = spawn.position;
        AudioSource audio = audioGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.8f;
        var so = new SerializedObject(seq);
        so.FindProperty("sealIndex").intValue = seal != null ? new SerializedObject(seal).FindProperty("index").intValue : -1;
        so.FindProperty("triggerZone").objectReferenceValue = zone;
        so.FindProperty("creature").objectReferenceValue = creature;
        so.FindProperty("spawn").objectReferenceValue = spawn;
        DemoSetPieceBuilder.SetArray(so.FindProperty("openDoors"), openDoors);
        DemoSetPieceBuilder.SetArray(so.FindProperty("breakDoors"), breakDoors);
        so.FindProperty("breakDelay").floatValue = breakDelay;
        so.FindProperty("beforeLine").stringValue = before;
        so.FindProperty("afterLine").stringValue = after;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>괴물 크기 = 프리팹 원래 크기 × 씬 플레이어 크기 × extra — EnemyScene(플레이어 1배, 프리팹 그대로)과 같은 비율 (2026-11-19).
    /// 예전엔 Vector3.one × 1.5로 덮어써 프리팹 자체 크기(1.6~2배)보다 작아졌음.</summary>
    internal static Vector3 CreatureScale(string prefabPath, float extra = 1f)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject player = GameObject.FindWithTag("Player");
        float p = player != null ? player.transform.lossyScale.y : 1f;
        return (prefab != null ? prefab.transform.localScale : Vector3.one) * (p * extra);
    }

    private static EnemyBase Creature(Transform root, string prefabPath, string name, Transform spawn, float scale)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), root);
        go.name = name;
        go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        go.transform.localScale = CreatureScale(prefabPath, scale);
        go.SetActive(false); // 연출이 켬
        return go.GetComponent<EnemyBase>();
    }

    private static StoryZone Zone(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return go.AddComponent<StoryZone>();
    }

    private static void NavLink(Transform parent, string name, Vector3 a, Vector3 b, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var link = go.AddComponent<Unity.AI.Navigation.NavMeshLink>();
        GameObject mg = GameObject.Find("MapGenerator");
        var surface = mg != null ? mg.GetComponent<Unity.AI.Navigation.NavMeshSurface>() : null;
        if (surface != null)
        {
            link.agentTypeID = surface.agentTypeID;
        }

        link.startPoint = a;
        link.endPoint = b;
        link.width = width;
        link.bidirectional = true;
    }

    private static Vector3 Ground(Vector3 p)
    {
        return Floor(p, out Vector3 f) ? f : p;
    }

    private static DemoTalisman NearestTalisman(Vector3 p, float radius = 5f)
    {
        DemoTalisman best = null;
        float bd = radius;
        foreach (DemoTalisman t in Object.FindObjectsByType<DemoTalisman>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(t.transform.position, p);
            if (d < bd)
            {
                bd = d;
                best = t;
            }
        }

        return best;
    }

    private static DoorClick NearestDoor(Vector3 p, float radius)
    {
        DoorClick best = null;
        float bd = radius;
        foreach (DoorClick d in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            float dist = Vector3.Distance(d.transform.position, p);
            if (dist < bd && !d.name.Contains("Wardrobe"))
            {
                bd = dist;
                best = d;
            }
        }

        return best;
    }

    private static string Index(DemoTalisman t)
    {
        return t != null ? (new SerializedObject(t).FindProperty("index").intValue + 1).ToString() : "?(못 찾음)";
    }
}
