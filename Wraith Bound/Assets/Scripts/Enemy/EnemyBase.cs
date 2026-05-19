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

    [SerializeField] Monsters data;
    [SerializeField] Transform player;
    [SerializeField] Transform eyePoint;

    NavMeshAgent agent;
    Animator animator;

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
    Vector3 lastMoveDir;

    [SerializeField]
    float chaseMemoryTime = 2f;

    bool keepChasingAfterHide;

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
            data.moveSpeed * 0.35f;

        currentState =
            State.Patrol;

        agent.autoBraking = false;
    }

    void Update()
    {
        bool visible =
            CheckVision();

        bool hidden =
            IsPlayerHidden();

        // 플레이어를 실제로 봄
        if (visible)
        {
            lastSeenTime =
                Time.time;

            currentState =
                State.Chase;

            SavePlayerMoveDirection();

            SaveLastKnownPosition();

            // 아직 안 숨었음
            if (!hidden)
            {
                keepChasingAfterHide =
                    false;
            }
        }

        // 눈앞에서 숨음
        if (visible && hidden)
        {
            keepChasingAfterHide =
                true;
        }

        // 추격 상태
        if (currentState == State.Chase)
        {
            // 대놓고 본 상태에서 숨었으면
            // 절대 수색으로 안 감
            if (keepChasingAfterHide)
            {
                agent.SetDestination(
                    player.position);

                UpdateAnimation();
                DebugState();
                return;
            }

            bool reachedPoint =
                !agent.pathPending &&
                agent.remainingDistance < 1.5f;

            // 못 본 틈에 숨음
            if (!visible &&
                Time.time - lastSeenTime >
                chaseMemoryTime &&
                reachedPoint)
            {
                currentState =
                    State.Investigate;

                investigateTimer =
                    8f;
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
        DebugState();
    }

    bool IsPlayerHidden()
    {
        CharacterController cc =
            Player.GetComponent<CharacterController>();

        if (cc == null)
            return false;

        return !cc.enabled;
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
            lastMoveDir =
                moveDir;
        }
    }

    void SaveLastKnownPosition()
    {
        // 마지막 이동 방향까지 예측
        Vector3 predictedPos =
            Player.position +
            lastMoveDir * 4f;

        if (NavMesh.SamplePosition(
            predictedPos,
            out NavMeshHit hit,
            4f,
            NavMesh.AllAreas))
        {
            lastKnownPos =
                hit.position;
        }
    }

    bool CheckVision()
    {
        if (Player == null)
            return false;

        if (IsPlayerHidden())
            return false;

        Vector3 origin =
            eyePoint != null ?
            eyePoint.position :
            transform.position + Vector3.up * 1.5f;

        Vector3 target =
            Player.position + Vector3.up * 1f;

        Vector3 dir =
            (target - origin).normalized;

        float dist =
            Vector3.Distance(
                origin,
                target);

        if (dist > data.detectRange)
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

        if (hit.collider.CompareTag("Player"))
        {
            return true;
        }

        return false;
    }

    void UpdateChase()
    {
        // 문 부수고도 계속 앞으로 감
        agent.SetDestination(
            lastKnownPos);

        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                2f);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Door"))
                continue;

            DoorController door =
                hit.GetComponentInParent<DoorController>();

            if (door == null)
                continue;

            if (door.IsBroken())
                continue;

            HandleDoor(door);
            return;
        }
    }

    void UpdateInvestigate()
    {
        investigateTimer -= Time.deltaTime;

        if (!agent.pathPending &&
            agent.remainingDistance < 1f)
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
                agent.SetDestination(
                    hit.position);
            }
        }

        if (investigateTimer <= 0f)
        {
            keepChasingAfterHide =
                false;

            currentState =
                State.Patrol;
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
            agent.SetDestination(
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
            animator.SetFloat(
                "Speed",
                1f);
        }
        else
        {
            animator.SetFloat(
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
        else
        {
            Debug.Log("🟡 수색중");
        }
    }
}