using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 걷기·달리기 애니메이션 재생 속도를 실제 이동 속도에 맞춤 → 발 미끄러짐(문워크) 줄임.
/// Animator의 State 값(1=걷기/순찰, 2=달리기/추격)에 따라 해당 클립의 "보폭 속도"(발이 안 미끄러지는 이동 속도,
/// Tools > Creatures > Measure Ground Offset으로 측정)로 나눈 값을 MoveSpeedMul 파라미터에 넣음.
/// 컨트롤러의 걷기·달리기 상태가 MoveSpeedMul을 속도 배수로 써야 동작 (Tools > Creatures > Setup Animator Transitions).
/// EnemyBase·AI 코드는 건드리지 않고 NavMeshAgent 속도만 읽음.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureLocomotionSync : MonoBehaviour
{
    public const string SpeedParameter = "MoveSpeedMul";

    [Tooltip("걷기(State 1) 클립의 보폭 속도 m/s — 0이면 맞추지 않음(춤 등 이동이 아닌 동작)")]
    [SerializeField] private float walkStrideSpeed = 1.9f;
    [Tooltip("달리기(State 2) 클립의 보폭 속도 m/s — 0이면 맞추지 않음")]
    [SerializeField] private float runStrideSpeed = 3.4f;
    [Tooltip("느리게 움직이거나 막혀 멈춰 있을 때의 최저 배수 — 너무 낮추면 슬로모션처럼 보임")]
    [SerializeField] private float minMultiplier = 0.6f;
    [SerializeField] private float maxMultiplier = 1.5f;
    [SerializeField] private float smoothing = 6f;

    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

    private Animator animator;
    private NavMeshAgent agent;
    private bool hasParameter;
    private float multiplier = 1f;
    private Vector3 lastPosition;

    public void Configure(float walkStride, float runStride, float min, float max)
    {
        walkStrideSpeed = walkStride;
        runStrideSpeed = runStride;
        minMultiplier = min;
        maxMultiplier = max;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float)
            {
                hasParameter = true;
            }
        }

        if (!hasParameter)
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        multiplier = 1f;
    }

    private void Update()
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        float stride = 0f;
        int state = animator.GetInteger(StateHash);
        if (state == 1)
        {
            stride = walkStrideSpeed;
        }
        else if (state == 2)
        {
            stride = runStrideSpeed;
        }

        float target = 1f;
        if (stride > 0.01f)
        {
            target = Mathf.Clamp(CurrentSpeed() / stride, minMultiplier, maxMultiplier);
        }

        multiplier = Mathf.Lerp(multiplier, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        animator.SetFloat(SpeedHash, multiplier);
    }

    private float CurrentSpeed()
    {
        // 에이전트가 켜져 있으면 그 속도, 아니면(연출로 직접 옮길 때) 실제 이동량
        float speed;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            Vector3 v = agent.velocity;
            v.y = 0f;
            speed = v.magnitude;
        }
        else
        {
            Vector3 d = transform.position - lastPosition;
            d.y = 0f;
            speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        }

        lastPosition = transform.position;
        return speed;
    }
}
