using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemyBase : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase }
    public State currentState;

    [SerializeField] private Monsters data;
    [SerializeField] private Transform player;
    [SerializeField] private Transform eyePoint;

    private NavMeshAgent agent;
    private Animator animator;

    protected NavMeshAgent Agent => agent;
    protected Animator Anim => animator;
    protected Transform Player => player;
    protected Monsters Data => data;

    float runSpeed;
    float walkSpeed;

    float investigateTimer;
    float patrolTimer;

    Vector3 lastKnownPos;

    // 🔥 경로 기억
    Queue<Vector3> pathQueue = new Queue<Vector3>();

    // 🔥 추가: 마지막으로 "실제로 봤던" 시간
    float lastSeenTime = -999f;

    // 🔥 추가: 코너/문 뒤에서 "막 사라진 경우"만 잠깐 유지 (짧게!)
    [SerializeField] float lostGraceTime = 0.7f; // 0.5~1.0 추천

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        runSpeed = data.moveSpeed;
        walkSpeed = data.moveSpeed * 0.35f;
        currentState = State.Patrol;
    }

    void Update()
    {
        bool visible = CheckVision();

        if (visible)
        {
            // 🔥 실제로 봤을 때만 시간 갱신
            lastSeenTime = Time.time;

            if (currentState != State.Chase)
                Debug.Log("👉 CHASE 시작");

            currentState = State.Chase;

            // 🔥 "보는 동안에만" 경로에 추가
            Vector3 pos = Player.position;
            if (pathQueue.Count == 0 || Vector3.Distance(lastKnownPos, pos) > 1f)
            {
                pathQueue.Enqueue(pos);
                lastKnownPos = pos;

                if (pathQueue.Count > 30)
                    pathQueue.Dequeue();
            }
        }
        else if (currentState == State.Chase)
        {
            // 🔥 핵심: 최근에 본 적이 없으면(= 못 본 채 숨음)
            // + 더 이상 따라갈 경로도 없으면 → 바로 수색
            bool withinGrace = (Time.time - lastSeenTime) <= lostGraceTime;

            if (!withinGrace && pathQueue.Count == 0)
            {
                Debug.Log("👉 INVESTIGATE 시작");

                currentState = State.Investigate;
                investigateTimer = 10f;
            }
        }

        switch (currentState)
        {
            case State.Patrol:
                Agent.speed = walkSpeed;
                UpdatePatrol();
                break;

            case State.Chase:
                Agent.speed = runSpeed;
                UpdateChase();
                break;

            case State.Investigate:
                Agent.speed = walkSpeed;
                UpdateInvestigate();
                break;
        }

        UpdateAnimation();
    }

    // =========================
    // 🔥 시야 검사 (그대로)
    // =========================
    bool CheckVision()
    {
        if (Player == null) return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 1.3f;
        Vector3 dir = (Player.position - origin).normalized;
        float dist = Vector3.Distance(origin, Player.position);

        if (dist > Data.detectRange) return false;

        int mask = Data.obstacleLayer | Data.playerLayer;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            int layer = hit.collider.gameObject.layer;

            if (((1 << layer) & Data.obstacleLayer) != 0)
                return false;

            if (((1 << layer) & Data.playerLayer) != 0)
                return true;
        }

        return false;
    }

    // =========================
    // 🔥 추격 (기존 유지)
    // =========================
    void UpdateChase()
    {
        if (pathQueue.Count > 0)
        {
            Vector3 target = pathQueue.Peek();
            Agent.SetDestination(target);

            if (Agent.remainingDistance < 1f)
                pathQueue.Dequeue();
        }

        // 문 공격 (그대로)
        Collider[] hits = Physics.OverlapSphere(transform.position, 2f);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Door")) continue;

            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.IsBroken())
            {
                Agent.isStopped = true;
                HandleDoor(door);
                return;
            }
        }

        Agent.isStopped = false;
    }

    // =========================
    void UpdateInvestigate()
    {
        investigateTimer -= Time.deltaTime;

        if (Agent.remainingDistance < 1.5f)
        {
            Vector3 rand = lastKnownPos + Random.insideUnitSphere * 4f;
            rand.y = 0;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                Agent.SetDestination(hit.position);
        }

        if (investigateTimer <= 0f)
        {
            Debug.Log("👉 PATROL 복귀");
            currentState = State.Patrol;
        }
    }

    void UpdatePatrol()
    {
        patrolTimer -= Time.deltaTime;

        if (patrolTimer > 0f) return;

        patrolTimer = Random.Range(2f, 4f);

        Vector3 rand = transform.position + Random.insideUnitSphere * 25f;
        rand.y = 0;

        if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 25f, NavMesh.AllAreas))
            Agent.SetDestination(hit.position);
    }

    protected virtual void HandleDoor(DoorController door) { }

    void UpdateAnimation()
    {
        if (currentState == State.Chase)
            Anim.SetFloat("Speed", 1f);
        else
            Anim.SetFloat("Speed", 0.3f);
    }
}