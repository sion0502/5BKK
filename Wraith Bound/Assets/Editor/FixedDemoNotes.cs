using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chapter1_FixedDemo 스토리 쪽지 후보 자리 (2026-11-19) — FixedDemo_Story/StoryNotes(StoryNoteSpawner)만 다시 만듦.
/// 문을 전부(열쇠 문 포함) 연 임시 NavMesh에서 시작 지점부터 걸어서 닿고, 벽 가까이(0.3~1.2m) 놓인 바닥 점을 1·2층 격자로 훑어,
/// 서로 MinSpacing 이상 떨어지게 고름(부적·열쇠 근처는 뺌). 실행 중 안 읽은 쪽지 수만큼 이 중 무작위로 놓임.
/// FixedDemoStoryBuilder.Build가 부르고, 따로는 Tools > Story > Fixed Demo > Build Story Notes.
/// </summary>
public static class FixedDemoNotes
{
    private const string RootName = "StoryNotes";
    private const float Grid = 2.5f;
    private const float MinSpacing = 14f;
    private static readonly float[] Floors = { 0f, 7f };

    [MenuItem("Tools/Story/Fixed Demo/Build Story Notes")]
    public static void BuildMenu()
    {
        GameObject story = GameObject.Find("FixedDemo_Story");
        if (story == null)
        {
            Debug.LogError("[FixedDemoNotes] FixedDemo_Story가 없음 — Chapter1_FixedDemo를 열고 Build Story Objects 먼저");
            return;
        }

        var log = new System.Text.StringBuilder();
        Build(story.transform, log);
        EditorSceneManager.MarkSceneDirty(story.scene);
        Debug.Log("[FixedDemoNotes] " + log);
    }

    internal static void Build(Transform story, System.Text.StringBuilder log)
    {
        Transform old = story.Find(RootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject player = GameObject.FindWithTag("Player");
        LobbyEnemySpawner generator = Object.FindFirstObjectByType<LobbyEnemySpawner>();
        if (player == null || generator == null)
        {
            log.AppendLine("쪽지: 플레이어·맵을 못 찾음 — 건너뜀");
            return;
        }

        var avoid = new List<Vector3>();
        foreach (DemoTalisman t in story.GetComponentsInChildren<DemoTalisman>(true))
        {
            avoid.Add(t.transform.position);
        }

        Transform keys = story.Find("Keys");
        if (keys != null)
        {
            foreach (Transform k in keys)
            {
                avoid.Add(k.position);
            }
        }

        // 맵 범위 = 타일 전체
        Bounds map = new Bounds(generator.transform.position, Vector3.zero);
        bool first = true;
        foreach (Transform tile in generator.transform)
        {
            if (!tile.name.StartsWith("Tile_"))
            {
                continue;
            }

            foreach (Renderer r in tile.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    map = r.bounds;
                    first = false;
                }
                else
                {
                    map.Encapsulate(r.bounds);
                }
            }
        }

        var candidates = new List<Vector3>();
        var surface = FixedDemoSetPieces.BakeReachability(openKeyDoors: true);
        try
        {
            NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 4f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            foreach (float fy in Floors)
            {
                for (float x = map.min.x + 1f; x < map.max.x; x += Grid)
                {
                    for (float z = map.min.z + 1f; z < map.max.z; z += Grid)
                    {
                        if (!NavMesh.SamplePosition(new Vector3(x, fy + 0.3f, z), out NavMeshHit h, 0.8f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - fy) > 0.3f)
                        {
                            continue;
                        }

                        if (!NavMesh.FindClosestEdge(h.position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance < 0.3f || edge.distance > 1.2f)
                        {
                            continue;
                        }

                        if (!NavMesh.CalculatePath(start.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                        {
                            continue;
                        }

                        // 바닥 실제 높이(아래로 레이)
                        Vector3 p = h.position;
                        if (Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out RaycastHit floor, 2f, ~0, QueryTriggerInteraction.Ignore) && floor.collider.GetComponentInParent<DoorClick>() == null)
                        {
                            p = floor.point;
                        }

                        candidates.Add(p);
                    }
                }
            }
        }
        finally
        {
            FixedDemoSetPieces.ClearReachability(surface);
        }

        // 섞은 뒤 서로 떨어지게 고름 (같은 칸 위·아래층은 높이 차로 구분)
        var rnd = new System.Random(1119);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var spots = new List<Vector3>();
        foreach (Vector3 c in candidates)
        {
            if (avoid.Exists(a => (a - c).sqrMagnitude < 5f * 5f))
            {
                continue;
            }

            if (spots.Exists(s => Mathf.Abs(s.y - c.y) < 3f && (s - c).sqrMagnitude < MinSpacing * MinSpacing))
            {
                continue;
            }

            spots.Add(c);
        }

        GameObject go = new GameObject(RootName);
        go.transform.SetParent(story, false);
        StoryNoteSpawner spawner = go.AddComponent<StoryNoteSpawner>();
        var so = new SerializedObject(spawner);
        SerializedProperty arr = so.FindProperty("spots");
        arr.arraySize = spots.Count;
        for (int i = 0; i < spots.Count; i++)
        {
            arr.GetArrayElementAtIndex(i).vector3Value = spots[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("쪽지 후보 자리 " + spots.Count + "곳 (격자 후보 " + candidates.Count + ")");
        if (spots.Count < StoryNoteCatalog.Count)
        {
            log.AppendLine("  ⚠ 쪽지 " + StoryNoteCatalog.Count + "장보다 자리가 적음 — MinSpacing을 줄일 것");
        }
    }
}
