using UnityEngine;
using UnityEngine.AI;

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

    float patrolTimer;
    float investigateTimer;

    float lastSeenTime = -999f;

    Vector3 lastKnownPos;

    [SerializeField] float chaseMemoryTime = 2f;

    bool sawPlayerAtHideMoment = false;

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

            sawPlayerAtHideMoment = false;

            SaveLastKnownPosition();
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

            if (!sawPlayerAtHideMoment)
            {
                if (Time.time - lastSeenTime > chaseMemoryTime &&
                    reachedLastPoint)
                {
                    currentState = State.Investigate;
                    investigateTimer = 8f;
                }
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

    bool IsPlayerHidden()
    {
        CharacterController cc =
            Player.GetComponent<CharacterController>();

        if (cc == null)
            return false;

        return cc.enabled == false;
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
            Vector3.Distance(origin, Player.position);

        if (dist > Data.detectRange)
            return false;

        if (!Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            dist))
        {
            return false;
        }

        if (hit.collider.CompareTag("Door"))
        {
            if (currentState != State.Chase)
                return false;

            Ray secondRay =
                new Ray(
                    hit.point + dir * 0.1f,
                    dir);

            if (Physics.Raycast(
                secondRay,
                out RaycastHit secondHit,
                dist))
            {
                if (secondHit.collider.CompareTag("Player"))
                {
                    if (IsPlayerHidden())
                        return false;

                    return true;
                }
            }

            return false;
        }

        if (hit.collider.CompareTag("Player"))
        {
            if (IsPlayerHidden())
                return false;

            return true;
        }

        return false;
    }

    void UpdateChase()
    {
        bool visible = CheckVision();

        bool playerHidden =
            IsPlayerHidden();

        if (visible || sawPlayerAtHideMoment)
        {
            Agent.SetDestination(
                Player.position);
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