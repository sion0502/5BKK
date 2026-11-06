using UnityEngine;

/// <summary>
/// 휴머노이드 크리처 발 붙이기 (보이는 몸만, 이동·판정은 그대로).
/// NurseGhost 걷기·달리기 클립은 원본 루트 높이(Keep Original Y)로 들어와 있어 재생 중 몸이 바닥에서 10~30cm 뜸.
/// 애니메이션 에셋(본편 공용)은 건드리지 않고, 매 LateUpdate에 최근 windowSeconds 동안 가장 낮았던 발끝 높이를
/// 디딘 발로 보고 그만큼 엉덩이 뼈를 내림. 내리기만 하고 올리지는 않음(발이 바닥에 묻히는 건 원래 동작 그대로).
/// </summary>
[RequireComponent(typeof(Animator))]
public class CreatureFootGrounding : MonoBehaviour
{
    [Tooltip("발바닥이 바닥에 닿았을 때 발끝 뼈의 높이 (NurseGhost 실측 약 0.03~0.04)")]
    [SerializeField] private float toeRestHeight = 0.035f;
    [Tooltip("디딘 발 높이를 찾는 구간 — 달리기의 두 발 다 뜬 순간(약 0.1초)은 넘기고, 절뚝 걸음의 출렁임은 따라가게")]
    [SerializeField] private float windowSeconds = 0.25f;
    [SerializeField] private float smoothing = 15f;
    [SerializeField] private float maxDrop = 0.6f;
    [SerializeField] private LayerMask groundMask = ~0;

    private const int BufferSize = 256; // 고주사율(200fps 이상)에서도 windowSeconds를 담도록

    private Animator animator;
    private Transform hips;
    private Transform[] feet;
    private readonly float[] lowest = new float[BufferSize];
    private readonly float[] times = new float[BufferSize];
    private int head;
    private int count;
    private float drop;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            feet = new[]
            {
                Bone(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot),
                Bone(HumanBodyBones.RightToes, HumanBodyBones.RightFoot),
            };
        }

        if (hips == null || feet == null || feet[0] == null || feet[1] == null)
        {
            enabled = false;
        }
    }

    private Transform Bone(HumanBodyBones preferred, HumanBodyBones fallback)
    {
        Transform bone = animator.GetBoneTransform(preferred);
        return bone != null ? bone : animator.GetBoneTransform(fallback);
    }

    private void OnEnable()
    {
        count = 0;
        drop = 0f;
    }

    private void LateUpdate()
    {
        if (!TryGetGround(out float groundY))
        {
            return;
        }

        float lowestFoot = Mathf.Min(feet[0].position.y, feet[1].position.y) - groundY;
        Push(lowestFoot);

        float planted = WindowMin();
        float target = Mathf.Clamp(planted - toeRestHeight, 0f, maxDrop);
        drop = Mathf.Lerp(drop, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        if (drop > 0.0005f)
        {
            hips.position += Vector3.down * drop;
        }
    }

    private bool TryGetGround(out float groundY)
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private void Push(float value)
    {
        lowest[head] = value;
        times[head] = Time.time;
        head = (head + 1) % BufferSize;
        count = Mathf.Min(count + 1, BufferSize);
    }

    private float WindowMin()
    {
        float min = float.MaxValue;
        float since = Time.time - windowSeconds;
        for (int i = 0; i < count; i++)
        {
            int index = (head - 1 - i + BufferSize) % BufferSize;
            if (times[index] < since)
            {
                break;
            }

            min = Mathf.Min(min, lowest[index]);
        }

        return min == float.MaxValue ? 0f : min;
    }
}
