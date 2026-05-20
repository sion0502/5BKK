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

    bool sawPlayerLastFrame;

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
        bool hidden =
            IsPlayerHidden();

        bool visible =
            CheckVision();

        // 실제 감지
        if (visible)
        {
            lastSeenTime =
                Time.time;

            currentState =
                State.Chase;

            SavePlayerMoveDirection();

            SaveLastKnownPosition();
        }

        // 눈앞에서 숨은 경우
        if (PlayerHidingController.JustEnteredHiding &&
            sawPlayerLastFrame)
        {
            keepChasingAfterHide =
                true;

            PlayerHidingController
                .JustEnteredHiding =
                false;
        }

        // 추격 상태
        if (currentState == State.Chase)
        {
            // 눈앞에서 숨음
            if (keepChasingAfterHide)
            {
                agent.speed =
                    runSpeed;

                agent.SetDestination(
                    lastKnownPos);

                bool chaseReachedPoint =
                    !agent.pathPending &&
                    agent.remainingDistance < 1.5f;

                // 마지막 위치 도착
                if (chaseReachedPoint)
                {
                    keepChasingAfterHide =
                        false;

                    currentState =
                        State.Investigate;

                    investigateTimer =
                        8f;
                }

                UpdateAnimation();
                DebugState();

                sawPlayerLastFrame =
                    visible;

                return;
            }

            bool reachedPoint =
                !agent.pathPending &&
                agent.remainingDistance < 1.5f;

            // 못 본 틈에 숨음
            if (!visible &&
                !hidden &&
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
                agent.speed =
                    walkSpeed;

                UpdatePatrol();
                break;

            case State.Chase:
                agent.speed =
                    runSpeed;

                UpdateChase();
                break;

            case State.Investigate:
                agent.speed =
                    walkSpeed;

                UpdateInvestigate();
                break;
        }

        UpdateAnimation();
        DebugState();

        sawPlayerLastFrame =
            visible;
    }

    bool IsPlayerHidden()
    {
        CharacterController cc =
            player.GetComponent<CharacterController>();

        if (cc == null)
            return false;

        return !cc.enabled;
    }

    void SavePlayerMoveDirection()
    {
        CharacterController cc =
            player.GetComponent<CharacterController>();

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
        Vector3 predictedPos =
            player.position +
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
        if (player == null)
            return false;

        if (IsPlayerHidden())
            return false;

        Vector3 origin =
            eyePoint != null ?
            eyePoint.position :
            transform.position + Vector3.up * 1.6f;

        Vector3 target =
            player.position + Vector3.up * 1.2f;

        Vector3 dir =
            target - origin;

        float dist =
            dir.magnitude;

        if (dist > data.detectRange)
            return false;

        dir.Normalize();

        Debug.DrawRay(
            origin,
            dir * dist,
            Color.red);

        if (Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            dist,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
        }

        return false;
    }

    void UpdateChase()
    {
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
    }

    void UpdateInvestigate()
    {
        investigateTimer -=
            Time.deltaTime;

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
        patrolTimer -=
            Time.deltaTime;

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
        animator.SetFloat(
            "Speed",
            currentState == State.Chase ? 1f : 0.3f);
    }

    void DebugState()
    {
        Debug.Log(currentState);
    }
}