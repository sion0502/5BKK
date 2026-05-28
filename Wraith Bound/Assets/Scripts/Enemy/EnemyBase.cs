using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : MonoBehaviour
{
    public enum State
    {
        Idle,
        Patrol,
        Chase
    }

    public State currentState;

    [SerializeField] Monsters data;
    [SerializeField] Transform player;
    [SerializeField] Transform eyePoint;

    NavMeshAgent agent;
    Animator animator;

    protected NavMeshAgent Agent => agent;
    protected Animator Anim => animator;

    float runSpeed;
    float walkSpeed;

    float idleTimer;

    float lastSeenTime = -999f;

    Vector3 lastKnownPos;

    [SerializeField]
    float chaseMemoryTime = 2f;

    void Awake()
    {
        agent =
            GetComponent<NavMeshAgent>();

        animator =
            GetComponentInChildren<Animator>();
    }

    void Start()
    {
        runSpeed =
            data.moveSpeed;

        walkSpeed =
            data.moveSpeed * 0.4f;

        currentState =
            State.Idle;

        idleTimer = 1f;

        agent.autoBraking = true;
    }

    void Update()
    {
        bool visible =
            CheckVision();

        if (visible)
        {
            lastSeenTime =
                Time.time;

            lastKnownPos =
                player.position;

            currentState =
                State.Chase;
        }

        switch (currentState)
        {
            case State.Idle:

                UpdateIdle();

                break;

            case State.Patrol:

                UpdatePatrol();

                break;

            case State.Chase:

                UpdateChase();

                break;
        }

        UpdateAnimation();
    }

    void UpdateIdle()
    {
        idleTimer -= Time.deltaTime;

        if (idleTimer > 0f)
            return;

        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir =
                Random.insideUnitSphere * 20f;

            randomDir += transform.position;

            if (NavMesh.SamplePosition(
                randomDir,
                out NavMeshHit hit,
                20f,
                NavMesh.AllAreas))
            {
                NavMeshPath path =
                    new NavMeshPath();

                bool canMove =
                    agent.CalculatePath(
                        hit.position,
                        path
                    );

                if (canMove &&
                    path.status ==
                    NavMeshPathStatus.PathComplete)
                {
                    agent.isStopped = false;

                    agent.speed =
                        walkSpeed;

                    agent.SetDestination(
                        hit.position
                    );

                    currentState =
                        State.Patrol;

                    return;
                }
            }
        }

        idleTimer = 1f;
    }

    void UpdatePatrol()
    {
        bool reached =
            !agent.pathPending &&
            agent.remainingDistance <=
            agent.stoppingDistance;

        if (!reached)
            return;

        agent.isStopped = true;

        currentState =
            State.Idle;

        idleTimer = 1f;
    }

    void UpdateChase()
    {
        agent.isStopped = false;

        agent.speed =
            runSpeed;

        agent.SetDestination(
            lastKnownPos);

        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                2f);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].CompareTag("Door"))
                continue;

            DoorController door =
                hits[i]
                .GetComponentInParent<DoorController>();

            if (door == null)
                continue;

            if (door.IsBroken())
                continue;

            HandleDoor(door);

            return;
        }

        bool reached =
            !agent.pathPending &&
            agent.remainingDistance <
            1.5f;

        if (!CheckVision() &&
            Time.time - lastSeenTime >
            chaseMemoryTime &&
            reached)
        {
            currentState =
                State.Idle;

            idleTimer = 1f;
        }
    }

    bool CheckVision()
    {
        Vector3 origin =
            eyePoint.position;

        Vector3 target =
            player.position + Vector3.up;

        Vector3 dir =
            target - origin;

        float dist =
            dir.magnitude;

        if (dist > data.detectRange)
            return false;

        dir.Normalize();

        if (Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            dist))
        {
            if (hit.collider.CompareTag("Door"))
            {
                return false;
            }

            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void HandleDoor(
        DoorController door)
    {
    }

    void UpdateAnimation()
    {
        switch (currentState)
        {
            case State.Idle:

                animator.SetInteger(
                    "State",
                    0
                );

                break;

            case State.Patrol:

                animator.SetInteger(
                    "State",
                    1
                );

                break;

            case State.Chase:

                animator.SetInteger(
                    "State",
                    2
                );

                break;
        }
    }
}