using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public abstract class EnemyBase : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Chase,
        Investigate
    }

    protected static readonly int AnimState = Animator.StringToHash("State");
    protected static readonly int AnimAttack = Animator.StringToHash("Attack");

    [Header("References")]
    [SerializeField] protected Monsters data;
    [SerializeField] protected Transform player;
    [SerializeField] protected Transform eyePoint;

    [Header("Debug")]
    [SerializeField] protected bool drawVisionDebug = true;

    [Header("Patrol")]
    [SerializeField] protected float patrolRadius = 20f;
    [SerializeField] protected float patrolReachDistance = 0.5f;

    [Header("Chase")]
    [SerializeField] protected float lostChaseDuration = 15f;

    [Header("Investigate")]
    [SerializeField] protected float investigateRadius = 3f;
    [SerializeField] protected float investigateDuration = 10f;
    [SerializeField] protected float investigateReachDistance = 0.6f;

    protected NavMeshAgent agent;
    protected Animator anim;

    protected State currentState;

    protected Vector3 lastKnownPosition;
    protected float lastDetectTime;
    protected float investigateTimer;

    protected bool isBusy;
    protected bool canDetectPlayer;
    protected bool lockAnimator;
    protected bool reachedLastKnownPosition;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        if (agent == null)
        {
            Debug.LogError($"{name} : NavMeshAgent가 없습니다.");
            enabled = false;
            return;
        }

        if (anim == null)
        {
            Debug.LogError($"{name} : Animator가 없습니다.");
            enabled = false;
            return;
        }

        if (data == null)
        {
            Debug.LogError($"{name} : Monsters 데이터가 없습니다.");
            enabled = false;
            return;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
                player = playerObj.transform;
        }

        currentState = State.Patrol;
        lastKnownPosition = transform.position;
        lastDetectTime = -999f;
        investigateTimer = 0f;
        reachedLastKnownPosition = false;

        agent.isStopped = false;
        agent.speed = GetPatrolSpeed();

        SetAnimatorByState();
        SetRandomPatrolPoint();
    }

    protected virtual void Update()
    {
        if (player == null)
            return;

        if (!agent.isOnNavMesh)
            return;

        canDetectPlayer = DetectPlayer();

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Chase:
                UpdateChase();
                break;

            case State.Investigate:
                UpdateInvestigate();
                break;
        }

        if (!lockAnimator)
            SetAnimatorByState();
    }

    protected bool DetectPlayer()
    {
        bool canSee = CheckVision();

        if (canSee)
        {
            lastDetectTime = Time.time;
            lastKnownPosition = player.position;
            reachedLastKnownPosition = false;

            if (currentState != State.Chase)
                ChangeState(State.Chase);

            return true;
        }

        return false;
    }

    protected bool CheckVision()
    {
        if (eyePoint == null)
            return false;

        Vector3 targetPos = player.position + Vector3.up * 0.8f;
        Vector3 dir = targetPos - eyePoint.position;

        float dist = dir.magnitude;

        if (dist > data.detectRange)
            return false;

        float angle = Vector3.Angle(eyePoint.forward, dir);

        if (angle > data.viewAngle * 0.5f)
            return false;

        dir.Normalize();

        if (drawVisionDebug)
            Debug.DrawRay(eyePoint.position, dir * dist, Color.red);

        int obstacleMask = data.obstacleLayer.value;
        int playerMask = data.playerLayer.value;

        if (obstacleMask != 0)
        {
            if (Physics.Raycast(
                eyePoint.position,
                dir,
                out RaycastHit obstacleHit,
                dist,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                if (drawVisionDebug)
                    Debug.DrawLine(eyePoint.position, obstacleHit.point, Color.blue);

                return false;
            }
        }

        if (playerMask != 0)
        {
            if (Physics.Raycast(
                eyePoint.position,
                dir,
                out RaycastHit playerHit,
                dist,
                playerMask,
                QueryTriggerInteraction.Ignore))
            {
                if (playerHit.transform == player || playerHit.transform.IsChildOf(player))
                {
                    if (drawVisionDebug)
                        Debug.DrawLine(eyePoint.position, playerHit.point, Color.green);

                    return true;
                }
            }
        }

        return false;
    }

    protected bool CheckHearing()
    {
        return false;
    }

    protected void UpdatePatrol()
    {
        if (isBusy)
            return;

        agent.speed = GetPatrolSpeed();

        if (!HasReachedDestination(patrolReachDistance))
            return;

        SetRandomPatrolPoint();
    }

    protected void UpdateChase()
    {
        if (isBusy)
            return;

        agent.speed = GetChaseSpeed();

        HandleChaseSpecial();

        if (isBusy)
            return;

        if (canDetectPlayer)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            return;
        }

        float lostTime = Time.time - lastDetectTime;

        if (lostTime < lostChaseDuration)
        {
            agent.isStopped = false;
            agent.SetDestination(lastKnownPosition);
            return;
        }

        StartCoroutine(StartInvestigate());
    }

    protected IEnumerator StartInvestigate()
    {
        isBusy = true;

        agent.isStopped = true;

        yield return new WaitForSeconds(0.2f);

        ChangeState(State.Investigate);

        reachedLastKnownPosition = false;
        investigateTimer = investigateDuration;

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;
        agent.SetDestination(lastKnownPosition);

        isBusy = false;
    }

    protected void UpdateInvestigate()
    {
        if (isBusy)
            return;

        agent.speed = GetPatrolSpeed();

        if (!reachedLastKnownPosition)
        {
            agent.SetDestination(lastKnownPosition);

            if (!HasReachedDestination(investigateReachDistance))
                return;

            reachedLastKnownPosition = true;
            investigateTimer = investigateDuration;
            SetRandomInvestigatePoint();
            return;
        }

        investigateTimer -= Time.deltaTime;

        if (investigateTimer <= 0f)
        {
            ChangeState(State.Patrol);
            SetRandomPatrolPoint();
            return;
        }

        if (!HasReachedDestination(investigateReachDistance))
            return;

        SetRandomInvestigatePoint();
    }

    protected void SetRandomPatrolPoint()
    {
        if (!agent.isOnNavMesh)
            return;

        agent.speed = GetPatrolSpeed();

        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
            randomDir.y = 0f;

            Vector3 randomPos = transform.position + randomDir;

            if (NavMesh.SamplePosition(
                randomPos,
                out NavMeshHit hit,
                patrolRadius,
                NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    protected void SetRandomInvestigatePoint()
    {
        if (!agent.isOnNavMesh)
            return;

        agent.speed = GetPatrolSpeed();

        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * investigateRadius;
            randomDir.y = 0f;

            Vector3 randomPos = lastKnownPosition + randomDir;

            if (NavMesh.SamplePosition(
                randomPos,
                out NavMeshHit hit,
                investigateRadius,
                NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    protected bool HasReachedDestination(float extraDistance)
    {
        if (agent.pathPending)
            return false;

        if (!agent.hasPath)
            return true;

        float reachDistance = agent.stoppingDistance + extraDistance;

        return agent.remainingDistance <= reachDistance;
    }

    protected void ChangeState(State nextState)
    {
        if (currentState == nextState)
            return;

        currentState = nextState;

        switch (currentState)
        {
            case State.Patrol:
                agent.speed = GetPatrolSpeed();
                break;

            case State.Chase:
                agent.speed = GetChaseSpeed();
                break;

            case State.Investigate:
                agent.speed = GetPatrolSpeed();
                break;
        }

        if (!lockAnimator)
            SetAnimatorByState();
    }

    protected void SetAnimatorByState()
    {
        if (anim == null)
            return;

        int value = 0;

        switch (currentState)
        {
            case State.Patrol:
                value = 1;
                break;

            case State.Chase:
                value = 2;
                break;

            case State.Investigate:
                value = 1;
                break;
        }

        anim.SetInteger(AnimState, value);
    }

    protected float GetPatrolSpeed()
    {
        return data.moveSpeed * 0.5f;
    }

    protected float GetChaseSpeed()
    {
        return data.moveSpeed;
    }

    protected virtual void HandleChaseSpecial()
    {
    }

    protected virtual void OnGUI()
    {
        if (anim == null)
            return;

        GUI.Label(
            new Rect(10, 10, 900, 30),
            $"{name} : {currentState} / Detect : {canDetectPlayer} / AnimState : {anim.GetInteger(AnimState)} / Speed : {agent.speed}"
        );
    }
}
