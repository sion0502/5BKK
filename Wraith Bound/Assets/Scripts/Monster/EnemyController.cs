using UnityEngine;
using UnityEngine.AI;

public enum State
{
    PATROL,
    CHASE,
    SEARCH
}

public class EnemyController : MonoBehaviour
{
    public Transform player;
    public Monsters data;

    NavMeshAgent agent;
    Animator anim;

    State state;

    float currentSpeed;

    Vector3 lastSeenPosition;

    float searchTimer;
    float searchTime = 5f;

    float patrolTimer;
    float patrolTime;

    bool isChasing = false;

    DoorScript targetDoor;

    float attackTimer;
    float attackDelay = 1f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        agent.speed = data.moveSpeed;
        agent.stoppingDistance = 2.5f;

        state = State.PATROL;
        SetPatrolTime();
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool inSafeZone = IsPlayerInSafeZone();

        if (!inSafeZone && dist < data.detectRange)
        {
            isChasing = true;
            lastSeenPosition = player.position;
            state = State.CHASE;
        }
        else
        {
            if (isChasing)
            {
                state = State.SEARCH;
                searchTimer = 0f;
                isChasing = false;
                targetDoor = null;
            }
        }

        switch (state)
        {
            case State.PATROL: Patrol(); break;
            case State.CHASE: Chase(); break;
            case State.SEARCH: Search(); break;
        }

        currentSpeed = Mathf.Lerp(currentSpeed, agent.velocity.magnitude, Time.deltaTime * 10f);
    }

    void LateUpdate()
    {
        anim.SetFloat("speed", currentSpeed);
    }

    bool IsPlayerInSafeZone()
    {
        Collider[] cols = Physics.OverlapSphere(player.position, 1f);

        foreach (Collider col in cols)
        {
            if (col.CompareTag("SafeZone"))
                return true;
        }

        return false;
    }

    void Patrol()
    {
        agent.speed = data.moveSpeed;

        patrolTimer += Time.deltaTime;

        if (patrolTimer >= patrolTime || !agent.hasPath)
        {
            patrolTimer = 0f;
            SetPatrolTime();

            Vector3 rand = Random.insideUnitSphere * 20f + transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 20f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
    }

    void Chase()
    {
        agent.speed = data.moveSpeed * 2f;

        DetectDoor();

        if (targetDoor != null)
        {
            // 🔥 문을 발견하면 무조건 문으로 이동
            agent.isStopped = false;
            agent.SetDestination(targetDoor.transform.position);

            float dist = Vector3.Distance(
                transform.position,
                targetDoor.GetComponent<Collider>().ClosestPoint(transform.position)
            );

            if (dist < 3.5f)
            {
                agent.isStopped = true;

                transform.LookAt(targetDoor.transform);

                anim.SetTrigger("attack");

                AttackDoor();
            }
        }
        else
        {
            // 문 없으면 플레이어 추적
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    void DetectDoor()
    {
        RaycastHit hit;

        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 dir = (player.position - transform.position).normalized;

        int mask = LayerMask.GetMask("Door");

        if (Physics.Raycast(origin, dir, out hit, 7f, mask))
        {
            if (hit.collider.CompareTag("Door"))
            {
                DoorScript door = hit.collider.GetComponent<DoorScript>();

                if (door != null)
                {
                    targetDoor = door;
                    return;
                }
            }
        }

        targetDoor = null;
    }

    void AttackDoor()
    {
        if (targetDoor == null) return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackDelay)
        {
            attackTimer = 0f;

            targetDoor.TakeDamage(1);
        }
    }

    void Search()
    {
        agent.speed = data.moveSpeed;

        searchTimer += Time.deltaTime;

        if (searchTimer >= searchTime)
        {
            state = State.PATROL;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            Vector3 rand = Random.insideUnitSphere * 6f + lastSeenPosition;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 6f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    void SetPatrolTime()
    {
        patrolTime = Random.Range(2f, 4f);
    }
}