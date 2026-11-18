using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chapter1_FixedDemo 길 안내 루트 (시연 전용 기 '길잡이 혼불', 2026-11-19) — FixedDemo_Story/GuideRoute만 다시 만듦.
/// 순서: 열쇠(부적1 방) → 부적1 → ④ 구역 → 부적4(③) → 로비 부적(⑤) → 출구 (2026-11-19 단축 — 예전엔 침대 열쇠 → 부적7 → ⑤ 구역).
/// 단계 경로는 문을 전부(열쇠 문 포함) 연 임시 NavMesh로 구워 저장하고 임시 NavMesh는 지움(실행 중엔 팀원 스포너가 다시 구움).
/// FixedDemoStoryBuilder.Build가 부르고, 따로는 Tools > Story > Fixed Demo > Build Guide Route.
/// </summary>
public static class FixedDemoGuide
{
    private const string RootName = "GuideRoute";

    [MenuItem("Tools/Story/Fixed Demo/Build Guide Route")]
    public static void BuildMenu()
    {
        GameObject story = GameObject.Find("FixedDemo_Story");
        if (story == null)
        {
            Debug.LogError("[FixedDemoGuide] FixedDemo_Story가 없음 — Chapter1_FixedDemo를 열고 Build Story Objects 먼저");
            return;
        }

        var log = new System.Text.StringBuilder();
        Build(story.transform, log);
        EditorSceneManager.MarkSceneDirty(story.scene);
        Debug.Log("[FixedDemoGuide]\n" + log);
    }

    private struct StepDef
    {
        public string Label;
        public DemoGuideRoute.StepKind Kind;
        public Transform Target;
        public DemoTalisman Talisman;
        public StoryZone Zone;
    }

    internal static void Build(Transform story, System.Text.StringBuilder log)
    {
        Transform old = story.Find(RootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        var talismans = new Dictionary<int, DemoTalisman>();
        foreach (DemoTalisman t in story.GetComponentsInChildren<DemoTalisman>(true))
        {
            talismans[t.Index] = t;
        }

        DemoTalisman t1 = Get(talismans, 0), t4 = Get(talismans, 3), lobby = Get(talismans, 7); // 7 = 로비 대기 의자 부적(⑤)
        Transform keys = story.Find("Keys");
        StoryZone z4 = Find<StoryZone>(story, "SetPiece4_Faceless/Trigger");
        Transform gate = story.Find("SealGate/ApproachZone");
        SealTest sealTest = Object.FindFirstObjectByType<SealTest>(FindObjectsInactive.Include);
        Transform exitCube = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "ExitEventCube")
            {
                exitCube = t;
            }
        }
        GameObject player = GameObject.FindWithTag("Player");
        if (t1 == null || t4 == null || lobby == null || keys == null || gate == null || player == null)
        {
            log.AppendLine("길 안내: 부적·열쇠·출구·플레이어를 못 찾음 — 건너뜀");
            return;
        }

        var defs = new List<StepDef>
        {
            new StepDef { Label = "열쇠 (부적1 방)", Kind = DemoGuideRoute.StepKind.Key, Target = NearestKey(keys, t1.transform.position), Talisman = t1 },
            new StepDef { Label = "부적1", Kind = DemoGuideRoute.StepKind.Talisman, Target = t1.transform, Talisman = t1 },
            new StepDef { Label = "④ 구역", Kind = DemoGuideRoute.StepKind.Zone, Target = z4 != null ? z4.transform : null, Zone = z4, Talisman = t1 },
            new StepDef { Label = "부적4 (③ 수술실)", Kind = DemoGuideRoute.StepKind.Talisman, Target = t4.transform, Talisman = t4 },
            // 2026-11-19: ⑤ Xenosaurid를 로비 부적으로 옮겨 루트 단축 — 예전 부적7(북동 2층, 약 320m)·⑤ 구역 단계는 뺌
            new StepDef { Label = "로비 부적 (⑤ Xenosaurid)", Kind = DemoGuideRoute.StepKind.Talisman, Target = lobby.transform, Talisman = lobby },
            // 출구 = 봉인 대문을 지나 팀원 ExitEventCube(허브로 가는 트리거)까지 (2026-11-19 '출구까지도 안내') — 대문이 닫혀 있으면 혼불이 대문 앞에서 기다림
            new StepDef { Label = "출구", Kind = DemoGuideRoute.StepKind.Gate, Target = exitCube != null ? exitCube : gate },
        };
        defs.RemoveAll(d => d.Target == null);

        // 봉인 대문(SealTest 문 2짝, DoorClick 아님)도 열린 셈 치고 구움
        var gateCols = new List<Collider>();
        if (sealTest != null)
        {
            foreach (Transform door in new[] { sealTest.leftDoor, sealTest.rightDoor })
            {
                if (door == null)
                {
                    continue;
                }

                foreach (Collider c in door.GetComponentsInChildren<Collider>())
                {
                    if (c.enabled && !c.isTrigger)
                    {
                        c.enabled = false;
                        gateCols.Add(c);
                    }
                }
            }
        }

        var surface = FixedDemoSetPieces.BakeReachability(openKeyDoors: true);
        gateCols.ForEach(c => c.enabled = true);
        var paths = new List<Vector3[]>();
        Vector3 prev = player.transform.position;
        try
        {
            // 다음 단계는 이번 경로가 실제로 끝난 곳에서 시작 — 목표 중심점이 벽 너머 막힌 공간이면(⑤ 구역) 거기서 시작한 경로가 엉뚱한 섬에서 출발했음
            foreach (StepDef d in defs)
            {
                Vector3 to = d.Kind == DemoGuideRoute.StepKind.Zone && d.Zone != null ? ZoneTarget(d.Zone, prev) : d.Target.position;
                Vector3[] p = Path(prev, to, d.Label, log);
                paths.Add(p);
                if (NavMesh.SamplePosition(p[p.Length - 1], out NavMeshHit end, 2f, NavMesh.AllAreas))
                {
                    prev = end.position;
                }
            }
        }
        finally
        {
            FixedDemoSetPieces.ClearReachability(surface);
        }

        GameObject go = new GameObject(RootName);
        go.transform.SetParent(story, false);
        DemoGuideRoute route = go.AddComponent<DemoGuideRoute>();
        var so = new SerializedObject(route);
        SerializedProperty steps = so.FindProperty("steps");
        steps.arraySize = defs.Count;
        for (int i = 0; i < defs.Count; i++)
        {
            SerializedProperty e = steps.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("label").stringValue = defs[i].Label;
            e.FindPropertyRelative("kind").enumValueIndex = (int)defs[i].Kind;
            e.FindPropertyRelative("target").objectReferenceValue = defs[i].Target;
            e.FindPropertyRelative("talisman").objectReferenceValue = defs[i].Talisman;
            e.FindPropertyRelative("zone").objectReferenceValue = defs[i].Zone;
            SerializedProperty p = e.FindPropertyRelative("path");
            p.arraySize = paths[i].Length;
            for (int k = 0; k < paths[i].Length; k++)
            {
                p.GetArrayElementAtIndex(k).vector3Value = paths[i][k];
            }
        }

        SerializedProperty gd = so.FindProperty("gateDoors");
        DemoSetPieceBuilder.SetArray(gd, sealTest != null ? new Object[] { sealTest.leftDoor, sealTest.rightDoor } : new Object[0]);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Vector3[] Path(Vector3 from, Vector3 to, string label, System.Text.StringBuilder log)
    {
        // 목표가 공중에 떠 있으면(출구 ExitEventCube는 y 5) 바닥까지 넓혀 찾음
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 4f, NavMesh.AllAreas) ||
            (!NavMesh.SamplePosition(to, out NavMeshHit b, 4f, NavMesh.AllAreas) && !NavMesh.SamplePosition(to, out b, 8f, NavMesh.AllAreas)))
        {
            log.AppendLine("길 안내 " + label + ": NavMesh 위 점을 못 찾음 — 직선");
            return new[] { from, to };
        }

        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        Vector3[] corners = path.corners;
        float len = 0f;
        for (int i = 1; i < corners.Length; i++)
        {
            len += Vector3.Distance(corners[i - 1], corners[i]);
        }

        log.AppendLine("길 안내 " + label + ": " + path.status + ", 꺾임 " + corners.Length + ", " + len.ToString("F0") + "m" +
                       (corners.Length > 0 ? ", 끝 " + Vector3.Distance(corners[corners.Length - 1], b.position).ToString("F1") + "m 모자람" : ""));
        if (corners.Length == 0)
        {
            return new[] { a.position, b.position };
        }

        // 경로 끝이 목표에 조금 못 미치면(목표가 가구 위) 목표를 끝에 붙임 — 멀면(길이 끊김) 붙이지 않음: 예전엔 끊긴 끝에서 목표까지 직선을 이어 벽을 뚫었음
        var list = new List<Vector3>(corners);
        float gap = Mathf.Min(Vector3.Distance(corners[corners.Length - 1], to), Vector3.Distance(corners[corners.Length - 1], b.position));
        if (gap > 0.5f && gap < 2.5f)
        {
            list.Add(to);
        }
        else if (gap >= 2.5f)
        {
            log.AppendLine("  ⚠ " + label + ": 목표까지 " + gap.ToString("F1") + "m 길이 끊김 — 끊긴 곳까지만 안내");
        }

        return Densify(list);
    }

    // 구역 안에서 prev에서 걸어서 닿는(PathComplete) 칸 중 구역 중심에 가장 가까운 곳 — 구역 일부가 벽 속·막힌 공간일 수 있어서
    private static Vector3 ZoneTarget(StoryZone zone, Vector3 prev)
    {
        Bounds b = zone.GetComponent<Collider>().bounds;
        NavMesh.SamplePosition(prev, out NavMeshHit start, 4f, NavMesh.AllAreas);
        Vector3 best = b.center;
        float bestD = float.MaxValue;
        for (float x = b.min.x + 0.5f; x < b.max.x; x += 1f)
        {
            for (float z = b.min.z + 0.5f; z < b.max.z; z += 1f)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, b.min.y + 0.3f, z), out NavMeshHit h, 0.6f, NavMesh.AllAreas))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float d = (h.position - b.center).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = h.position;
                }
            }
        }

        return best;
    }

    // NavMesh 꺾임 점은 계단 구간을 긴 직선 하나로 잇기 때문에(높이가 비스듬히 직선) 혼불이 바닥·천장을 뚫고 다녔음
    // → 1m 간격으로 쪼개고 각 점 높이를 실제 바닥(아래로 레이, 문짝·트리거 제외)에 맞춤
    private const float Spacing = 1f;

    private static Vector3[] Densify(List<Vector3> pts)
    {
        var outPts = new List<Vector3> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z)) / Spacing));
            for (int k = 1; k <= n; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                outPts.Add(k == n ? b : Ground(p));
            }
        }

        return outPts.ToArray();
    }

    private static Vector3 Ground(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<DoorClick>() != null || h.collider.GetComponentInParent<EnemyBase>() != null ||
                h.collider.GetComponentInParent<CharacterController>() != null)
            {
                continue;
            }

            return h.point;
        }

        return p;
    }

    private static Transform NearestKey(Transform keys, Vector3 near)
    {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (InteractableItem item in keys.GetComponentsInChildren<InteractableItem>(true))
        {
            float d = Vector3.Distance(item.transform.position, near);
            if (d < bestD)
            {
                bestD = d;
                best = item.transform;
            }
        }

        return best;
    }

    private static DemoTalisman Get(Dictionary<int, DemoTalisman> map, int index)
    {
        return map.TryGetValue(index, out DemoTalisman t) ? t : null;
    }

    private static T Find<T>(Transform root, string path) where T : Component
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }
}
