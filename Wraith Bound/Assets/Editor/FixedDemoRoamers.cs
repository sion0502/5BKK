using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Chapter1_FixedDemo — 연출과 별개로 맵을 돌아다니는 괴물 4종 (2026-11-19 사용자 결정).
///  · 탈춤귀신·간호사·Xenosaurid·얼굴 없는 귀신, 실행 때 서로 다른 타일에 무작위 스폰 (DemoRoamingSpawner)
///  · 스폰 제외: 입구·출구, 연출 타일(계단2 ④·수술실 ③·계단1), 입구 다음 첫 타일(남쪽 4Way 복도) → 후보 5칸
///  · 순찰은 맵 전체 9칸
/// FixedDemoStoryBuilder.Build가 부르고, 이것만 다시 만들 땐 Tools > Story > Fixed Demo > Build Roaming Monsters.
/// </summary>
public static class FixedDemoRoamers
{
    private const string RootName = "Roamers";

    // 크기 = 프리팹 원래 크기 × 플레이어 크기 (EnemyScene과 같은 비율, 2026-11-19) — 여기 값은 추가 배수
    private static readonly (string path, float scale)[] Creatures =
    {
        ("Assets/Prefab/Enemy/Monster/MaskGhost.prefab", 1f),
        ("Assets/Prefab/Enemy/Monster/NurseGhost.prefab", 1f),
        ("Assets/Prefab/Enemy/Monster/Xenosaurid.prefab", 1f),
        ("Assets/Prefab/Enemy/Monster/Non-FaceGhost.prefab", 1f),
    };

    private static readonly string[] ExcludedTiles =
    {
        "Tile_2Way (Stair 2)", "Tile_3Way (OperatingRoom)", "Tile_2Way (Stair 1)",
        "Tile_4Way (Corrider)", // 입구 바로 다음 첫 타일 — 시작하자마자 마주치지 않게 (2026-11-19 사용자 요청)
    };

    [MenuItem("Tools/Story/Fixed Demo/Build Roaming Monsters")]
    public static void BuildMenu()
    {
        GameObject story = GameObject.Find("FixedDemo_Story");
        if (story == null)
        {
            Debug.LogError("[FixedDemoRoamers] FixedDemo_Story가 없음 — Chapter1_FixedDemo를 열고 Build Story Objects 먼저");
            return;
        }

        var log = new System.Text.StringBuilder();
        Build(story.transform, log);
        EditorSceneManager.MarkSceneDirty(story.scene);
        Debug.Log("[FixedDemoRoamers]\n" + log);
    }

    internal static void Build(Transform parent, System.Text.StringBuilder log)
    {
        Transform old = parent.Find(RootName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        LobbyEnemySpawner generator = Object.FindFirstObjectByType<LobbyEnemySpawner>();
        if (generator == null)
        {
            log.AppendLine("배회 괴물: LobbyEnemySpawner(MapGenerator) 없음 — 건너뜀");
            return;
        }

        var patrol = new List<Object>();
        var spawn = new List<Object>();
        foreach (Transform tile in generator.transform)
        {
            if (!tile.name.StartsWith("Tile_"))
            {
                continue;
            }

            patrol.Add(tile);
            if (System.Array.IndexOf(ExcludedTiles, tile.name) < 0)
            {
                spawn.Add(tile);
            }
        }

        GameObject go = new GameObject(RootName);
        go.transform.SetParent(parent, false);
        DemoRoamingSpawner spawner = go.AddComponent<DemoRoamingSpawner>();
        var so = new SerializedObject(spawner);

        SerializedProperty list = so.FindProperty("creatures");
        list.arraySize = Creatures.Length;
        for (int i = 0; i < Creatures.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Creatures[i].path);
            if (prefab == null)
            {
                log.AppendLine("배회 괴물: 프리팹 없음 " + Creatures[i].path);
            }

            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            e.FindPropertyRelative("extraScale").floatValue = Creatures[i].scale;
        }

        DemoSetPieceBuilder.SetArray(so.FindProperty("spawnTiles"), spawn.ToArray());
        DemoSetPieceBuilder.SetArray(so.FindProperty("patrolTiles"), patrol.ToArray());
        so.FindProperty("surface").objectReferenceValue = generator.GetComponent<NavMeshSurface>();
        so.ApplyModifiedPropertiesWithoutUndo();

        log.AppendLine("배회 괴물 " + Creatures.Length + "종 — 스폰 후보 " + spawn.Count + "칸, 순찰 " + patrol.Count + "칸");
    }
}
