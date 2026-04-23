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

    // ⭐ 문 뒤 감지 완전 차단
    bool CheckVision()
    {
        if (player == null) return false;

        Vector3 origin = eyePoint.position;
        Vector3 dir = (player.position - origin).normalized;
        float dist = Vector3.Distance(origin, player.position);

        if (dist > data.detectRange) return false;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist))
        {
            if (hit.collider.CompareTag("Player"))
                return true;

            // ⭐ 문이면 무조건 차단
            if (hit.collider.CompareTag("Door"))
                return false;

            // 벽 차단
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Default"))
                return false;
        }

        return false;
    }

    void UpdatePatrol()
    {
        patrolTimer -= Time.deltaTime;

        if (patrolTimer > 0f) return;

        patrolTimer = Random.Range(2f, 4f);

        Vector3 dir = Random.insideUnitSphere;
        dir.y = 0;

        Vector3 target = transform.position + dir.normalized * Random.Range(8f, 15f);

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void UpdateInvestigate()
    {
        investigateTimer -= Time.deltaTime;

        if (agent.remainingDistance < 2f)
        {
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
    }
}