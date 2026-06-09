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
    [SerializeField] protected Transform eyePoint;

    [Header("Runtime Player Auto Find")]
    protected string playerTag = "Player";

    protected Transform player;
    protected AudioSource playerFootstepSource;
    protected CharacterController playerCharacterController;
    protected Rigidbody playerRigidbody;

    [Header("Debug")]
    [SerializeField] protected bool drawVisionDebug = true;
    [SerializeField] protected bool debugStateLog = true;
    [SerializeField] protected bool debugSenseLog = true;

    [Header("Vision")]
    [SerializeField] protected float viewAngle = 90f;
    [SerializeField] protected LayerMask obstacleLayer;
    [SerializeField] protected float playerDetectRadius = 0.35f;

    [Header("Door")]
    [SerializeField] protected LayerMask doorLayer;
    [SerializeField] protected float doorCheckHeight = 1.0f;
    [SerializeField] protected float patrolDoorFrontCheckDistance = 1.2f;
    [SerializeField] protected float pathDoorCheckRadius = 0.25f;
    [SerializeField] protected float chaseDoorDetectDistance = 2.2f;

    [Header("Patrol")]
    [SerializeField] protected float patrolReachDistance = 0.5f;
    [SerializeField] protected float globalPatrolSampleRadius = 2.0f;
    [SerializeField] protected float minWallClearance = 0.8f;
    [SerializeField] protected float minPatrolPointDistance = 4f;

    [Header("Investigate")]
    [SerializeField] protected float investigateRadius = 3f;
    [SerializeField] protected float investigateReachDistance = 0.6f;
    [SerializeField] protected float investigateStartDistance = 2.2f;

    [Header("Hearing")]
    [SerializeField] protected float minFootstepMoveSpeed = 0.15f;

    [Header("Sense Timing")]
    [SerializeField] protected float senseStartDelay = 0.5f;

    [Header("Chase Optimization")]
    [SerializeField] protected float chaseDestinationUpdateInterval = 0.15f;
    [SerializeField] protected float chaseRepathDistance = 0.35f;

    [Header("Log Timing")]
    [SerializeField] protected float logInterval = 0.5f;

    protected NavMeshAgent agent;
    protected Animator anim;

    protected State currentState;
    protected Vector3 lastKnownPosition;
    protected Vector3 lastChaseDestination;

    protected float lastDetectTime;
    protected float investigateTimer;
    protected float nextSenseTime;
    protected float senseEnableTime;
    protected float nextChaseRepathTime;
    protected float nextLogTime;

    protected bool hasPatDestination;
    protected bool isBusy;
    protected bool canDetectPlayer;
    protected bool lockAnimator;
    protected bool reachedLastKnownPosition;
    protected bool investigateRoutineRunning;

    protected bool lastSawPlayer;
    protected bool lastHeardPlayer;
    protected bool targetLostActive;

    protected Vector3 currentPatrolDestination;
    protected NavMeshTriangulation cachedTriangulation;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (eyePoint == null)
            eyePoint = transform;
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

        AutoFindPlayerReferences();

        currentState = State.Patrol;
        lastKnownPosition = transform.position;
        lastChaseDestination = transform.position;
        lastDetectTime = -999f;
        investigateTimer = 0f;
        reachedLastKnownPosition = false;
        hasPatDestination = false;
        lastSawPlayer = false;
        lastHeardPlayer = false;
        targetLostActive = false;

        senseEnableTime = Time.time + senseStartDelay;

        SetupAgent();
        CacheTriangulation();
        SetAnimatorByState();
        SetNextGlobalPatDestination();

        LogAI("초기 상태: Patrol");
    }

    protected virtual void Update()
    {
        if (eyePoint == null) return;
        if (agent == null) return;
        if (!agent.isOnNavMesh) return;

        if (player == null)
            AutoFindPlayerReferences();

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

    protected void SetupAgent()
    {
        agent.isStopped = false;
        agent.speed = GetPatrolSpeed();

        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 60);

        if (agent.radius < 0.35f)
            agent.radius = 0.35f;
    }

    protected void AutoFindPlayerReferences()
    {
        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }

        if (player == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 200f, data.playerLayer, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0)
                player = hits[0].transform;
        }

        if (player == null)
            return;

        if (playerCharacterController == null)
            playerCharacterController = player.GetComponent<CharacterController>();

        if (playerRigidbody == null)
            playerRigidbody = player.GetComponent<Rigidbody>();

        if (playerFootstepSource == null)
            playerFootstepSource = player.GetComponentInChildren<AudioSource>();
    }

    protected void UpdateSenses()
    {
        canDetectPlayer = false;
        lastSawPlayer = false;
        lastHeardPlayer = false;

        if (Time.time < senseEnableTime) return;
        if (Time.time < nextSenseTime) return;

        nextSenseTime = Time.time + Mathf.Max(0.02f, data.checkInterval);

        bool sawPlayer = CheckVision();
        bool heardPlayer = CheckHearing();

        lastSawPlayer = sawPlayer;
        lastHeardPlayer = heardPlayer;

        if (!sawPlayer && !heardPlayer)
            return;

        canDetectPlayer = true;
        targetLostActive = false;

        if (player != null)
            lastKnownPosition = player.position;
        else
            lastKnownPosition = DetectPlayerPosition();

        lastDetectTime = Time.time;

        if (sawPlayer && heardPlayer)
            LogSense("시야 + 소리 둘 다 감지");
        else if (sawPlayer)
            LogSense("시야만 감지");
        else if (heardPlayer)
            LogSense("소리만 감지");

        if (currentState != State.Chase)
            ChangeState(State.Chase);
    }

    protected virtual bool CheckVision()
    {
        if (player == null) return false;
        if (eyePoint == null) return false;

        Vector3 eyePos = eyePoint.position;
        Vector3 targetPos = GetPlayerAimPosition();
        Vector3 toPlayer = targetPos - eyePos;

        float dist = toPlayer.magnitude;
        if (dist > data.detectRange) return false;
        if (dist <= 0.01f) return false;

        Vector3 dirToPlayer = toPlayer.normalized;
        float angle = Vector3.Angle(eyePoint.forward, dirToPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        int blockMask = obstacleLayer.value | doorLayer.value;

        if (Physics.SphereCast(eyePos, playerDetectRadius, dirToPlayer, out RaycastHit blockHit, dist, blockMask, QueryTriggerInteraction.Ignore))
        {
            if (drawVisionDebug)
                Debug.DrawLine(eyePos, blockHit.point, Color.red, data.checkInterval);

            return false;
        }

        if (drawVisionDebug)
            Debug.DrawLine(eyePos, targetPos, Color.green, data.checkInterval);

        return true;
    }

    protected Vector3 GetPlayerAimPosition()
    {
        if (playerCharacterController != null)
            return player.transform.position + Vector3.up * Mathf.Max(0.8f, playerCharacterController.height * 0.5f);

        return player.position + Vector3.up * 1.0f;
    }

    protected virtual bool CheckHearing()
    {
        if (player == null) return false;
        if (playerFootstepSource == null) return false;
        if (!playerFootstepSource.isPlaying) return false;
        if (!IsPlayerActuallyMoving()) return false;

        float dist = Vector3.Distance(transform.position, player.position);
        return dist <= data.hearingRange;
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

    protected virtual Vector3 DetectPlayerPosition()
    {
        if (player != null)
            return player.position;

        return lastKnownPosition;
    }

    protected void UpdatePatrol()
    {
        if (isBusy) return;

        LogAIThrottled("순찰 중");

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;

        if (IsClosedDoorDirectlyAhead(patrolDoorFrontCheckDistance))
        {
            LogAI("순찰 중 닫힌 문 감지 → 새 순찰 목적지 선택");
            SetNextGlobalPatDestination();
            return;
        }

        if (!hasPatDestination)
        {
            LogAI("순찰 목적지 없음 → 새 순찰 목적지 선택");
            SetNextGlobalPatDestination();
            return;
        }

        SafeSetDestination(currentPatrolDestination);

        if (!HasReachedDestination(patrolReachDistance))
            return;

        LogAI("순찰 목적지 도착 → 새 순찰 목적지 선택");
        SetNextGlobalPatDestination();
    }

    protected void UpdateChase()
    {
        if (isBusy) return;

        LogAIThrottled("추적 중");

        agent.speed = GetChaseSpeed();

        HandleChaseSpecial();

        if (isBusy) return;

        agent.isStopped = false;

        if (canDetectPlayer)
        {
            if (player != null)
                lastKnownPosition = player.position;

            SetChaseDestination(lastKnownPosition);

            if (lastSawPlayer && lastHeardPlayer)
                LogSenseThrottled("시야 + 소리 둘 다 감지 CHASE 중");
            else if (lastSawPlayer)
                LogSenseThrottled("시야만 감지 CHASE 중");
            else if (lastHeardPlayer)
                LogSenseThrottled("소리만 감지 CHASE 중");

            return;
        }

        float lostTime = Time.time - lastDetectTime;

        if (lostTime < data.targetLostTIme)
        {
            if (!targetLostActive)
            {
                targetLostActive = true;
                LogAI("시야/소리 감지 X → targetLostTime 발동");
            }

            if (player != null)
                lastKnownPosition = player.position;

            float distToPlayerPosition = Vector3.Distance(transform.position, lastKnownPosition);

            if (distToPlayerPosition <= investigateStartDistance)
            {
                LogAI("targetLostTime 중 플레이어 위치 도달 → 수색 시작");

                if (!investigateRoutineRunning)
                    StartCoroutine(StartInvestigate());

                return;
            }

            SetChaseDestination(lastKnownPosition);

            LogAIThrottled($"시야/소리 감지 X → targetLostTime 진행 중, 남은 시간 {data.targetLostTIme - lostTime:F1}초");
            return;
        }

        float finalDist = Vector3.Distance(transform.position, lastKnownPosition);

        if (finalDist <= investigateStartDistance)
        {
            LogAI("targetLostTime 종료 시 플레이어 위치 도달 → 수색 시작");

            if (!investigateRoutineRunning)
                StartCoroutine(StartInvestigate());

            return;
        }

        LogAI("targetLostTime 종료 + 플레이어 위치 도달 실패 → 순찰 복귀");
        ReturnToPatrolRoute();
    }

    protected void SetChaseDestination(Vector3 target)
    {
        if (Time.time < nextChaseRepathTime)
            return;

        if (Vector3.Distance(lastChaseDestination, target) < chaseRepathDistance)
            return;

        nextChaseRepathTime = Time.time + chaseDestinationUpdateInterval;
        lastChaseDestination = target;

        SafeSetDestination(target);
    }

    protected IEnumerator StartInvestigate()
    {
        investigateRoutineRunning = true;
        isBusy = true;

        agent.isStopped = true;
        agent.ResetPath();

        yield return new WaitForSeconds(0.15f);

        ChangeState(State.Investigate);

        reachedLastKnownPosition = false;
        investigateTimer = data.targetLostTIme;

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;

        SetRandomInvestigatePointAround(lastKnownPosition, investigateRadius);

        isBusy = false;
        investigateRoutineRunning = false;
    }

    protected void UpdateInvestigate()
    {
        if (isBusy) return;

        LogAIThrottled("수색 중");

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;

        if (IsClosedDoorDirectlyAhead(patrolDoorFrontCheckDistance))
        {
            LogAI("수색 중 닫힌 문 감지 → 순찰 복귀");
            ReturnToPatrolRoute();
            return;
        }

        if (!reachedLastKnownPosition)
        {
            if (HasClosedDoorBetween(transform.position, lastKnownPosition))
            {
                LogAI("수색 위치까지 닫힌 문 존재 → 순찰 복귀");
                ReturnToPatrolRoute();
                return;
            }

            SafeSetDestination(lastKnownPosition);

            if (!HasReachedDestination(investigateReachDistance))
                return;

            reachedLastKnownPosition = true;
            investigateTimer = data.targetLostTIme;

            LogAI("마지막 위치 도착 완료 → 주변 랜덤 수색 시작");

            SetRandomInvestigatePointAround(lastKnownPosition, investigateRadius);
            return;
        }

        investigateTimer -= Time.deltaTime;

        if (investigateTimer <= 0f)
        {
            LogAI("수색 실패 → 순찰 복귀");
            ReturnToPatrolRoute();
            return;
        }

        if (!HasReachedDestination(investigateReachDistance))
            return;

        LogAI($"수색 중 → 남은 시간 {investigateTimer:F1}초, 다음 수색 지점 선택");
        SetRandomInvestigatePointAround(lastKnownPosition, investigateRadius);
    }

    protected void ReturnToPatrolRoute()
    {
        targetLostActive = false;

        ChangeState(State.Patrol);

        if (hasPatDestination)
        {
            agent.isStopped = false;
            agent.speed = GetPatrolSpeed();
            SafeSetDestination(currentPatrolDestination);
        }
        else
        {
            SetNextGlobalPatDestination();
        }
    }

    protected void SetNextGlobalPatDestination()
    {
        if (!agent.isOnNavMesh) return;

        CacheTriangulation();

        for (int i = 0; i < 80; i++)
        {
            if (!TryGetRandomGlobalNavMeshPoint(out Vector3 point))
                continue;

            if (!IsValidDestination(point, minWallClearance))
                continue;

            if (Vector3.Distance(transform.position, point) < minPatrolPointDistance)
                continue;

            if (HasClosedDoorBetween(transform.position, point))
                continue;

            if (!HasCompletePath(point))
                continue;

            currentPatrolDestination = point;
            hasPatDestination = true;

            agent.isStopped = false;
            SafeSetDestination(currentPatrolDestination);
            return;
        }

        hasPatDestination = false;
        agent.isStopped = true;
    }

    protected void SetRandomInvestigatePointAround(Vector3 center, float radius)
    {
        if (!agent.isOnNavMesh) return;

        if (radius <= 0f)
            radius = 0.05f;

        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * radius;
            randomDir.y = 0f;

            Vector3 pos = center + randomDir;

            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, radius, NavMesh.AllAreas))
                continue;

            if (!IsValidDestination(hit.position, minWallClearance * 0.5f))
                continue;

            if (HasClosedDoorBetween(transform.position, hit.position))
                continue;

            if (!HasCompletePath(hit.position))
                continue;

            SafeSetDestination(hit.position);
            currentPatrolDestination = hit.position;
            hasPatDestination = true;
            return;
        }

        SafeSetDestination(center);
        currentPatrolDestination = center;
        hasPatDestination = false;
    }

    protected bool IsValidDestination(Vector3 point, float clearance)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit sampleHit, 1.0f, NavMesh.AllAreas))
            return false;

        if (NavMesh.FindClosestEdge(sampleHit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
        {
            if (edgeHit.distance < clearance)
                return false;
        }

        return true;
    }

    protected bool HasCompletePath(Vector3 target)
    {
        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(target, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    protected bool SafeSetDestination(Vector3 target)
    {
        if (!agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;

        if (!HasCompletePath(hit.position))
            return false;

        agent.SetDestination(hit.position);
        return true;
    }

    protected void CacheTriangulation()
    {
        cachedTriangulation = NavMesh.CalculateTriangulation();
    }

    protected bool TryGetRandomGlobalNavMeshPoint(out Vector3 result)
    {
        result = transform.position;

        if (cachedTriangulation.vertices == null || cachedTriangulation.vertices.Length == 0)
            return false;

        for (int i = 0; i < 30; i++)
        {
            int idx = Random.Range(0, cachedTriangulation.vertices.Length);
            Vector3 basePoint = cachedTriangulation.vertices[idx];

            Vector3 randomOffset = new Vector3(
                Random.Range(-globalPatrolSampleRadius, globalPatrolSampleRadius),
                0f,
                Random.Range(-globalPatrolSampleRadius, globalPatrolSampleRadius)
            );

            Vector3 samplePoint = basePoint + randomOffset;

            if (NavMesh.SamplePosition(samplePoint, out NavMeshHit hit, globalPatrolSampleRadius + 1f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        return false;
    }

    protected bool HasReachedDestination(float extraDistance)
    {
        if (agent.pathPending) return false;
        if (!agent.hasPath) return true;

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
            if (door == null) continue;

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
        if (door == null) return false;

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
            if (click == null) continue;
            if (click.IsOpen() || click.IsBroken()) continue;

            DoorBrokenTest broken = hits[i].collider.GetComponentInParent<DoorBrokenTest>();
            if (broken == null || broken.IsBroken()) continue;

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

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        if (player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
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

        State prevState = currentState;
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

        LogAI($"상태 변경: {prevState} → {currentState}");

        if (!lockAnimator)
            SetAnimatorByState();
    }

    protected void SetAnimatorByState()
    {
        if (anim == null) return;

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

    protected void LogAI(string message)
    {
        if (!debugStateLog) return;
        Debug.Log($"[{name}] {message}");
    }

    protected void LogSense(string message)
    {
        if (!debugSenseLog) return;
        Debug.Log($"[{name}] {message}");
    }

    protected void LogAIThrottled(string message)
    {
        if (!debugStateLog) return;
        if (Time.time < nextLogTime) return;

        nextLogTime = Time.time + logInterval;
        Debug.Log($"[{name}] {message}");
    }

    protected void LogSenseThrottled(string message)
    {
        if (!debugSenseLog) return;
        if (Time.time < nextLogTime) return;

        nextLogTime = Time.time + logInterval;
        Debug.Log($"[{name}] {message}");
    }

    protected virtual void HandleChaseSpecial()
    {
    }
}
