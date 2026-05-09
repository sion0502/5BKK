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

    Queue<Vector3> pathQueue = new Queue<Vector3>();

    float lastSeenTime = -999f;

    [SerializeField] float chaseMemoryTime = 2.0f;

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
            lastSeenTime = Time.time;
            currentState = State.Chase;

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
            if (Time.time - lastSeenTime > chaseMemoryTime && pathQueue.Count == 0)
            {
                currentState = State.Investigate;
                investigateTimer = 8f;
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
        DebugState();
    }

    bool IsPlayerHidden()
    {
        var cc = Player.GetComponent<CharacterController>();
        if (cc == null) return false;

        return cc.enabled == false;
    }

    bool CheckVision()
    {
        if (Player == null) return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 1.3f;
        Vector3 dir = (Player.position - origin).normalized;
        float dist = Vector3.Distance(origin, Player.position);

        if (dist > Data.detectRange) return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // 🔥 숨었으면 Player 무시
            if (IsPlayerHidden() && hit.collider.CompareTag("Player"))
                continue;

            // 🔥 문 처리
            if (hit.collider.CompareTag("Door"))
            {
                if (currentState != State.Chase)
                    return false;

                continue;
            }

            // 🔥 플레이어 감지
            if (hit.collider.CompareTag("Player"))
            {
                if (IsPlayerHidden())
                {
                    if (Time.time - lastSeenTime < chaseMemoryTime)
                        return true;

                    return false;
                }

                return true;
            }

            // 장애물
            if (((1 << hit.collider.gameObject.layer) & Data.obstacleLayer) != 0)
                return false;
        }

        return false;
    }

    void UpdateChase()
    {
        bool visible = CheckVision();

        if (visible)
        {
            Agent.SetDestination(Player.position);
            pathQueue.Clear();
        }
        else
        {
            if (pathQueue.Count > 0)
            {
                Vector3 target = pathQueue.Peek();
                Agent.SetDestination(target);

                if (Agent.remainingDistance < 1f)
                    pathQueue.Dequeue();
            }
        }

        // 🔥 문 공격 (추격 상태에서만)
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

    void DebugState()
    {
        if (currentState == State.Patrol)
            Debug.Log("🟢 순찰중");
        else if (currentState == State.Chase)
            Debug.Log("🔴 추격중");
        else if (currentState == State.Investigate)
            Debug.Log("🟡 수색중");
    }
}