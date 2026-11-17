using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 맵(Scenes/Demo/Chapter1_Demo.unity — Chapter1 2 복제) 3×3 타일 배치. Tools > Story > Build Demo Map Layout.
/// 타일 100m 칸(기준점 = 칸 중앙, 크기 10배), 아래 가운데에 입구, 위 가운데에 출구.
/// · 각 칸이 이어져야 할 방향을 정해 두고, 타일의 출입구(Tile.openings — 회전 0일 때 Up=+z·Down=−z·Left=−x·Right=+x)가
///   그 방향을 모두 덮도록 90° 단위로 돌림
/// · 남는(바깥으로 난) 출입구는 Chapter1 2 병동 타일처럼 출입구 끝에 InteriorWall을 세워 막음
/// · 출구 타일은 한 줄 위로 옮김. 크리처·연출·NavMesh는 따로.
/// 다시 실행하면 3×3 칸의 타일을 지우고 새로 놓음(입구·출구 타일은 유지).
/// </summary>
public static class DemoMapBuilder
{
    private const string ScenePath = "Assets/Scenes/Demo/Chapter1_Demo.unity";
    private const string TileFolder = "Assets/Prefab/Map/Tile/Chapter1/";
    private const string SealWallPrefab = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/Hospital_Modules/Module_Interior/HH_Hospital_InteriorWall_4x4m.prefab";

    private const float CellSize = 100f;
    private const float TileScale = 10f;
    private static readonly Vector3 Origin = new Vector3(99.89f, 0f, 1.19f); // (0,0) 칸 중앙 — Chapter1 2와 같은 격자

    // 막는 벽 자세 — Chapter1 2 병동 타일 RightExit의 벽을 타일 로컬(회전 0, 오른쪽 출입구)로 잰 값
    private static readonly Vector3 SealLocalPos = new Vector3(4.834f, -0.632f, -0.848f);
    private static readonly Vector3 SealLocalEuler = new Vector3(0f, 180f, 0f);
    private static readonly Vector3 SealWorldScale = new Vector3(3.1f, 6.501f, 4.222f);

    private struct Slot
    {
        public int Col, Row;
        public string Prefab;
        public Dir Need;
    }

    // 북: 계단2 · 복도(+, 출구) · 계단3 / 가운데: 수술실 · 로비(+) · 약품실 / 남: 병동 · 방(+, 입구) · 창고
    private static readonly Slot[] Layout =
    {
        new Slot { Col = 0, Row = 2, Prefab = "Tile_2Way (Stair 2)", Need = Dir.Right | Dir.Down },
        new Slot { Col = 1, Row = 2, Prefab = "Tile_4Way (Corrider)", Need = Dir.Everything },
        new Slot { Col = 2, Row = 2, Prefab = "Tile_2Way (Stair 3)", Need = Dir.Left | Dir.Down },
        new Slot { Col = 0, Row = 1, Prefab = "Tile_3Way (OperatingRoom)", Need = Dir.Up | Dir.Down | Dir.Right },
        new Slot { Col = 1, Row = 1, Prefab = "Tile_4Way (Lobby)", Need = Dir.Everything },
        new Slot { Col = 2, Row = 1, Prefab = "Tile_3Way (Dispensary)", Need = Dir.Up | Dir.Down | Dir.Left },
        new Slot { Col = 0, Row = 0, Prefab = "Tile_3Way (Ward)", Need = Dir.Up | Dir.Right },
        new Slot { Col = 1, Row = 0, Prefab = "Tile_4Way (Room)", Need = Dir.Everything },
        new Slot { Col = 2, Row = 0, Prefab = "Tile_3Way (Storage)", Need = Dir.Up | Dir.Left },
    };

    [MenuItem("Tools/Story/Build Demo Map Layout")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[DemoMapBuilder] " + ScenePath + " 를 열고 실행");
            return;
        }

        // 3×3 칸에 있던 타일 제거 (입구·출구는 유지)
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name.StartsWith("Tile_") && go.name != "Tile_Entranc" && go.name != "Tile_exi")
            {
                Object.DestroyImmediate(go);
            }
        }

        var log = new System.Text.StringBuilder();
        foreach (Slot slot in Layout)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TileFolder + slot.Prefab + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("[DemoMapBuilder] 타일 없음: " + slot.Prefab);
                continue;
            }

            Dir open = prefab.GetComponent<Tile>() != null ? prefab.GetComponent<Tile>().openings : Dir.Everything;
            int steps = BestRotation(open, slot.Need, out Dir world);
            if (steps < 0)
            {
                Debug.LogError("[DemoMapBuilder] " + slot.Prefab + " 를 어떻게 돌려도 " + slot.Need + " 를 덮지 못함");
                continue;
            }

            Vector3 center = Origin + new Vector3(slot.Col * CellSize, 0f, slot.Row * CellSize);
            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            tile.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, steps * 90f, 0f));
            tile.transform.localScale = Vector3.one * TileScale;

            Dir extra = world & ~slot.Need;
            foreach (Dir d in new[] { Dir.Up, Dir.Down, Dir.Left, Dir.Right })
            {
                if ((extra & d) != 0)
                {
                    Seal(tile.transform, d);
                }
            }

            log.AppendLine(slot.Prefab + " @(" + slot.Col + "," + slot.Row + ") 회전 " + steps * 90 + "° 출입구 " + world + (extra != 0 ? " / 막음 " + extra : string.Empty));
        }

        // 출구는 3줄 맨 위 가운데 칸 위로
        GameObject exit = GameObject.Find("Tile_exi");
        if (exit != null)
        {
            Vector3 p = exit.transform.position;
            exit.transform.position = new Vector3(p.x, p.y, 250.6f + CellSize);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[DemoMapBuilder] 3×3 배치 완료\n" + log);
    }

    // ── NavMesh ─────────────────────────────────────────

    private const string NavMeshAssetPath = "Assets/Scenes/Demo/Chapter1_Demo/NavMesh-Demo.asset";

    /// <summary>
    /// 시연 씬 NavMesh를 에디터에서 구워 전용 파일로 저장 + 실행 중 다시 굽는 팀원 LobbyEnemySpawner는 이 씬에서만 끔.
    /// (실행 중 굽기는 일부 문 모듈 메쉬가 Read/Write 꺼져 있어 '에디터에선 되지만 빌드에선 안 됨' — 원본 Chapter1 2의 NavMesh 파일은 건드리지 않음)
    /// </summary>
    [MenuItem("Tools/Story/Bake Demo Map NavMesh")]
    public static void BakeNavMesh()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject host = FindRoot(scene, "MapGenerator");
        var surface = host != null ? host.GetComponent<Unity.AI.Navigation.NavMeshSurface>() : null;
        if (scene.path != ScenePath || surface == null)
        {
            Debug.LogError("[DemoMapBuilder] " + ScenePath + " 를 열고 실행 (MapGenerator의 NavMeshSurface 필요)");
            return;
        }

        foreach (MonoBehaviour mb in host.GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().Name == "LobbyEnemySpawner")
            {
                mb.enabled = false;
            }
        }

        // 문짝(DoorClick·LockedDoor)은 닫힌 채로 구우면 길이 막힘 → 굽는 동안만 충돌체를 끔
        // (원래 씬은 실행 중 DoorLayerSetter가 문을 굽기 대상에서 빠지는 레이어로 옮긴 뒤 구움, 닫힌 문은 NavMeshObstacle이 막음)
        var doorColliders = new List<Collider>();
        foreach (MonoBehaviour door in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (door == null || (door.GetType().Name != "DoorClick" && door.GetType().Name != "LockedDoor"))
            {
                continue;
            }

            foreach (Collider c in door.GetComponentsInChildren<Collider>())
            {
                if (c.enabled && !c.isTrigger)
                {
                    c.enabled = false;
                    doorColliders.Add(c);
                }
            }
        }

        surface.navMeshData = null;
        try
        {
            surface.BuildNavMesh();
        }
        finally
        {
            foreach (Collider c in doorColliders)
            {
                c.enabled = true;
            }
        }
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Demo/Chapter1_Demo"))
        {
            AssetDatabase.CreateFolder("Assets/Scenes/Demo", "Chapter1_Demo");
        }

        AssetDatabase.DeleteAsset(NavMeshAssetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
        surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshAssetPath);
        surface.AddData();
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[DemoMapBuilder] NavMesh 구움 → " + NavMeshAssetPath);
    }

    // ── 크리처 ─────────────────────────────────────────

    private const string MaskGhostPrefab = "Assets/Prefab/Enemy/Monster/MaskGhost.prefab";

    /// <summary>
    /// 크리처 3종을 각 날개에 배치(간호사는 프롤로그 전용이라 제거) + 날개 출입구를 잇는 순찰 지점.
    /// 북: Xenosaurid(복도·계단) / 서: 탈춤귀신(수술실) / 동: 얼굴 없는 귀신(약품실). 지점은 바닥 있고 벽 없는 곳으로 보정.
    /// </summary>
    [MenuItem("Tools/Story/Place Demo Map Creatures")]
    public static void PlaceCreatures()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("[DemoMapBuilder] " + ScenePath + " 를 열고 실행");
            return;
        }

        Physics.SyncTransforms();
        GameObject nurse = FindRoot(scene, "NurseGhost");
        if (nurse != null)
        {
            Object.DestroyImmediate(nurse);
        }

        GameObject mask = FindRoot(scene, "MaskGhost");
        if (mask == null)
        {
            mask = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MaskGhostPrefab), scene);
            mask.name = "MaskGhost";
            mask.SetActive(true);
        }

        mask.transform.localScale = Vector3.one * 1.5f;

        var log = new System.Text.StringBuilder();
        Wing(scene, FindRoot(scene, "Xenosaurid"), "PatrolZone_Xeno", Cell(1, 2) + new Vector3(0f, 0f, -38f),
            new[] { Cell(0, 2) + new Vector3(47f, 0f, 0f), Cell(2, 2) + new Vector3(-47f, 0f, 0f), Cell(1, 2) + new Vector3(0f, 0f, -47f) }, log);
        Wing(scene, mask, "PatrolZone_Mask", Cell(0, 1) + new Vector3(38f, 0f, 0f),
            new[] { Cell(0, 1) + new Vector3(0f, 0f, 47f), Cell(0, 1) + new Vector3(47f, 0f, 0f), Cell(0, 1) + new Vector3(0f, 0f, -47f) }, log);
        Wing(scene, FindRoot(scene, "Non-FaceGhost"), "PatrolZone_NonFace", Cell(2, 1) + new Vector3(0f, 0f, -38f),
            new[] { Cell(2, 1) + new Vector3(0f, 0f, 47f), Cell(2, 1) + new Vector3(-47f, 0f, 0f), Cell(2, 1) + new Vector3(0f, 0f, -47f) }, log);

        // 북쪽(복도·계단)·서쪽(수술실)·로비 문은 시작할 때 열어 둠 — 문 많은 곳에서 몬스터 순찰이 멈추던 것
        GameObject story = FindRoot(scene, "Demo_Story");
        if (story == null)
        {
            story = new GameObject("Demo_Story");
            SceneManager.MoveGameObjectToScene(story, scene);
        }

        DemoDoorOpener opener = story.GetComponent<DemoDoorOpener>() ?? story.AddComponent<DemoDoorOpener>();
        var oso = new SerializedObject(opener);
        SerializedProperty tiles = oso.FindProperty("tiles");
        string[] open = { "Tile_4Way (Corrider)", "Tile_2Way (Stair 2)", "Tile_2Way (Stair 3)", "Tile_3Way (OperatingRoom)", "Tile_4Way (Lobby)" };
        tiles.arraySize = open.Length;
        for (int i = 0; i < open.Length; i++)
        {
            GameObject t = FindRoot(scene, open[i]);
            tiles.GetArrayElementAtIndex(i).objectReferenceValue = t != null ? t.transform : null;
        }

        oso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[DemoMapBuilder] 크리처 배치\n" + log);
    }

    private static Vector3 Cell(int col, int row)
    {
        return Origin + new Vector3(col * CellSize, 0f, row * CellSize);
    }

    private static void Wing(Scene scene, GameObject creature, string zoneName, Vector3 start, Vector3[] points, System.Text.StringBuilder log)
    {
        if (creature == null)
        {
            log.AppendLine(zoneName + ": 크리처 없음");
            return;
        }

        GameObject zone = FindRoot(scene, zoneName);
        if (zone == null)
        {
            zone = new GameObject(zoneName);
            SceneManager.MoveGameObjectToScene(zone, scene);
            zone.AddComponent<PatrolPointZone>();
        }

        for (int i = zone.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(zone.transform.GetChild(i).gameObject);
        }

        zone.transform.position = start;
        for (int i = 0; i < points.Length; i++)
        {
            Transform p = new GameObject(i == 0 ? "PatrolPoint" : "PatrolPoint (" + i + ")").transform;
            p.SetParent(zone.transform, true);
            p.position = Walkable(points[i]);
            p.gameObject.AddComponent<PatrolPoint>(); // PatrolPointZone은 자식의 PatrolPoint 컴포넌트만 수집
        }

        // 구역에 직렬화된 지점 목록도 새 지점으로 — 기존 구역을 재사용하면 지운 예전 지점(빈 참조)을 계속 가리켜 순찰 지점을 못 골랐음
        var zso = new SerializedObject(zone.GetComponent<PatrolPointZone>());
        SerializedProperty list = zso.FindProperty("points");
        PatrolPoint[] fresh = zone.GetComponentsInChildren<PatrolPoint>();
        list.arraySize = fresh.Length;
        for (int i = 0; i < fresh.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = fresh[i];
        }

        zso.ApplyModifiedPropertiesWithoutUndo();

        creature.transform.position = Walkable(start);
        EnemyBase enemy = creature.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            var so = new SerializedObject(enemy);
            SerializedProperty prop = so.FindProperty("patrolPointZone");
            if (prop != null)
            {
                prop.objectReferenceValue = zone.GetComponent<PatrolPointZone>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        log.AppendLine(creature.name + " → " + creature.transform.position.ToString("F0") + " 순찰 " + points.Length + "곳");
    }

    // 바닥이 있고(1.3m 높이에서 아래로) 벽이 없는 곳 — 아니면 둘레로 조금씩 넓혀 찾음
    private static Vector3 Walkable(Vector3 p)
    {
        for (float r = 0f; r <= 6f; r += 0.5f)
        {
            for (int a = 0; a < (r == 0f ? 1 : 12); a++)
            {
                float ang = a * Mathf.PI / 6f;
                Vector3 c = p + new Vector3(Mathf.Cos(ang) * r, 1.3f, Mathf.Sin(ang) * r);
                if (!Physics.CheckBox(c, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    && Physics.Raycast(c, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return hit.point;
                }
            }
        }

        Debug.LogWarning("[DemoMapBuilder] 걸을 수 있는 곳을 못 찾음: " + p);
        return p;
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

    // 회전(90° 단위, 시계 방향 = +Y)마다 출입구를 월드 방향으로 바꿔 필요한 방향을 모두 덮는지 — 남는 출입구가 적은 회전 우선
    private static int BestRotation(Dir open, Dir need, out Dir world)
    {
        int best = -1;
        int bestExtra = int.MaxValue;
        world = Dir.None;
        for (int r = 0; r < 4; r++)
        {
            Dir w = Rotate(open, r);
            if ((w & need) != need)
            {
                continue;
            }

            int extra = Count(w & ~need);
            if (extra < bestExtra)
            {
                bestExtra = extra;
                best = r;
                world = w;
            }
        }

        return best;
    }

    // Y +90°: Up(+z)→Right(+x), Right→Down, Down→Left, Left→Up
    private static Dir Rotate(Dir d, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            Dir n = Dir.None;
            if ((d & Dir.Up) != 0) n |= Dir.Right;
            if ((d & Dir.Right) != 0) n |= Dir.Down;
            if ((d & Dir.Down) != 0) n |= Dir.Left;
            if ((d & Dir.Left) != 0) n |= Dir.Up;
            d = n;
        }

        return d;
    }

    private static int Count(Dir d)
    {
        int c = 0;
        foreach (Dir x in new[] { Dir.Up, Dir.Down, Dir.Left, Dir.Right })
        {
            if ((d & x) != 0) c++;
        }

        return c;
    }

    // 월드 방향 d의 출입구 끝에 벽을 세움 — 기준 자세(타일 로컬 오른쪽)를 그 방향으로 돌려서
    private static void Seal(Transform tile, Dir d)
    {
        Vector3 worldDir = d == Dir.Up ? Vector3.forward : d == Dir.Down ? Vector3.back : d == Dir.Left ? Vector3.left : Vector3.right;
        Vector3 localDir = tile.InverseTransformDirection(worldDir);
        Quaternion turn = Quaternion.FromToRotation(Vector3.right, localDir);
        GameObject wall = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SealWallPrefab), tile);
        wall.name = "DemoSeal_" + d;
        wall.transform.localPosition = turn * SealLocalPos;
        wall.transform.localRotation = turn * Quaternion.Euler(SealLocalEuler);
        Vector3 parent = tile.lossyScale;
        wall.transform.localScale = new Vector3(SealWorldScale.x / parent.x, SealWorldScale.y / parent.y, SealWorldScale.z / parent.z);
    }
}
