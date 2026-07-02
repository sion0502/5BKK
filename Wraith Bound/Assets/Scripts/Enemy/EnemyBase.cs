using System.Collections;
using System.Reflection;
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
    protected PlayerHidingController playerHidingController;

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
    [SerializeField] protected float chaseDoorSearchRadius = 18f;
    [SerializeField] protected float patrolDoorSearchRadius = 10f;
    [SerializeField] protected float patrolDoorOpenDistance = 0.75f;
    [SerializeField] protected float patrolDoorOpenFaceAngle = 20f;
    [SerializeField] protected bool autoOpenDoorsOnPatrol = true;

    [Header("Patrol")]
    [SerializeField] protected float patrolReachDistance = 0.5f;
    [SerializeField] protected float globalPatrolSampleRadius = 2.0f;
    [SerializeField] protected float minWallClearance = 0.8f;
    [SerializeField] protected float minPatrolPointDistance = 4f;

    [Header("Obstacle Avoidance")]
    [SerializeField] protected float obstacleCheckDistance = 1.2f;
    [SerializeField] protected float obstacleCheckRadius = 0.35f;
    [SerializeField] protected float obstacleStuckTime = 1.0f;
    [SerializeField] protected float obstacleStuckSpeed = 0.08f;
    [SerializeField] protected float obstacleSideStepDistance = 2.0f;
    [SerializeField] protected float obstacleAvoidCooldown = 0.75f;

    [Header("Investigate")]
    [SerializeField] protected float investigateRadius = 5f;
    [SerializeField] protected float investigateReachDistance = 0.6f;
    [SerializeField] protected float investigateStartDistance = 2.2f;

    [Header("Hearing")]
    [SerializeField] protected float minFootstepMoveSpeed = 0.15f;

    [Header("Hiding")]
    [SerializeField] protected float hidingSeenMemoryTime = 0.75f;
    [SerializeField] protected float playerCatchDistance = 1.1f;
    [SerializeField] protected float playerContactKillDistance = 0.15f;

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
    protected float nextSenseLogTime;
    protected float lastVisionDetectTime;
    protected float obstacleStuckTimer;
    protected float nextObstacleAvoidTime;

    protected bool hasPatDestination;
    protected bool isBusy;
    protected bool canDetectPlayer;
    protected bool lockAnimator;
    protected bool reachedLastKnownPosition;
    protected bool investigateRoutineRunning;
    protected bool wasPlayerHiding;
    protected bool playerDeadLogged;
    protected bool hiddenSearchTargetActive;
    protected bool hiddenKillTargetActive;
    protected bool doorSpecialAllowed;

    protected bool lastSawPlayer;
    protected bool lastHeardPlayer;
    protected bool targetLostActive;

    protected Vector3 currentPatrolDestination;
    protected NavMeshTriangulation cachedTriangulation;
    protected Vector3 lastObstacleCheckPosition;

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
        lastVisionDetectTime = -999f;
        lastObstacleCheckPosition = transform.position;
        investigateTimer = 0f;
        reachedLastKnownPosition = false;
        hasPatDestination = false;
        lastSawPlayer = false;
        lastHeardPlayer = false;
        targetLostActive = false;
        wasPlayerHiding = IsPlayerHiding();
        playerDeadLogged = false;
        hiddenSearchTargetActive = false;
        hiddenKillTargetActive = false;
        doorSpecialAllowed = false;

        senseEnableTime = Time.time + senseStartDelay;

        ApplyDataSettings();
        AutoAssignDoorLayer();
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

        CheckPlayerCatchDistance();

        UpdateSenses();
        HandlePlayerHidingState();

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

        UpdateObstacleAvoidance();

        if (!lockAnimator)
            SetAnimatorByState();
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (IsPlayerContact(collision.collider))
            KillPlayer();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (IsPlayerContact(other))
            KillPlayer();
    }

    protected void SetupAgent()
    {
        agent.isStopped = false;
        agent.speed = GetPatrolSpeed();

        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 60);

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

        if (playerHidingController == null)
            playerHidingController = player.GetComponent<PlayerHidingController>();
    }

    protected void UpdateSenses()
    {
        canDetectPlayer = false;
        lastSawPlayer = false;
        lastHeardPlayer = false;

        if (Time.time < senseEnableTime) return;
        if (Time.time < nextSenseTime) return;

        nextSenseTime = Time.time + Mathf.Max(0.02f, data.checkInterval);

        bool playerIsHiding = IsPlayerHiding();
        bool sawPlayer = playerIsHiding ? false : CheckVision();
        bool heardPlayer = playerIsHiding ? false : CheckHearing();

        lastSawPlayer = sawPlayer;
        lastHeardPlayer = heardPlayer;

        if (sawPlayer)
            lastVisionDetectTime = Time.time;

        LogCurrentSenseState(sawPlayer, heardPlayer, playerIsHiding);

        if (!sawPlayer && !heardPlayer)
            return;

        canDetectPlayer = true;
        targetLostActive = false;
        hiddenSearchTargetActive = false;
        doorSpecialAllowed = sawPlayer;

        if (player != null)
            lastKnownPosition = player.position;
        else
            lastKnownPosition = DetectPlayerPosition();

        lastDetectTime = Time.time;

        if (currentState != State.Chase)
            ChangeState(State.Chase);
    }

    protected virtual bool CheckVision()
    {
        if (player == null) return false;
        if (eyePoint == null) return false;

        Vector3 eyePos = eyePoint.position;
        Vector3 targetPos = GetBestVisiblePlayerPosition(eyePos, out bool hasCandidate);
        if (!hasCandidate) return false;

        Vector3 toPlayer = targetPos - eyePos;

        float dist = toPlayer.magnitude;
        if (dist > data.detectRange) return false;
        if (dist <= 0.01f) return false;

        Vector3 dirToPlayer = toPlayer.normalized;
        float angle = Vector3.Angle(eyePoint.forward, dirToPlayer);

        if (angle > viewAngle * 0.5f)
            return false;

        if (IsVisionBlockedByObstacle(eyePos, dirToPlayer, dist, out RaycastHit blockHit))
        {
            if (drawVisionDebug)
                Debug.DrawLine(eyePos, blockHit.point, Color.red, data.checkInterval);

            return false;
        }

        if (drawVisionDebug)
            Debug.DrawLine(eyePos, targetPos, Color.green, data.checkInterval);

        return true;
    }

    protected Vector3 GetBestVisiblePlayerPosition(Vector3 eyePos, out bool hasCandidate)
    {
        hasCandidate = true;

        Vector3 fallback = GetPlayerAimPosition();
        float bestDistance = Vector3.Distance(eyePos, fallback);
        Vector3 bestPosition = fallback;

        if (data == null || data.playerLayer.value == 0)
            return bestPosition;

        Collider[] playerColliders = Physics.OverlapSphere(eyePos, data.detectRange, data.playerLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider col = playerColliders[i];
            if (col == null) continue;

            Vector3 candidate = col.bounds.center;
            float distance = Vector3.Distance(eyePos, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    protected bool IsVisionBlockedByObstacle(Vector3 eyePos, Vector3 dirToPlayer, float distanceToPlayer, out RaycastHit blockingHit)
    {
        blockingHit = default;

        int mask = obstacleLayer.value | doorLayer.value | data.playerLayer.value;

        RaycastHit[] hits = Physics.SphereCastAll(
            eyePos,
            playerDetectRadius,
            dirToPlayer,
            distanceToPlayer,
            mask,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (IsSelfCollider(hitCollider)) continue;

            if (IsPlayerCollider(hitCollider))
                return false;

            blockingHit = hits[i];
            return true;
        }

        return false;
    }

    protected bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;

        Transform hitTransform = col.transform;

        if (player != null && (hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform)))
            return true;

        if (col.CompareTag(playerTag))
            return true;

        return data != null && (data.playerLayer.value & (1 << col.gameObject.layer)) != 0;
    }

    protected bool IsSelfCollider(Collider col)
    {
        if (col == null) return false;

        Transform hitTransform = col.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
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

        if (autoOpenDoorsOnPatrol && TryOpenClosedDoorOnCurrentPath(patrolDoorFrontCheckDistance))
        {
            LogAI("순찰 목적지가 문 너머에 있음 → 닫힌 문 자동 열기");
            return;
        }

        if (IsClosedDoorOnCurrentPath(patrolDoorFrontCheckDistance))
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

        if (autoOpenDoorsOnPatrol && TryMoveTowardClosedDoorToTarget(currentPatrolDestination, patrolDoorSearchRadius))
        {
            LogAI("순찰 목적지 방향의 닫힌 문으로 이동");
            return;
        }

        SetPatrolDestination(currentPatrolDestination);

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

        if (hiddenKillTargetActive)
        {
            if (!IsPlayerHiding())
            {
                hiddenKillTargetActive = false;
                LogAI("대놓고 숨은 플레이어가 숨기 해제 → 즉사 예약 취소");
            }
            else
            {
                float distToHiddenPosition = Vector3.Distance(transform.position, lastKnownPosition);

                if (distToHiddenPosition <= playerCatchDistance)
                {
                    LogAI("대놓고 숨은 위치 도착 → Player Dead");
                    KillPlayer();
                    return;
                }

                SetChaseDestination(lastKnownPosition);
                LogAIThrottled("대놓고 숨은 플레이어 위치까지 추격 중");
                return;
            }
        }

        if (hiddenSearchTargetActive)
        {
            float distToHiddenPosition = Vector3.Distance(transform.position, lastKnownPosition);

            if (distToHiddenPosition <= investigateStartDistance || HasReachedDestination(investigateReachDistance))
            {
                LogAI("플레이어 숨은 위치 도착 → 수색모드 시작");

                if (!investigateRoutineRunning)
                    StartCoroutine(StartInvestigate());

                return;
            }

            SetChaseDestination(lastKnownPosition);
            LogAIThrottled("시야/소리 감지 X → 플레이어가 숨은 마지막 위치까지 추격 중");
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
            float distToPlayerPosition = Vector3.Distance(transform.position, lastKnownPosition);

            if (distToPlayerPosition <= investigateStartDistance || HasReachedDestination(investigateReachDistance))
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

        if (finalDist <= investigateStartDistance || HasReachedDestination(investigateReachDistance))
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
        LogAI("수색모드 시작: 마지막 위치 주변 5m 수색");

        reachedLastKnownPosition = false;
        investigateTimer = data.targetLostTIme;
        hiddenSearchTargetActive = false;

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;

        SetRandomInvestigatePointAround(lastKnownPosition, GetInvestigateRadius());

        isBusy = false;
        investigateRoutineRunning = false;
    }

    protected void UpdateInvestigate()
    {
        if (isBusy) return;

        LogAIThrottled("수색 중");

        agent.speed = GetPatrolSpeed();
        agent.isStopped = false;

        if (IsClosedDoorOnCurrentPath(patrolDoorFrontCheckDistance))
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

            SetRandomInvestigatePointAround(lastKnownPosition, GetInvestigateRadius());
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
        SetRandomInvestigatePointAround(lastKnownPosition, GetInvestigateRadius());
    }

    protected void ReturnToPatrolRoute()
    {
        targetLostActive = false;
        hiddenSearchTargetActive = false;
        hiddenKillTargetActive = false;
        doorSpecialAllowed = false;

        ChangeState(State.Patrol);

        if (hasPatDestination)
        {
            agent.isStopped = false;
            agent.speed = GetPatrolSpeed();
            SetPatrolDestination(currentPatrolDestination);
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

            currentPatrolDestination = point;
            hasPatDestination = true;

            agent.isStopped = false;
            SetPatrolDestination(currentPatrolDestination);
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

    protected bool SetPatrolDestination(Vector3 target)
    {
        if (!agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
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

    protected void UpdateObstacleAvoidance()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (isBusy || agent.isStopped)
        {
            ResetObstacleStuckCheck();
            return;
        }

        if (currentState != State.Patrol && currentState != State.Investigate)
        {
            ResetObstacleStuckCheck();
            return;
        }

        if (!agent.hasPath || agent.pathPending || HasReachedDestination(patrolReachDistance))
        {
            ResetObstacleStuckCheck();
            return;
        }

        float speed = (transform.position - lastObstacleCheckPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        bool stuckBySpeed = speed <= obstacleStuckSpeed;
        bool obstacleAhead = HasObstacleDirectlyAhead();
        bool blockedPath = obstacleAhead || agent.pathStatus != NavMeshPathStatus.PathComplete;

        lastObstacleCheckPosition = transform.position;

        if (!stuckBySpeed || !blockedPath)
        {
            obstacleStuckTimer = 0f;
            return;
        }

        obstacleStuckTimer += Time.deltaTime;

        if (obstacleStuckTimer < obstacleStuckTime)
            return;

        if (Time.time < nextObstacleAvoidTime)
            return;

        nextObstacleAvoidTime = Time.time + obstacleAvoidCooldown;
        obstacleStuckTimer = 0f;

        if (TrySetObstacleSideStepDestination())
        {
            LogAI("장애물에 막힘 → 옆 지점으로 우회");
            return;
        }

        if (currentState == State.Patrol)
        {
            LogAI("장애물에 막힘 → 새 순찰 목적지 선택");
            SetNextGlobalPatDestination();
        }
        else if (currentState == State.Investigate)
        {
            LogAI("장애물에 막힘 → 새 수색 지점 선택");
            SetRandomInvestigatePointAround(lastKnownPosition, GetInvestigateRadius());
        }
    }

    protected void ResetObstacleStuckCheck()
    {
        obstacleStuckTimer = 0f;
        lastObstacleCheckPosition = transform.position;
    }

    protected bool HasObstacleDirectlyAhead()
    {
        int mask = GetObstacleAvoidanceMask();
        if (mask == 0) return false;

        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 direction = GetAgentMoveDirection();

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        return Physics.SphereCast(
            origin,
            obstacleCheckRadius,
            direction.normalized,
            out _,
            obstacleCheckDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    protected int GetObstacleAvoidanceMask()
    {
        int mask = obstacleLayer.value;

        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;

        int obstacleNamedLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleNamedLayer >= 0)
            mask |= 1 << obstacleNamedLayer;

        if (doorLayer.value != 0)
            mask &= ~doorLayer.value;

        if (data != null && data.playerLayer.value != 0)
            mask &= ~data.playerLayer.value;

        return mask;
    }

    protected Vector3 GetAgentMoveDirection()
    {
        if (agent != null && agent.hasPath)
        {
            Vector3 dir = agent.steeringTarget - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    protected bool TrySetObstacleSideStepDestination()
    {
        Vector3 forward = GetAgentMoveDirection();
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.y = 0f;
        forward.Normalize();

        Vector3[] directions =
        {
            Quaternion.Euler(0f, 70f, 0f) * forward,
            Quaternion.Euler(0f, -70f, 0f) * forward,
            Quaternion.Euler(0f, 120f, 0f) * forward,
            Quaternion.Euler(0f, -120f, 0f) * forward,
            -forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = transform.position + directions[i] * obstacleSideStepDistance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, obstacleSideStepDistance, NavMesh.AllAreas))
                continue;

            if (!IsValidDestination(hit.position, minWallClearance * 0.5f))
                continue;

            if (HasClosedDoorBetween(transform.position, hit.position))
                continue;

            if (!SetPatrolDestination(hit.position))
                continue;

            currentPatrolDestination = hit.position;
            hasPatDestination = true;
            return true;
        }

        return false;
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

    protected bool IsClosedDoorOnCurrentPath(float distance)
    {
        return TryGetClosedDoorOnCurrentPath(distance, out _, out _);
    }

    protected bool TryOpenClosedDoorOnCurrentPath(float distance)
    {
        if (!TryGetClosedDoorOnCurrentPath(distance, out DoorClick door, out _))
            return false;

        if (!IsReadyToPushOpenDoor(door))
        {
            MoveToPushDoorPosition(door);
            return true;
        }

        return TryOpenDoorForEnemy(door);
    }

    protected bool TryGetClosedDoorOnCurrentPath(float distance, out DoorClick door, out RaycastHit doorHit)
    {
        door = null;
        doorHit = default;

        if (doorLayer.value == 0)
            return false;

        if (agent == null || !agent.hasPath)
            return false;

        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 dir = GetAgentMoveDirection();

        if (dir.sqrMagnitude <= 0.0001f)
            return false;

        if (!Physics.SphereCast(origin, pathDoorCheckRadius, dir.normalized, out RaycastHit hit, distance, doorLayer, QueryTriggerInteraction.Collide))
            return false;

        door = hit.collider.GetComponentInParent<DoorClick>();
        if (door == null) return false;
        if (door.IsOpen() || door.IsBroken()) return false;
        if (!IsCurrentDestinationBeyondDoor(hit.point, dir.normalized))
            return false;

        doorHit = hit;
        return true;
    }

    protected bool IsCurrentDestinationBeyondDoor(Vector3 doorPoint, Vector3 moveDirection)
    {
        Vector3 target = currentPatrolDestination;

        if (currentState == State.Investigate)
            target = lastKnownPosition;

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.01f)
            return false;

        float targetProjection = Vector3.Dot(toTarget, moveDirection);
        float doorProjection = Vector3.Dot(doorPoint - transform.position, moveDirection);

        return targetProjection > doorProjection + 0.5f;
    }

    protected bool TryOpenDoorForEnemy(DoorClick door)
    {
        if (door == null) return false;

        System.Type doorType = typeof(DoorClick);
        FieldInfo openField = doorType.GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
        if (openField == null) return false;

        MethodInfo playSoundMethod = doorType.GetMethod("PlayDoorSound", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo syncNavMethod = doorType.GetMethod("SyncNavMeshObstacle", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo openRotField = doorType.GetField("openRot", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo doorOpenAngleField = doorType.GetField("DoorOpenAngle", BindingFlags.Instance | BindingFlags.Public);

        if (openRotField != null && doorOpenAngleField != null)
        {
            float openAngle = (float)doorOpenAngleField.GetValue(door);
            Vector3 enemyDir = transform.position - door.transform.position;
            float dot = Vector3.Dot(door.transform.right, enemyDir);
            float angle = dot > 0f ? openAngle : -openAngle;
            Quaternion enemySideOpenRot = Quaternion.Euler(
                door.transform.eulerAngles.x,
                door.transform.eulerAngles.y + angle,
                door.transform.eulerAngles.z
            );

            openRotField.SetValue(door, enemySideOpenRot);
        }

        openField.SetValue(door, true);
        playSoundMethod?.Invoke(door, null);
        syncNavMethod?.Invoke(door, new object[] { true });

        return true;
    }

    protected DoorBrokenTest GetClosedDoorOnChasePath(float distance)
    {
        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 targetDir = GetChaseTargetDirection();

        DoorBrokenTest directTargetDoor = GetClosedBreakableDoorInDirection(origin, targetDir, distance);
        if (directTargetDoor != null)
            return directTargetDoor;

        Vector3 dir = GetChaseMoveDirection();

        DoorBrokenTest moveDirectionDoor = GetClosedBreakableDoorInDirection(origin, dir, distance);
        if (moveDirectionDoor != null)
            return moveDirectionDoor;

        return FindBestClosedBreakableDoorTowardTarget(lastKnownPosition, chaseDoorSearchRadius);
    }

    protected DoorBrokenTest GetClosedBreakableDoorInDirection(Vector3 origin, Vector3 dir, float distance)
    {
        if (doorLayer.value == 0)
            return null;

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

    protected DoorBrokenTest FindBestClosedBreakableDoorTowardTarget(Vector3 target, float searchRadius)
    {
        if (doorLayer.value == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, doorLayer, QueryTriggerInteraction.Collide);
        DoorBrokenTest bestDoor = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick click = hits[i].GetComponentInParent<DoorClick>();
            if (click == null) continue;
            if (click.IsOpen() || click.IsBroken()) continue;

            DoorBrokenTest broken = hits[i].GetComponentInParent<DoorBrokenTest>();
            if (broken == null || broken.IsBroken()) continue;

            Vector3 doorPos = GetDoorWorldPosition(click.transform);
            if (!IsDoorUsefulForTarget(doorPos, target))
                continue;

            float score = Vector3.Distance(transform.position, doorPos) + Vector3.Distance(doorPos, target);
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = broken;
            }
        }

        return bestDoor;
    }

    protected bool TryMoveTowardClosedDoorToTarget(Vector3 target, float searchRadius)
    {
        if (doorLayer.value == 0)
            return false;

        DoorClick door = FindBestClosedDoorTowardTarget(target, searchRadius);
        if (door == null)
            return false;

        Vector3 doorPos = GetDoorWorldPosition(door.transform);
        if (Vector3.Distance(transform.position, doorPos) <= patrolDoorFrontCheckDistance + 0.4f)
        {
            if (!IsReadyToPushOpenDoor(door))
            {
                MoveToPushDoorPosition(door);
                return true;
            }

            return TryOpenDoorForEnemy(door);
        }

        return MoveToPushDoorPosition(door);
    }

    protected bool IsReadyToPushOpenDoor(DoorClick door)
    {
        if (door == null) return false;

        Vector3 doorPos = GetDoorWorldPosition(door.transform);
        Vector3 toDoor = doorPos - transform.position;
        toDoor.y = 0f;

        if (toDoor.magnitude > patrolDoorOpenDistance)
            return false;

        if (toDoor.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(transform.forward, toDoor.normalized);
        return angle <= patrolDoorOpenFaceAngle;
    }

    protected bool MoveToPushDoorPosition(DoorClick door)
    {
        if (door == null) return false;

        Vector3 doorPos = GetDoorWorldPosition(door.transform);
        Vector3 fromDoor = transform.position - doorPos;
        fromDoor.y = 0f;

        if (fromDoor.sqrMagnitude <= 0.001f)
            fromDoor = -GetAgentMoveDirection();

        if (fromDoor.sqrMagnitude <= 0.001f)
            fromDoor = -transform.forward;

        Vector3 pushPosition = doorPos + fromDoor.normalized * Mathf.Max(0.35f, patrolDoorOpenDistance * 0.8f);
        pushPosition.y = transform.position.y;

        if (Vector3.Distance(transform.position, doorPos) <= patrolDoorOpenDistance + 0.25f)
            RotateTowardPoint(doorPos);

        return SetPatrolDestination(pushPosition);
    }

    protected void RotateTowardPoint(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * Time.deltaTime);
    }

    protected DoorClick FindBestClosedDoorTowardTarget(Vector3 target, float searchRadius)
    {
        if (doorLayer.value == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, doorLayer, QueryTriggerInteraction.Collide);
        DoorClick bestDoor = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].GetComponentInParent<DoorClick>();
            if (door == null) continue;
            if (door.IsOpen() || door.IsBroken()) continue;

            Vector3 doorPos = GetDoorWorldPosition(door.transform);
            if (!IsDoorUsefulForTarget(doorPos, target))
                continue;

            float score = Vector3.Distance(transform.position, doorPos) + Vector3.Distance(doorPos, target);
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    protected bool IsDoorUsefulForTarget(Vector3 doorPos, Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        Vector3 toDoor = doorPos - transform.position;

        toTarget.y = 0f;
        toDoor.y = 0f;

        if (toTarget.sqrMagnitude <= 0.01f || toDoor.sqrMagnitude <= 0.01f)
            return false;

        float dot = Vector3.Dot(toTarget.normalized, toDoor.normalized);
        if (dot < 0.15f)
            return false;

        return Vector3.Distance(doorPos, target) < Vector3.Distance(transform.position, target);
    }

    protected Vector3 GetDoorWorldPosition(Transform doorTransform)
    {
        Collider col = doorTransform.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return doorTransform.position;
    }

    protected Vector3 GetChaseTargetDirection()
    {
        Vector3 target = lastKnownPosition;

        if (canDetectPlayer && player != null)
            target = player.position;

        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        return Vector3.zero;
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

    protected float GetInvestigateRadius()
    {
        return Mathf.Max(5f, investigateRadius);
    }

    protected bool IsPlayerHiding()
    {
        return playerHidingController != null && playerHidingController.isHiding;
    }

    protected void HandlePlayerHidingState()
    {
        bool isHiding = IsPlayerHiding();

        if (isHiding && !wasPlayerHiding)
        {
            bool visibleAtHidingMoment = CheckVision();
            bool wasSeenWhileEntering = visibleAtHidingMoment || lastSawPlayer || Time.time - lastVisionDetectTime <= hidingSeenMemoryTime;

            if (wasSeenWhileEntering)
            {
                if (player != null)
                    lastKnownPosition = player.position;

                hiddenKillTargetActive = true;
                hiddenSearchTargetActive = false;
                targetLostActive = false;
                LogAI("플레이어가 시야 안에서 숨음 → 숨은 위치까지 추격 후 사망 처리");

                if (currentState != State.Chase)
                    ChangeState(State.Chase);
            }
            else if (currentState == State.Chase && player != null)
            {
                lastKnownPosition = player.position;
                hiddenSearchTargetActive = true;
                hiddenKillTargetActive = false;
                LogAI("플레이어가 시야 밖에서 숨음 → 마지막 숨은 위치 추적 후 수색");
            }
        }

        wasPlayerHiding = isHiding;
    }

    protected void CheckPlayerCatchDistance()
    {
        if (playerDeadLogged) return;
        if (player == null) return;

        float distance = GetDistanceToPlayerCollider();

        if (distance <= Mathf.Max(0.01f, playerContactKillDistance))
        {
            LogAI($"플레이어와 직접 접촉({distance:F2}m) → Player Dead");
            KillPlayer();
            return;
        }

        if (currentState != State.Chase) return;

        float catchDistance = Mathf.Max(0.1f, playerCatchDistance);

        if (distance > catchDistance)
            return;

        LogAI($"플레이어와 접촉 거리 도달({distance:F2}m) → Player Dead");
        KillPlayer();
    }

    protected float GetDistanceToPlayerCollider()
    {
        float bestDistance = Vector3.Distance(transform.position, player.position);

        Collider[] enemyColliders = GetComponentsInChildren<Collider>();
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

        if (enemyColliders == null || playerColliders == null || enemyColliders.Length == 0 || playerColliders.Length == 0)
            return bestDistance;

        for (int i = 0; i < enemyColliders.Length; i++)
        {
            Collider enemyCol = enemyColliders[i];
            if (enemyCol == null || !enemyCol.enabled) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCol = playerColliders[j];
                if (playerCol == null || !playerCol.enabled) continue;

                Vector3 pointOnEnemy = enemyCol.ClosestPoint(playerCol.bounds.center);
                Vector3 pointOnPlayer = playerCol.ClosestPoint(pointOnEnemy);
                float distance = Vector3.Distance(pointOnEnemy, pointOnPlayer);

                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        return bestDistance;
    }

    protected bool IsPlayerContact(Collider other)
    {
        if (other == null)
            return false;

        Transform hitTransform = other.transform;

        if (player != null && (hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform)))
            return true;

        if (other.CompareTag(playerTag))
            return true;

        if (data != null && (data.playerLayer.value & (1 << other.gameObject.layer)) != 0)
            return true;

        return false;
    }

    protected void KillPlayer()
    {
        if (playerDeadLogged) return;

        playerDeadLogged = true;
        DeathEndingUI.ShowDeathEnding("Player Dead");
    }

    protected void LogCurrentSenseState(bool sawPlayer, bool heardPlayer, bool playerIsHiding)
    {
        string senseState;

        if (sawPlayer && heardPlayer)
            senseState = "시야 + 소리 둘 다 감지";
        else if (sawPlayer)
            senseState = "시야만 감지";
        else if (heardPlayer)
            senseState = "소리만 감지";
        else if (playerIsHiding)
            senseState = "플레이어 숨음 상태 → 시야/소리 감지 X";
        else
            senseState = "시야/소리 감지 X";

        LogSenseThrottled($"{senseState} | AI 상태: {currentState}");
    }

    protected void ApplyDataSettings()
    {
        if (data == null) return;

        viewAngle = data.viewAngle;

        if (data.obstacleLayer.value != 0)
            obstacleLayer = data.obstacleLayer;
    }

    protected void AutoAssignDoorLayer()
    {
        if (doorLayer.value != 0) return;

        int layer = LayerMask.NameToLayer("Door");
        if (layer < 0) return;

        doorLayer = 1 << layer;
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
        if (Time.time < nextSenseLogTime) return;

        nextSenseLogTime = Time.time + logInterval;
        Debug.Log($"[{name}] {message}");
    }

    protected virtual void HandleChaseSpecial()
    {
    }
}
