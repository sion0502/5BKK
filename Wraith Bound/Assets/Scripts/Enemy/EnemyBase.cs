using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 AI 진입점.
/// EnemyState / EnemySense / EnemyCombat / NavMotor / EnemyDoorUtility + Patrol/
/// </summary>
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
    [SerializeField] protected float patrolDoorPassThroughDistance = 1.5f;
    [SerializeField] protected bool autoOpenDoorsOnPatrol = true;

    [Header("Patrol")]
    [SerializeField] protected PatrolPointZone patrolPointZone;
    [SerializeField] protected float patrolReachDistance = 0.5f;
    [SerializeField] protected float globalPatrolSampleRadius = 2.0f;
    [SerializeField] protected float minWallClearance = 0.8f;
    [SerializeField] protected int patrolDestinationPickAttempts = 100;

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
    [SerializeField] protected float footstepHearingMemory = 0.85f;

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
    protected Transform player;

    protected string playerTag = "Player";
    protected AudioSource playerFootstepSource;
    protected CharacterController playerCharacterController;
    protected Rigidbody playerRigidbody;
    protected PlayerHidingController playerHidingController;

    readonly EnemyState _state = new EnemyState();
    NavMotor _navMotor;
    PatrolBehavior _patrol;

    internal EnemyState RuntimeState => _state;
    internal EnemySense Sense { get; private set; }
    internal EnemyCombat Combat { get; private set; }
    internal NavMotor NavMotor => _navMotor;
    internal PatrolBehavior Patrol => _patrol;
    internal NavMeshAgent Agent => agent;
    internal Animator Anim => anim;
    internal Monsters Data => data;
    internal Transform EyePoint => eyePoint;
    internal int AnimStateHash => AnimState;

    internal float ViewAngle => viewAngle;
    internal float EyeFrontArcAngle => data != null ? data.eyeFrontArcAngle : 180f;
    internal LayerMask ObstacleLayer => obstacleLayer;
    internal LayerMask DoorLayer => doorLayer;
    internal float PlayerDetectRadius => playerDetectRadius;
    internal float DoorCheckHeight => doorCheckHeight;
    internal float PathDoorCheckRadius => pathDoorCheckRadius;
    internal float PatrolDoorFrontCheckDistance => patrolDoorFrontCheckDistance;
    internal float ChaseDoorSearchRadius => chaseDoorSearchRadius;
    internal float MinFootstepMoveSpeed => minFootstepMoveSpeed;
    internal float FootstepHearingMemory => footstepHearingMemory;
    internal float HidingSeenMemoryTime => hidingSeenMemoryTime;
    internal float PlayerCatchDistance => playerCatchDistance;
    internal float PlayerContactKillDistance => playerContactKillDistance;
    internal float InvestigateRadius => investigateRadius;
    internal float InvestigateReachDistance => investigateReachDistance;
    internal float InvestigateStartDistance => investigateStartDistance;
    internal float PatrolReachDistance => patrolReachDistance;
    internal float MinWallClearance => minWallClearance;

    internal Transform Player
    {
        get => player;
        set => player = value;
    }

    internal string PlayerTag => playerTag;

    internal CharacterController PlayerCharacterController
    {
        get => playerCharacterController;
        set => playerCharacterController = value;
    }

    internal Rigidbody PlayerRigidbody
    {
        get => playerRigidbody;
        set => playerRigidbody = value;
    }

    internal AudioSource PlayerFootstepSource
    {
        get => playerFootstepSource;
        set => playerFootstepSource = value;
    }

    internal PlayerHidingController PlayerHidingController
    {
        get => playerHidingController;
        set => playerHidingController = value;
    }
    internal float ChaseDestinationUpdateInterval => chaseDestinationUpdateInterval;
    internal float ChaseRepathDistance => chaseRepathDistance;
    internal float ObstacleCheckDistance => obstacleCheckDistance;
    internal float ObstacleCheckRadius => obstacleCheckRadius;
    internal float ObstacleStuckTime => obstacleStuckTime;
    internal float ObstacleStuckSpeed => obstacleStuckSpeed;
    internal float ObstacleSideStepDistance => obstacleSideStepDistance;
    internal float ObstacleAvoidCooldown => obstacleAvoidCooldown;
    internal bool DrawVisionDebug => drawVisionDebug;

    protected State currentState
    {
        get => _state.CurrentState;
        set => _state.CurrentState = value;
    }

    protected bool isBusy
    {
        get => _state.IsBusy;
        set => _state.IsBusy = value;
    }

    protected bool canDetectPlayer
    {
        get => _state.CanDetectPlayer;
        set => _state.CanDetectPlayer = value;
    }

    protected bool lockAnimator
    {
        get => _state.LockAnimator;
        set => _state.LockAnimator = value;
    }

    protected bool doorSpecialAllowed
    {
        get => _state.DoorSpecialAllowed;
        set => _state.DoorSpecialAllowed = value;
    }

    protected Vector3 lastKnownPosition
    {
        get => _state.LastKnownPosition;
        set => _state.LastKnownPosition = value;
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();

        if (eyePoint == null)
            eyePoint = transform;

        AutoAssignDoorLayer();
    }

    protected virtual void Start()
    {
        if (!ValidateComponents())
            return;

        ResolvePatrolPointZone();
        InitState();
        ApplyDataSettings();
        AutoAssignDoorLayer();
        SetupAgent();
        InitPatrolRuntime();

        Sense = new EnemySense(this);
        Combat = new EnemyCombat(this);

        Sense.AutoFindPlayerReferences();
        _state.WasPlayerHiding = playerHidingController != null && playerHidingController.isHiding;
        Combat.TickAnimation();
        SetNextGlobalPatDestination();

        LogAI("초기 상태: Patrol");
    }

    protected virtual void Update()
    {
        if (PlayerDeathDebug.IsDead)
            return;

        if (eyePoint == null || agent == null || !agent.isOnNavMesh)
            return;

        if (player == null)
            Sense.AutoFindPlayerReferences();

        Sense.CheckPlayerCatchDistance();
        Sense.Tick();
        Sense.TickHiding();

        switch (_state.CurrentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Chase:
                Combat.TickChase();
                break;

            case State.Investigate:
                Combat.TickInvestigate();
                break;
        }

        Combat.TickObstacleAvoidance();

        if (!_state.LockAnimator)
            Combat.TickAnimation();
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (Sense != null && Sense.IsPlayerContact(collision.collider))
            Sense.KillPlayerDirect();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (Sense != null && Sense.IsPlayerContact(other))
            Sense.KillPlayerDirect();
    }

    bool ValidateComponents()
    {
        if (agent == null)
        {
            Debug.LogError($"{name} : NavMeshAgent가 없습니다.");
            enabled = false;
            return false;
        }

        if (anim == null)
        {
            Debug.LogError($"{name} : Animator가 없습니다.");
            enabled = false;
            return false;
        }

        if (data == null)
        {
            Debug.LogError($"{name} : Monsters 데이터가 없습니다.");
            enabled = false;
            return false;
        }

        return true;
    }

    void InitState()
    {
        _state.CurrentState = State.Patrol;
        _state.LastKnownPosition = transform.position;
        _state.LastChaseDestination = transform.position;
        _state.LastObstacleCheckPosition = transform.position;
        _state.SenseEnableTime = Time.time + senseStartDelay;
    }

    void InitPatrolRuntime()
    {
        _navMotor = new NavMotor(agent);

        PatrolPointSelector pointSelector = null;
        if (patrolPointZone != null && patrolPointZone.HasPoints)
        {
            pointSelector = new PatrolPointSelector(
                transform,
                _navMotor,
                patrolPointZone,
                autoOpenDoorsOnPatrol,
                doorLayer
            );
        }

        PatrolPlanner planner = new PatrolPlanner(
            transform,
            _navMotor,
            patrolDestinationPickAttempts,
            autoOpenDoorsOnPatrol,
            doorLayer,
            doorCheckHeight,
            pathDoorCheckRadius,
            pointSelector
        );

        PatrolDoorService doorService = new PatrolDoorService(
            transform,
            _navMotor,
            planner,
            doorLayer,
            patrolDoorOpenDistance
        );

        _patrol = new PatrolBehavior(_navMotor, planner, doorService, patrolReachDistance);
    }

    void UpdatePatrol()
    {
        if (_state.IsBusy) return;

        LogAIThrottled("순찰 중");

        float patrolSpeed = data.moveSpeed * 0.5f;

        switch (_patrol.Update(patrolSpeed, autoOpenDoorsOnPatrol))
        {
            case PatrolBehavior.PatrolUpdateResult.ReachedDestination:
                LogAI("순찰 목적지 도착 → 새 순찰 목적지 선택");
                SetNextGlobalPatDestination();
                break;

            case PatrolBehavior.PatrolUpdateResult.NeedDestination:
                LogAI("순찰 목적지 선택");
                SetNextGlobalPatDestination();
                break;

            case PatrolBehavior.PatrolUpdateResult.HandlingDoor:
                LogAI("순찰 중 닫힌 문 자동 열기");
                break;

            case PatrolBehavior.PatrolUpdateResult.StuckNeedRepath:
                LogAI("벽/코너 막힘 → 새 순찰 목적지 선택");
                SetNextGlobalPatDestination();
                break;
        }

        SyncPatrolDestinationFromRuntime();
    }

    internal void SetNextGlobalPatDestination()
    {
        if (!agent.isOnNavMesh) return;

        _state.HasPatDestination = _patrol.PickRandomDestination();
        if (_state.HasPatDestination)
        {
            _state.CurrentPatrolDestination = _patrol.CurrentDestination;
            agent.isStopped = false;
            _navMotor?.ResetProgressTracking();
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;
    }

    internal void SyncPatrolDestinationFromRuntime()
    {
        _state.HasPatDestination = _patrol.HasDestination;
        _state.CurrentPatrolDestination = _patrol.CurrentDestination;
    }

    void SetupAgent()
    {
        agent.isStopped = false;
        agent.speed = data.moveSpeed * 0.5f;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 60);
    }

    void ApplyDataSettings()
    {
        viewAngle = data.viewAngle;

        if (data.obstacleLayer.value != 0)
            obstacleLayer = data.obstacleLayer;
    }

    void ResolvePatrolPointZone()
    {
        if (patrolPointZone != null)
            return;

        patrolPointZone = FindFirstObjectByType<PatrolPointZone>();
    }

    void AutoAssignDoorLayer()
    {
        int layer = LayerMask.NameToLayer("Door");
        if (layer < 0)
            return;

        if (doorLayer.value == 0)
            doorLayer = 1 << layer;

        EnemyDoorUtility.EnsureDoorsUseLayer(layer);
    }

    protected internal virtual Vector3 DetectPlayerPosition()
    {
        if (player != null)
            return player.position;

        return _state.LastKnownPosition;
    }

    protected bool SafeSetDestination(Vector3 target) => Combat.SafeSetDestination(target);

    protected DoorBrokenTest GetClosedDoorOnChasePath(float distance) =>
        Combat.GetClosedDoorOnChasePath(distance);

    protected internal virtual void HandleChaseSpecial()
    {
    }

    internal void LogAI(string message)
    {
        if (!debugStateLog) return;
        Debug.Log($"[{name}] {message}");
    }

    internal void LogSenseThrottled(string message)
    {
        if (!debugSenseLog) return;
        if (Time.time < _state.NextSenseLogTime) return;

        _state.NextSenseLogTime = Time.time + logInterval;
        Debug.Log($"[{name}] {message}");
    }

    internal void LogAIThrottled(string message)
    {
        if (!debugStateLog) return;
        if (Time.time < _state.NextLogTime) return;

        _state.NextLogTime = Time.time + logInterval;
        Debug.Log($"[{name}] {message}");
    }
}
