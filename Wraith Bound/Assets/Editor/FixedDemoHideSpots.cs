using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 연출 ③(수술실 타일 1층 남동쪽 방, x 309~333 · z 51~81) 안에 숨는 곳 트롤리(본편 `Trolley_Hide`) 3개 (2026-11-19 사용자 요청 '2개 이상').
/// 맵의 다른 숨는 곳과 같은 종류·같은 크기(월드 1.5배). 자리 = 걸어서 닿는 바닥 중 벽에서 0.8~2m(벽에 붙여 둔 느낌),
/// 트롤리 크기 상자가 가구·벽과 안 겹치는 곳, 침대 부적에서 3m 밖, 문에서 3.5m 밖, 서로 4m 이상. 방향은 가까운 벽과 나란히.
/// FixedDemoStoryBuilder.Build가 부름(FixedDemo_Story/HideSpots3 만 다시 만듦).
/// </summary>
public static class FixedDemoHideSpots
{
    private const string RootName = "HideSpots3";
    private const string TrolleyPrefab = "Assets/Prefab/Object/Chapter1/Trolley_Hide.prefab";
    private const float WorldScale = 1.5f;
    private const int Count = 3;
    private static readonly Rect Room = Rect.MinMaxRect(310f, 52f, 332f, 80f);
    private static readonly Vector3 HalfSize = new Vector3(0.85f, 0.6f, 1.15f); // 트롤리(1.57×1.43×2.11m) 반 크기, 바닥 위 0.2m부터

    internal static void Build(Transform story, System.Text.StringBuilder log)
    {
        Transform old = story.Find(RootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrolleyPrefab);
        if (prefab == null)
        {
            log.AppendLine("③ 숨는 곳: 트롤리 프리팹 없음");
            return;
        }

        var avoid = new List<Vector3>();
        foreach (DemoTalisman t in story.GetComponentsInChildren<DemoTalisman>(true))
        {
            avoid.Add(t.transform.position);
        }

        // 문 앞은 비움 — 귀신이 들어오고 플레이어가 도망치는 길 (처음엔 북쪽 문 바로 앞 1~2m에 놓였음)
        var doors = new List<Vector3>();
        foreach (DoorClick d in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            Vector3 dp = EnemyDoorUtility.GetDoorWorldPosition(d.transform);
            if (dp.y < 3f && Room.Contains(new Vector2(dp.x, dp.z)) || (dp.x > Room.xMin - 3f && dp.x < Room.xMax + 3f && dp.z > Room.yMin - 3f && dp.z < Room.yMax + 3f && dp.y < 3f))
            {
                doors.Add(dp);
            }
        }

        Transform root = new GameObject(RootName).transform;
        root.SetParent(story, false);
        var placed = new List<Vector3>();
        var surface = FixedDemoSetPieces.BakeReachability(openKeyDoors: true);
        try
        {
            var rnd = new System.Random(311);
            var cands = new List<Vector3>();
            for (float x = Room.xMin; x <= Room.xMax; x += 0.75f)
            {
                for (float z = Room.yMin; z <= Room.yMax; z += 0.75f)
                {
                    cands.Add(new Vector3(x, 0.3f, z));
                }
            }

            for (int i = cands.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (cands[i], cands[j]) = (cands[j], cands[i]);
            }

            foreach (Vector3 c in cands)
            {
                if (placed.Count >= Count)
                {
                    break;
                }

                if (!NavMesh.SamplePosition(c, out NavMeshHit h, 0.5f, NavMesh.AllAreas) || h.position.y > 0.5f)
                {
                    continue;
                }

                if (!NavMesh.FindClosestEdge(h.position, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance < 0.8f || edge.distance > 2f)
                {
                    continue;
                }

                Vector3 p = h.position;
                if (avoid.Exists(a => (a - p).sqrMagnitude < 3f * 3f) || placed.Exists(q => (q - p).sqrMagnitude < 4f * 4f) ||
                    doors.Exists(d => new Vector2(d.x - p.x, d.z - p.z).sqrMagnitude < 3.5f * 3.5f))
                {
                    continue;
                }

                // 가까운 벽과 나란히 (트롤리 긴 쪽 = 로컬 z)
                Vector3 normal = edge.normal;
                normal.y = 0f;
                Quaternion rot = normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(Vector3.Cross(Vector3.up, normal.normalized)) : Quaternion.identity;
                if (Physics.CheckBox(p + Vector3.up * (0.2f + HalfSize.y), HalfSize, rot, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue; // 가구·벽과 겹침
                }

                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = "Trolley_Hide_" + (placed.Count + 1);
                go.transform.SetPositionAndRotation(p, rot);
                go.transform.localScale = Vector3.one * WorldScale;
                placed.Add(p);
            }
        }
        finally
        {
            FixedDemoSetPieces.ClearReachability(surface);
        }

        log.AppendLine("③ 숨는 곳 트롤리 " + placed.Count + "개: " + string.Join(", ", placed.ConvertAll(v => v.ToString("F1"))));
    }
}
