using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Investigate,
        Chase
    }

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

    float patrolTimer;
    float investigateTimer;

    float lastSeenTime = -999f;

    Vector3 lastKnownPos;
    Vector3 lastSeenMoveDir;

    [SerializeField]
    float chaseMemoryTime = 2f;

    bool sawPlayerAtHideMoment = false;
    bool mustUseDoorPath = false;

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

            SaveLastKnownPosition();

            SavePlayerMoveDirection();

            sawPlayerAtHideMoment = false;
        }
        else if (currentState == State.Chase)
        {
            bool reachedLastPoint =
                !Agent.pathPending &&
                Agent.remainingDistance < 1f;

            bool playerHidden =
                IsPlayerHidden();

            bool sawRecently =
                Time.time - lastSeenTime < 0.5f;

            if (playerHidden && sawRecently)
            {
                sawPlayerAtHideMoment = true;
            }

            if (sawPlayerAtHideMoment)
            {
                return;
            }

            if (Time.time - lastSeenTime > chaseMemoryTime &&
                reachedLastPoint)
            {
                mustUseDoorPath = false;

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
        CharacterController cc =
            Player.GetComponent<CharacterController>();

        if (cc == null)
            return false;

        return cc.enabled == false;
    }

    void SavePlayerMoveDirection()
    {
        CharacterController cc =
            Player.GetComponent<CharacterController>();

        if (cc == null)
            return;

        Vector3 moveDir =
            cc.velocity.normalized;

        if (moveDir.magnitude > 0.1f)
        {
            lastSeenMoveDir = moveDir;
        }
    }

    void SaveLastKnownPosition()
    {
        if (NavMesh.SamplePosition(
            Player.position,
            out NavMeshHit hit,
            2f,
            NavMesh.AllAreas))
        {
            lastKnownPos = hit.position;
        }
    }

    bool CheckVision()
    {
        if (Player == null)
            return false;

        Vector3 origin =
            eyePoint != null ?
            eyePoint.position :
            transform.position + Vector3.up * 1.3f;

        Vector3 dir =
            (Player.position - origin).normalized;

        float dist =
            Vector3.Distance(
                origin,
                Player.position);

        if (dist > Data.detectRange)
            return false;

        Debug.DrawRay(
            origin,
            dir * dist,
            Color.red);

        if (!Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            dist))
        {
            return false;
        }

        Debug.Log(
            "맞은 오브젝트 : " +
            hit.collider.name);

        if (hit.collider.CompareTag("Player"))
        {
            return true;
        }

        if (hit.collider.CompareTag("Door"))
        {
            mustUseDoorPath = true;
        }

        return false;
    }

    void UpdateChase()
    {
        bool visible =
            CheckVision();

        if (visible || sawPlayerAtHideMoment)
        {
            mustUseDoorPath = false;

            Agent.SetDestination(
                Player.position);

            return;
        }

        if (mustUseDoorPath)
        {
            Vector3 nextDoorPathPos =
                lastKnownPos +
                (lastSeenMoveDir * 3f);

            Agent.SetDestination(
                nextDoorPathPos);
        }
        else
        {
            Agent.SetDestination(
                lastKnownPos);
        }

        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                2f);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Door"))
                continue;

            DoorController door =
                hit.GetComponentInParent<DoorController>();

            if (door != null &&
                !door.IsBroken())
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

        if (Agent.remainingDistance < 1f)
        {
            Vector3 rand =
                lastKnownPos +
                Random.insideUnitSphere * 4f;

            rand.y = 0;

            if (NavMesh.SamplePosition(
                rand,
                out NavMeshHit hit,
                4f,
                NavMesh.AllAreas))
            {
                Agent.SetDestination(
                    hit.position);
            }
        }

        if (investigateTimer <= 0f)
        {
            sawPlayerAtHideMoment = false;

            currentState = State.Patrol;
        }
    }

    void UpdatePatrol()
    {
        patrolTimer -= Time.deltaTime;

        if (patrolTimer > 0f)
            return;

        patrolTimer =
            Random.Range(2f, 4f);

        Vector3 rand =
            transform.position +
            Random.insideUnitSphere * 25f;

        rand.y = 0;

        if (NavMesh.SamplePosition(
            rand,
            out NavMeshHit hit,
            25f,
            NavMesh.AllAreas))
        {
            Agent.SetDestination(
                hit.position);
        }
    }

    protected virtual void HandleDoor(
        DoorController door)
    {
    }

    void UpdateAnimation()
    {
        if (currentState == State.Chase)
        {
            Anim.SetFloat(
                "Speed",
                1f);
        }
        else
        {
            Anim.SetFloat(
                "Speed",
                0.3f);
        }
    }

    void DebugState()
    {
        if (currentState == State.Patrol)
        {
            Debug.Log("🟢 순찰중");
        }
        else if (currentState == State.Chase)
        {
            Debug.Log("🔴 추격중");
        }
        else if (currentState == State.Investigate)
        {
            Debug.Log("🟡 수색중");
        }
    }
}