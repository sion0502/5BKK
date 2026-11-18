using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 배회 괴물이 닫힌 문 앞에 오면 문을 열어 줌 (2026-11-19 Play 확인: 팀원 순찰 AI가 닫힌 문 앞까지 와서 못 열고 포기 → 다른 지점을 골라
/// 같은 구역을 왔다 갔다만 해서, 맵을 돌아다녀도 괴물과 마주치지 않았음). EnemyBase는 건드리지 않고 옆에서 문만 엶(문 자체 소리 — 플레이어에겐 신호).
/// 잠긴 문(LockedDoor 잠김)은 안 엶. 문이 열리면 DoorClick이 NavMesh 깎기를 풀어 AI가 그대로 지나감.
/// 25초 넘게 반경 8m 밖으로 못 나가면(고립된 바닥 조각·끼임·잠긴 문 앞 왕복) 플레이어에게서 멀고 안 보이는 다른 자리로 옮김(DemoRoamingSpawner.Relocate).
/// DemoRoamingSpawner가 배회 괴물마다 붙임.
/// </summary>
public class DemoMonsterDoorHelper : MonoBehaviour
{
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float interval = 0.25f;

    [Tooltip("이 시간 동안 stuckRadius 밖으로 못 나가면(고립·끼임·잠긴 문 앞 왕복) 다른 자리로 옮김")]
    [SerializeField] private float stuckSeconds = 25f;
    [SerializeField] private float stuckRadius = 8f;

    private NavMeshAgent agent;
    private Animator animator;
    private bool canBreak;
    private DemoRoamingSpawner spawner;
    private Vector3 lastMovedPos;
    private float lastMovedTime;
    private float next;

    public void Init(DemoRoamingSpawner owner)
    {
        spawner = owner;
    }
    private static List<DoorClick> doors;
    private static float doorsCachedAt = -1f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(true);
        canBreak = GetComponent<MonsterEnemy>() != null; // 문 부수는 괴물(탈춤귀신·간호사·Xenosaurid). 얼굴 없는 귀신(GhostEnemy)은 부수지 않음
    }

    private void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled || Time.time < next)
        {
            return;
        }

        next = Time.time + interval;
        if (PlayerDeathDebug.IsDying)
        {
            return;
        }

        CheckStuck();
        if (!agent.hasPath)
        {
            return;
        }

        // 가려는 쪽(다음 꺾임 점 방향)에 있는 가까운 닫힌 문
        Vector3 pos = transform.position;
        Vector3 ahead = agent.steeringTarget - pos;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.01f)
        {
            ahead = agent.desiredVelocity;
            ahead.y = 0f;
        }

        float r = reach * Mathf.Max(1f, transform.lossyScale.x * 0.8f);
        foreach (DoorClick door in Doors())
        {
            if (door == null || !door.isActiveAndEnabled || door.IsPassageReady())
            {
                continue;
            }

            Vector3 d = EnemyDoorUtility.GetDoorWorldPosition(door.transform) - pos;
            if (Mathf.Abs(d.y) > 2.5f)
            {
                continue;
            }

            d.y = 0f;
            if (d.sqrMagnitude > r * r || (ahead.sqrMagnitude > 0.01f && Vector3.Dot(d.normalized, ahead.normalized) < 0.1f))
            {
                continue;
            }

            // 지나가는 선(지금 위치 → 다음 꺾임 점) 가까이에 있는 문만 — 옆 벽의 캐비닛 문은 안 엶
            Vector3 seg = agent.steeringTarget - pos;
            seg.y = 0f;
            float t = seg.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(d, seg) / seg.sqrMagnitude) : 0f;
            if ((d - seg * t).sqrMagnitude > 1.3f * 1.3f)
            {
                continue;
            }

            // 쫓는 중인 '문 부수는 괴물'에겐 부서지는 문(DoorBrokenTest)은 안 열어 줌 — 팀원 AI가 직접 부숨
            // (예전엔 이 도우미가 먼저 열어 버려 연출 ③ 탈춤귀신이 문을 안 부수고 지나갔음, 2026-11-19)
            if (canBreak && door.GetComponent<DoorBrokenTest>() != null && animator != null && CreatureChaseState.IsChasing(animator))
            {
                continue;
            }

            LockedDoor locked = door.GetComponent<LockedDoor>();
            if (locked != null && locked.IsLocked)
            {
                continue;
            }

            DemoDoorOpener.Open(door, true);
        }
    }

    private void CheckStuck()
    {
        // 제자리뿐 아니라 좁은 곳을 왔다 갔다 하는 것도 갇힌 것으로 (Xenosaurid가 복도 10m를 1분 넘게 왕복 — 2026-11-19)
        if ((transform.position - lastMovedPos).sqrMagnitude > stuckRadius * stuckRadius || lastMovedTime == 0f)
        {
            lastMovedPos = transform.position;
            lastMovedTime = Time.time;
            return;
        }

        if (spawner == null || Time.time - lastMovedTime < stuckSeconds)
        {
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && Vector3.Distance(player.transform.position, transform.position) < 20f)
        {
            return; // 플레이어 근처(숨은 플레이어 앞에서 기다리는 중 등)면 그대로
        }

        if (spawner.Relocate(agent))
        {
            lastMovedPos = transform.position;
            lastMovedTime = Time.time;
        }
    }

    // 맵의 문 목록은 몇 초마다만 다시 모음(괴물 넷이 같이 씀)
    private static List<DoorClick> Doors()
    {
        if (doors == null || Time.time - doorsCachedAt > 5f || doorsCachedAt > Time.time)
        {
            doors = new List<DoorClick>(FindObjectsByType<DoorClick>(FindObjectsSortMode.None));
            doorsCachedAt = Time.time;
        }

        return doors;
    }
}
