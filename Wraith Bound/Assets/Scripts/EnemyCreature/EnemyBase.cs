using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyBase : MonoBehaviour
{
    public enum EnemyState { Patrol, Chase, Investigate }

    [Header("Core")]
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] protected Transform eyePoint;

    [Header("Perimeter")]
    [SerializeField] protected float patrolRange = 40f;
    [SerializeField] protected float defaultViewDistance = 60f;
    [SerializeField] protected float defaultViewAngle = 100f;

    protected Transform player;
    protected EnemyState currentState;

    protected float walkSpeed;
    protected float runSpeed;
    protected float viewDistance;
    protected float viewAngle;

    protected Vector3 lastKnownPosition;
    protected bool canSeePlayer;

    protected float forcedChaseTimer = 0f;
    float chaseMemoryTimer = 0f;
    float chaseMemoryDuration = 8f;

    float investigateTimer;
    Vector3 investigateCenter;

    float idleTimer = 0f;
    bool isIdling = false;

    bool investigateIdle = false;
    float investigateIdleTimer = 0f;

    // 의심 모드 상수: 10초 고정 지속
    const float InvestigateDuration = 10f;

    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentState = EnemyState.Patrol;
        viewDistance = defaultViewDistance;
        viewAngle = defaultViewAngle;
        SetRandomWalk();
    }

    protected virtual void Update()
    {
        // 벽/시야 이슈를 줄이기 위한 안정화 CheckVision
        CheckVision();

        switch (currentState)
        {
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Chase: UpdateChase(); break;
            case EnemyState.Investigate: UpdateInvestigate(); break;
        }

        UpdateAnimator();
    }

    void ResetMovement()
    {
        if (agent != null) agent.isStopped = false;
        isIdling = false;
        investigateIdle = false;
    }

    // 시야 판단: 벽 차단 포함
    void CheckVision()
    {
        if (player == null || eyePoint == null) return;

        Vector3 origin = eyePoint.position;
        Vector3 dir = (player.position - origin).normalized;
        float dist = Vector3.Distance(origin, player.position);

        bool seen = false;

        if (dist <= viewDistance)
        {
            float angle = Vector3.Angle(eyePoint.forward, dir);
            if (angle <= viewAngle)
            {
                RaycastHit hit;
                int layerMask = LayerMask.GetMask("Default", "Wall", "Player");
                if (Physics.Raycast(origin, dir, out hit, viewDistance, layerMask))
                {
                    if (hit.collider != null && hit.collider.CompareTag("Player"))
                    {
                        seen = true;
                        lastKnownPosition = player.position;
                    }
                }
            }
        }

        if (seen)
        {
            canSeePlayer = true;
            chaseMemoryTimer = chaseMemoryDuration;
            currentState = EnemyState.Chase;
            ResetMovement();
        }
        else
        {
            chaseMemoryTimer -= Time.deltaTime;
            if (chaseMemoryTimer <= 0f)
                canSeePlayer = false;
        }

        if (forcedChaseTimer > 0f)
        {
            forcedChaseTimer -= Time.deltaTime;
            canSeePlayer = true;
            currentState = EnemyState.Chase;
        }
    }

    void UpdatePatrol()
    {
        if (agent == null) return;
        agent.speed = walkSpeed;

        if (canSeePlayer)
        {
            currentState = EnemyState.Chase;
            ResetMovement();
            return;
        }

        if (isIdling)
        {
            idleTimer -= Time.deltaTime;
            agent.isStopped = true;
            if (idleTimer <= 0f)
            {
                isIdling = false;
                agent.isStopped = false;
                SetRandomWalk();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            if (Random.value < 0.08f)
            {
                isIdling = true;
                idleTimer = Random.Range(1f, 2f);
            }
            else
            {
                SetRandomWalk();
            }
        }
    }

    protected virtual bool IsActionBlocked()
    {
        return false;
    }

    void UpdateChase()
    {
        if (IsActionBlocked()) return;

        agent.speed = runSpeed;
        agent.isStopped = false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dirToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
        }

        // 플레이어 위치로 매 프레임 이동 목표
        agent.SetDestination(player.position);

        if (canSeePlayer)
        {
            lastKnownPosition = player.position;
            OnChaseUpdate();
            return;
        }

        if (chaseMemoryTimer > 0f)
        {
            agent.SetDestination(lastKnownPosition);
            return;
        }

        // 시야를 잃고 기억도 없으면 Investigate
        investigateTimer = InvestigateDuration;
        investigateCenter = lastKnownPosition;
        currentState = EnemyState.Investigate;
        ResetMovement();
    }

    void UpdateInvestigate()
    {
        agent.speed = walkSpeed;

        if (canSeePlayer)
        {
            currentState = EnemyState.Chase;
            ResetMovement();
            return;
        }

        investigateTimer -= Time.deltaTime;

        if (investigateIdle)
        {
            investigateIdleTimer -= Time.deltaTime;
            agent.isStopped = true;
            if (investigateIdleTimer <= 0f)
            {
                investigateIdle = false;
                agent.isStopped = false;
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            if (Random.value < 0.3f)
            {
                investigateIdle = true;
                investigateIdleTimer = Random.Range(1f, 2.5f);
            }
            else
            {
                Vector3 randomPos = investigateCenter + Random.insideUnitSphere * 6f;
                randomPos.y = 0;

                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }
        }

        if (investigateTimer <= 0f)
        {
            currentState = EnemyState.Patrol;
            ResetMovement();
            SetRandomWalk();
        }
    }

    void SetRandomWalk()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRange;
            randomDir += transform.position;
            randomDir.y = 0;

            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, patrolRange, NavMesh.AllAreas))
            {
                if (Vector3.Distance(hit.position, transform.position) > 1f)
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    return;
                }
            }
        }
        agent.ResetPath();
        agent.isStopped = true;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = agent.velocity.magnitude;
        if (speed < 0.1f) animator.SetFloat("Speed", 0f);
        else if (currentState == EnemyState.Chase) animator.SetFloat("Speed", 2f);
        else animator.SetFloat("Speed", 1f);
    }

    protected virtual void OnChaseUpdate() { }
}
