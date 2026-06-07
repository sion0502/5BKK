using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
    [SerializeField] protected AudioSource playerFootstepSource;
    [SerializeField] protected CharacterController playerCharacterController;
    [SerializeField] protected Rigidbody playerRigidbody;

    [Header("Debug")]
    [SerializeField] protected bool drawVisionDebug = true;

    [Header("Door")]
    [SerializeField] protected LayerMask doorLayer;
    [SerializeField] protected float doorCheckHeight = 1.0f;
    [SerializeField] protected float patrolDoorFrontCheckDistance = 1.2f;
    [SerializeField] protected float pathDoorCheckRadius = 0.25f;

    [Header("Patrol")]
    [SerializeField] protected float patrolRadius = 20f;
    [SerializeField] protected float patrolReachDistance = 0.5f;

    [Header("Investigate")]
    [SerializeField] protected float investigateRadius = 3f;
    [SerializeField] protected float investigateDuration = 10f;
    [SerializeField] protected float investigateReachDistance = 0.6f;

    [Header("Hearing")]
    [SerializeField] protected float minFootstepMoveSpeed = 0.15f;

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

    private bool investigateRoutineRunning;
    private float nextSenseTime;

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

        if (player == null)
        {
            Debug.LogError($"{name} : Player 태그 오브젝트를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        if (eyePoint == null)
            eyePoint = transform;

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

        UpdateSenses();

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

    protected void UpdateSenses()
    {
        if (Time.time < nextSenseTime)
            return;

        nextSenseTime = Time.time + Mathf.Max(0.02f, data.checkInterval);

        canDetectPlayer = CheckVision();

        if (canDetectPlayer)
        {
            lastKnownPosition = player.position;
            lastDetectTime = Time.time;
            reachedLastKnownPosition = false;

            if (currentState != State.Chase)
                ChangeState(State.Chase);

            return;
        }

        DetectPlayerBySound();
    }

    protected virtual bool CheckVision()
    {
        if (eyePoint == null || player == null)
            return false;

        Vector3 eyePos = eyePoint.position;
        Vector3 targetPos = GetPlayerTargetPosition();
        Vector3 toTarget = targetPos - eyePos;
        float dist = toTarget.magnitude;

        if (dist > data.detectRange)
            return false;

        Vector3 dir = toTarget.normalized;
        float angle = Vector3.Angle(eyePoint.forward, dir);

        if (angle > data.viewAngle * 0.5f)
            return false;

        int mask = data.obstacleLayer.value | data.playerLayer.value;

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            bool seen = IsPlayerTransform(hit.transform);

            if (drawVisionDebug)
            {
                Color c = seen ? Color.green : Color.red;
                Debug.DrawLine(eyePos, hit.point, c, data.checkInterval);
            }

            return seen;
        }

        if (drawVisionDebug)
            Debug.DrawRay(eyePos, dir * dist, Color.yellow, data.checkInterval);

        return false;
    }

    protected virtual void DetectPlayerBySound()
    {
        if (playerFootstepSource == null)
            return;

        if (!playerFootstepSource.isPlaying)
            return;

        if (!IsPlayerActuallyMoving())
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > data.hearingRange)
            return;

        lastKnownPosition = player.position;
        lastDetectTime = Time.time;
        reachedLastKnownPosition = false;

        if (currentState != State.Chase)
            ChangeState(State.Chase);
    }

    protected bool IsPlayerActuallyMoving()
    {
        if (playerCharacterController != null)
        {
            Vector3 v = playerCharacterController.velocity;
            v.y = 0f;
            return v.magnitude > minFootstepMoveSpeed;
        }

        if (playerRigidbody != null)
        {
            Vector3 v = playerRigidbody.linearVelocity;
            v.y = 0f;
            return v.magnitude > minFootstepMoveSpeed;
        }

        return false;
    }

    protected virtual Vector3 GetPlayerTargetPosition()
    {
        Collider c = player.GetComponentInChildren<Collider>();
        if (c != null)
            return c.bounds.center;

        return player.position + Vector3.up * 0.9f;
    }

    protected bool IsPlayerTransform(Transform target)
    {
        if (target == null || player == null)
            return false;

        return target == player || target.IsChildOf(player) || player.IsChildOf(target);
    }

    protected void UpdatePatrol()
    {
        if (isBusy)
            return;

        agent.speed = GetPatrolSpeed();

        if (IsClosedDoorDirectlyAhead(patrolDoorFrontCheckDistance))
        {
            SetRandomPatrolPoint();
            return;
        }

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

        agent.isStopped = false;

        if (canDetectPlayer)
        {
            agent.SetDestination(player.position);
            return;
        }

        float lostTime = Time.time - lastDetectTime;

        if (lostTime < data.targetLostTIme)
        {
            agent.SetDestination(lastKnownPosition);
            return;
        }

        if (!investigateRoutineRunning)
            StartCoroutine(StartInvestigate());
    }

    protected IEnumerator StartInvestigate()
    {
        investigateRoutineRunning = true;
        isBusy = true;

        agent.isStopped = true;
        yield return new WaitForSeconds(0.15f);

        ChangeState(State.Investigate);
        reachedLastKnownPosition = false;
        investigateTimer = investigateDuration;

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;
        agent.SetDestination(lastKnownPosition);

        isBusy = false;
        investigateRoutineRunning = false;
    }

    protected void UpdateInvestigate()
    {
        if (isBusy)
            return;

        agent.speed = GetPatrolSpeed();

        if (IsClosedDoorDirectlyAhead(patrolDoorFrontCheckDistance))
        {
            ChangeState(State.Patrol);
            SetRandomPatrolPoint();
            return;
        }

        if (!reachedLastKnownPosition)
        {
            if (HasClosedDoorBetween(transform.position, lastKnownPosition))
            {
                ChangeState(State.Patrol);
                SetRandomPatrolPoint();
                return;
            }

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

        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
            randomDir.y = 0f;

            Vector3 randomPos = transform.position + randomDir;

            if (!NavMesh.SamplePosition(randomPos, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
                continue;

            if (HasClosedDoorBetween(transform.position, hit.position))
                continue;

            agent.isStopped = false;
            agent.SetDestination(hit.position);
            return;
        }

        agent.isStopped = true;
    }

    protected void SetRandomInvestigatePoint()
    {
        if (!agent.isOnNavMesh)
            return;

        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * investigateRadius;
            randomDir.y = 0f;

            Vector3 randomPos = lastKnownPosition + randomDir;

            if (!NavMesh.SamplePosition(randomPos, out NavMeshHit hit, investigateRadius, NavMesh.AllAreas))
                continue;

            if (HasClosedDoorBetween(transform.position, hit.position))
                continue;

            agent.isStopped = false;
            agent.SetDestination(hit.position);
            return;
        }

        ChangeState(State.Patrol);
        SetRandomPatrolPoint();
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

    protected bool HasClosedDoorBetween(Vector3 from, Vector3 to)
    {
        Vector3 start = from + Vector3.up * doorCheckHeight;
        Vector3 end = to + Vector3.up * 0.2f;
        Vector3 dir = end - start;
        float dist = dir.magnitude;

        if (dist <= 0.1f)
            return false;

        dir.Normalize();

        RaycastHit[] hits = Physics.SphereCastAll(
            start,
            pathDoorCheckRadius,
            dir,
            dist,
            doorLayer,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].collider.GetComponentInParent<DoorClick>();
            if (door == null)
                continue;

            if (!door.IsOpen() && !door.IsBroken())
                return true;
        }

        return false;
    }

    protected bool IsClosedDoorDirectlyAhead(float distance)
    {
        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 dir = transform.forward;

        if (!Physics.Raycast(origin, dir, out RaycastHit hit, distance, doorLayer, QueryTriggerInteraction.Collide))
            return false;

        DoorClick door = hit.collider.GetComponentInParent<DoorClick>();
        if (door == null)
            return false;

        return !door.IsOpen() && !door.IsBroken();
    }

    protected DoorBrokenTest GetClosedDoorOnChasePath(float distance)
    {
        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 dir = GetChaseMoveDirection();

        if (dir.sqrMagnitude <= 0.0001f)
            return null;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            0.25f,
            dir,
            distance,
            doorLayer,
            QueryTriggerInteraction.Collide
        );

        float nearest = float.MaxValue;
        DoorBrokenTest result = null;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick click = hits[i].collider.GetComponentInParent<DoorClick>();
            if (click == null)
                continue;

            if (click.IsOpen() || click.IsBroken())
                continue;

            DoorBrokenTest broken = hits[i].collider.GetComponentInParent<DoorBrokenTest>();
            if (broken == null || broken.IsBroken())
                continue;

            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                result = broken;
            }
        }

        return result;
    }

    protected Vector3 GetChaseMoveDirection()
    {
        if (agent != null && agent.hasPath)
        {
            Vector3 dir = agent.steeringTarget - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                return dir.normalized;
        }

        if (player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                return dir.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
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

        switch (currentState)
        {
            case State.Patrol:
                anim.SetInteger(AnimState, 1);
                break;

            case State.Chase:
                anim.SetInteger(AnimState, 2);
                break;

            case State.Investigate:
                anim.SetInteger(AnimState, 1);
                break;
        }
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
}
