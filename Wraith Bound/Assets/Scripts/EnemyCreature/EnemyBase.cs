using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase }
    public State currentState;

    public Monsters data;

    public NavMeshAgent agent;
    public Transform player;
    public Transform eyePoint;
    public Animator animator;

    float runSpeed;
    float walkSpeed;

    float forcedChaseTimer;
    float investigateTimer;

    Vector3 lastKnownPos;
    float patrolTimer;

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
            currentState = State.Chase;
            lastKnownPos = player.position;
            forcedChaseTimer = 5f;
        }
        else if (currentState == State.Chase)
        {
            forcedChaseTimer -= Time.deltaTime;

            if (forcedChaseTimer <= 0f)
            {
                currentState = State.Investigate;
                investigateTimer = 10f;
            }
        }

        switch (currentState)
        {
            case State.Patrol:
                agent.speed = walkSpeed;
                UpdatePatrol();
                break;

            case State.Chase:
                agent.speed = runSpeed;
                UpdateChase();
                break;

            case State.Investigate:
                agent.speed = walkSpeed;
                UpdateInvestigate();
                break;
        }

        UpdateAnimation();
    }

    bool CheckVision()
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.3f;
        Vector3 dir = (player.position - origin).normalized;
        float dist = Vector3.Distance(origin, player.position);

        if (dist > data.detectRange) return false;

        int mask = data.obstacleLayer | data.playerLayer;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, mask))
        {
            if (((1 << hit.collider.gameObject.layer) & data.obstacleLayer) != 0)
                return false;

            if (((1 << hit.collider.gameObject.layer) & data.playerLayer) != 0)
                return true;
        }

        return false;
    }

    void UpdatePatrol()
    {
        patrolTimer -= Time.deltaTime;

        if (patrolTimer > 0f) return;

        patrolTimer = Random.Range(2f, 4f);

        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;
            dir.Normalize();

            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, dir);

            // 🔥 벽/문 감지 → 바로 다른 방향
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                if (((1 << hit.collider.gameObject.layer) & data.obstacleLayer) != 0)
                    continue;

                if (hit.collider.CompareTag("Door"))
                    continue;
            }

            // 🔥 넓은 순찰 (핵심)
            Vector3 target = transform.position + dir * Random.Range(15f, 30f);

            if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                return;
            }
        }
    }

    void UpdateInvestigate()
    {
        investigateTimer -= Time.deltaTime;

        if (agent.remainingDistance < 2f)
        {
            // 🔥 작은 범위 수색 (핵심)
            Vector3 rand = lastKnownPos + Random.insideUnitSphere * 5f;
            rand.y = 0;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        if (investigateTimer <= 0f)
        {
            currentState = State.Patrol;

            // 🔥 수색 끝 → 바로 멀리 이동 (핵심)
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;

            Vector3 farTarget = transform.position + dir * Random.Range(20f, 35f);

            if (NavMesh.SamplePosition(farTarget, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }
    }

    void UpdateChase()
    {
        agent.SetDestination(player.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, 5f);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Door")) continue;

            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.IsBroken())
            {
                agent.SetDestination(door.transform.position);
                HandleDoor(door);
                return;
            }
        }
    }

    protected virtual void HandleDoor(DoorController door) { }

    void UpdateAnimation()
    {
        if (currentState == State.Patrol)
            animator.SetFloat("Speed", 0.3f);

        else if (currentState == State.Chase)
            animator.SetFloat("Speed", 1f);

        else if (currentState == State.Investigate)
            animator.SetFloat("Speed", 0.3f);
    }
}