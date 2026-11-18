using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 시연 씬에서 연출과 별개로 맵을 돌아다니는 괴물 (2026-11-19 사용자 요청 "맵에 돌아다니는 애들이 없어서 재미없다").
///  · 괴물마다 spawnTiles 중 서로 다른 타일을 무작위로 골라, 그 타일 안 NavMesh 위(플레이어와 minPlayerDistance 이상)에 생성.
///  · 순찰 지점은 patrolTiles 전체(맵 전체)에 타일마다 pointsPerTile개 — 팀원 순찰 AI(EnemyBase)가 그 중 갈 수 있는 곳을 골라 다님.
///  · NavMesh는 씬의 LobbyEnemySpawner가 실행 중에 구움(팀원 코드 그대로). 그 전에 문짝(DoorClick)마다 NavMeshModifier(빌드 제외)를 붙여
///    문틀에도 길이 나게 함 — 닫힌 문은 DoorClick의 NavMeshObstacle(실행 중 깎기)이 막고, 괴물이 열면 뚫림(프롤로그 NavMesh와 같은 방식).
///    문짝을 넣고 구우면 문틀에 길이 아예 없어 괴물이 자기 방 밖으로 못 나갔음.
/// </summary>
public class DemoRoamingSpawner : MonoBehaviour
{
    [Serializable]
    public class Creature
    {
        public GameObject prefab;
        [Tooltip("추가 배수 — 크기는 '프리팹 원래 크기 × 플레이어 크기'(EnemyScene과 같은 비율, 2026-11-19)에 이걸 곱함")]
        public float extraScale = 1f;
    }

    [SerializeField] private Creature[] creatures = { };
    [Tooltip("스폰 후보 타일 — 괴물끼리 같은 타일엔 안 나옴")]
    [SerializeField] private Transform[] spawnTiles = { };
    [Tooltip("후보에 있어도 스폰·옮김에서 빼는 타일 이름 — 입구 바로 다음 첫 타일(시작하자마자 마주치지 않게, 2026-11-19 사용자 요청)")]
    [SerializeField] private string[] excludedTileNames = { "Tile_4Way (Corrider)" };
    [Tooltip("순찰 지점을 깔 타일 (맵 전체)")]
    [SerializeField] private Transform[] patrolTiles = { };
    [SerializeField] private NavMeshSurface surface;

    [SerializeField] private int pointsPerTile = 6;
    [SerializeField] private float[] floorHeights = { 0f, 7f };
    [Tooltip("타일 가장자리에서 이만큼 안쪽에서만 고름 (옆 타일과 겹치지 않게)")]
    [SerializeField] private float tileMargin = 5f;
    [Tooltip("이 거리보다 가까우면 스폰 안 함")]
    [SerializeField] private float minPlayerDistance = 30f;
    [Tooltip("벽에서 이만큼 떨어진 곳만 (가구 위·좁은 틈 제외)")]
    [SerializeField] private float minEdgeDistance = 1f;
    [SerializeField] private bool bakeDoorwaysOpen = true;
    [SerializeField] private float bakeTimeout = 10f;

    private static readonly FieldInfo ZoneField =
        typeof(EnemyBase).GetField("patrolPointZone", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly List<EnemyBase> spawned = new List<EnemyBase>();

    public IReadOnlyList<EnemyBase> Spawned => spawned;

    private static DemoRoamingSpawner instance;
    private PatrolPointZone roamZone;

    private static readonly FieldInfo DataField =
        typeof(EnemyBase).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 연출 괴물이 나타나면 같은 종류의 배회 괴물을 없앰 — 같은 괴물이 둘 돌아다니지 않게 (2026-11-19 사용자 요청).
    /// 종류 = 괴물 데이터 에셋 이름(복제본 꼬리표 '(Demo)'·'(Clone)' 뗌). DemoOperatingRoomSequence·DemoAmbushSequence가 괴물을 켤 때 부름.
    /// </summary>
    public static void RetireSameKind(EnemyBase appeared)
    {
        if (instance == null || appeared == null)
        {
            return;
        }

        string kind = KindOf(appeared);
        foreach (EnemyBase roamer in instance.spawned)
        {
            if (roamer != null && roamer != appeared && roamer.gameObject.activeSelf && KindOf(roamer) == kind)
            {
                roamer.gameObject.SetActive(false);
                Debug.Log("[Demo] 연출 " + appeared.name + " 등장 → 같은 종류 배회 괴물 " + roamer.name + " 없앰");
            }
        }

        // 나타난 연출 괴물이 이제 그 자리를 이어받아 맵 전체를 돌아다님 (순찰 지점·문 열기·갇힘 대비) — 켠 직후, Start 전이라 순찰 구역이 먹힘
        if (instance.roamZone != null && ZoneField != null)
        {
            ZoneField.SetValue(appeared, instance.roamZone);
        }

        if (appeared.GetComponent<DemoMonsterDoorHelper>() == null)
        {
            appeared.gameObject.AddComponent<DemoMonsterDoorHelper>().Init(instance);
        }

        if (!instance.spawned.Contains(appeared))
        {
            instance.spawned.Add(appeared);
        }
    }

    private static string KindOf(EnemyBase enemy)
    {
        var data = DataField != null ? DataField.GetValue(enemy) as Monsters : null;
        string n = data != null ? data.name : enemy.name;
        return n.Replace(" (Demo)", string.Empty).Replace("(Clone)", string.Empty).Trim();
    }

    private void OnEnable()
    {
        instance = this;
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Awake()
    {
        if (bakeDoorwaysOpen)
        {
            int n = 0;
            foreach (DoorClick door in FindObjectsByType<DoorClick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (door.GetComponent<NavMeshModifier>() == null)
                {
                    NavMeshModifier m = door.gameObject.AddComponent<NavMeshModifier>();
                    m.ignoreFromBuild = true;
                    n++;
                }
            }

            Debug.Log("[Demo] 배회 괴물: 문짝 " + n + "개를 NavMesh 굽기에서 뺌");
        }
    }

    private IEnumerator Start()
    {
        NavMeshData before = surface != null ? surface.navMeshData : null;
        float t = 0f;
        while (surface != null && surface.navMeshData == before && t < bakeTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        PatrolPointZone zone = BuildPatrolZone();
        roamZone = zone;
        SpawnAll(zone);
    }

    private PatrolPointZone BuildPatrolZone()
    {
        Transform zoneRoot = new GameObject("PatrolZone_Roam").transform;
        zoneRoot.SetParent(transform, false);
        int count = 0;
        foreach (Transform tile in patrolTiles)
        {
            if (tile == null)
            {
                continue;
            }

            for (int i = 0; i < pointsPerTile; i++)
            {
                if (TryRandomPoint(tile, 0f, out Vector3 p))
                {
                    Transform pt = new GameObject("Roam_" + tile.name + "_" + i).transform;
                    pt.SetParent(zoneRoot, false);
                    pt.position = p;
                    pt.gameObject.AddComponent<PatrolPoint>();
                    count++;
                }
            }
        }

        Debug.Log("[Demo] 배회 괴물: 순찰 지점 " + count + "개");
        return zoneRoot.gameObject.AddComponent<PatrolPointZone>();
    }

    private void SpawnAll(PatrolPointZone zone)
    {
        var tiles = new List<Transform>();
        foreach (Transform tile in spawnTiles)
        {
            if (tile != null && !IsExcluded(tile))
            {
                tiles.Add(tile);
            }
        }

        // 섞기
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }

        GameObject holder = new GameObject("RoamHolder");
        holder.SetActive(false);
        int next = 0;
        foreach (Creature c in creatures)
        {
            if (c == null || c.prefab == null)
            {
                continue;
            }

            // 남은 타일 중 자리가 나오는 첫 타일 — 쓴 타일은 빼서 같은 타일에 둘 안 나옴
            bool placed = false;
            while (next < tiles.Count && !placed)
            {
                Transform tile = tiles[next++];
                if (TryRandomPoint(tile, minPlayerDistance, out Vector3 pos))
                {
                    Spawn(c, tile, pos, zone, holder.transform);
                    placed = true;
                }
            }

            if (!placed)
            {
                Debug.LogWarning("[Demo] 배회 괴물: " + c.prefab.name + " 놓을 타일이 없음");
            }
        }

        Destroy(holder);
    }

    private void Spawn(Creature c, Transform tile, Vector3 pos, PatrolPointZone zone, Transform holder)
    {
        // 비활성 부모 아래에 만들어 Awake·Start 전에 크기·순찰 구역을 정함
        GameObject go = Instantiate(c.prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), holder);
        go.name = "Roam_" + c.prefab.name;
        // EnemyScene(플레이어 1배·괴물 프리팹 크기 그대로)과 같은 비율 — 예전엔 1.5로 덮어써 프리팹 크기(1.6~2배)보다 작아졌음
        GameObject player = GameObject.FindWithTag("Player");
        float playerScale = player != null ? player.transform.lossyScale.y : 1f;
        go.transform.localScale = c.prefab.transform.localScale * (playerScale * c.extraScale);
        EnemyBase enemy = go.GetComponent<EnemyBase>();
        if (enemy != null && ZoneField != null)
        {
            ZoneField.SetValue(enemy, zone);
        }

        DemoCreatureTuning.Apply(enemy); // 추격 속도 = 플레이어 달리기 (켜기 전에)

        go.SetActive(true);
        go.transform.SetParent(transform, true);
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.Warp(pos);
        }

        if (enemy != null)
        {
            spawned.Add(enemy);
        }

        go.AddComponent<DemoMonsterDoorHelper>().Init(this); // 닫힌 문 앞에서 포기하고 왔다 갔다만 하던 것 — 문을 열어 줌 + 갇히면 옮김

        Debug.Log("[Demo] 배회 괴물 " + c.prefab.name + " → " + tile.name + " " + pos.ToString("F1"));
    }

    private bool TryRandomPoint(Transform tile, float awayFromPlayer, out Vector3 point)
    {
        point = Vector3.zero;
        if (!TileRect(tile, out Rect rect))
        {
            return false;
        }

        GameObject player = GameObject.FindWithTag("Player");
        for (int i = 0; i < 80; i++)
        {
            float y = floorHeights.Length > 0 ? floorHeights[UnityEngine.Random.Range(0, floorHeights.Length)] : tile.position.y;
            var probe = new Vector3(UnityEngine.Random.Range(rect.xMin, rect.xMax), y + 0.5f, UnityEngine.Random.Range(rect.yMin, rect.yMax));
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 p = hit.position;
            if (!rect.Contains(new Vector2(p.x, p.z)) || Mathf.Abs(p.y - y) > 1.5f)
            {
                continue;
            }

            if (NavMesh.FindClosestEdge(p, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance < minEdgeDistance)
            {
                continue;
            }

            if (awayFromPlayer > 0f && player != null && Vector3.Distance(player.transform.position, p) < awayFromPlayer)
            {
                continue;
            }

            // 스폰 자리는 맵으로 걸어 나갈 수 있는 곳만 — 고립된 작은 바닥 조각(계단 밑 등)에 나와 꼼짝 못 했음(2026-11-19)
            if (awayFromPlayer > 0f && !ReachesFar(p))
            {
                continue;
            }

            point = p;
            return true;
        }

        return false;
    }

    // 다른 타일 쪽으로 6m 넘게 걸어갈 수 있는지(닫힌 문 앞에서 끊겨도 그만큼 가면 고립 아님)
    private bool ReachesFar(Vector3 p)
    {
        var path = new NavMeshPath();
        int tries = 0;
        foreach (Transform tile in patrolTiles)
        {
            if (tile == null || !TileRect(tile, out Rect r) || tries++ > 8)
            {
                continue;
            }

            if (!NavMesh.SamplePosition(new Vector3(r.center.x, p.y + 0.5f, r.center.y), out NavMeshHit h, 8f, NavMesh.AllAreas))
            {
                continue;
            }

            if (!NavMesh.CalculatePath(p, h.position, NavMesh.AllAreas, path))
            {
                continue;
            }

            float len = 0f;
            Vector3[] c = path.corners;
            for (int i = 1; i < c.Length; i++)
            {
                len += Vector3.Distance(c[i - 1], c[i]);
            }

            if (len >= 6f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>오래 갇힌 괴물을 플레이어에게서 멀고 안 보이는 다른 자리로 옮김 (DemoMonsterDoorHelper가 부름).</summary>
    public bool Relocate(NavMeshAgent agent)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Camera eye = Camera.main;
        for (int k = 0; k < 6; k++)
        {
            Transform tile = spawnTiles[UnityEngine.Random.Range(0, spawnTiles.Length)];
            if (tile == null || IsExcluded(tile) || !TryRandomPoint(tile, minPlayerDistance, out Vector3 p))
            {
                continue;
            }

            if (eye != null && !Physics.Linecast(eye.transform.position, p + Vector3.up * 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue; // 플레이어 눈에 보이는 자리엔 갑자기 나타나지 않게
            }

            agent.Warp(p);
            agent.ResetPath();
            Debug.Log("[Demo] 배회 괴물 " + agent.name + " 갇혀서 옮김 → " + tile.name + " " + p.ToString("F1"));
            return true;
        }

        return false;
    }

    private bool IsExcluded(Transform tile)
    {
        return excludedTileNames != null && System.Array.IndexOf(excludedTileNames, tile.name) >= 0;
    }

    private readonly Dictionary<Transform, Rect> rects = new Dictionary<Transform, Rect>();

    private bool TileRect(Transform tile, out Rect rect)
    {
        if (rects.TryGetValue(tile, out rect))
        {
            return true;
        }

        Renderer[] rs = tile.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
        {
            return false;
        }

        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs)
        {
            b.Encapsulate(r.bounds);
        }

        rect = Rect.MinMaxRect(b.min.x + tileMargin, b.min.z + tileMargin, b.max.x - tileMargin, b.max.z - tileMargin);
        rects[tile] = rect;
        return true;
    }
}
