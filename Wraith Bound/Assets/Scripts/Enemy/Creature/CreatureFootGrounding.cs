using System;
using UnityEngine;

/// <summary>
/// 크리처 발바닥을 바닥에 맞춤 (보이는 몸만 — 이동·판정·NavMesh 위치는 그대로).
/// Meshy 자동 리그 귀신(간호사·탈춤)은 Mixamo 클립을 리타깃하면 발 높이가 클립마다 달라 10~25cm 뜨거나 파묻힘.
/// 에디터(Tools > Creatures > Setup Foot Grounding)가 바인드 포즈 메쉬에서 실제 발바닥 정점 몇 개를
/// 발 뼈 기준 좌표(solePoints)로 구워 두고, 실행 중엔 그 점들만 따라가 매 프레임 발바닥 높이를 계산 —
/// 최근 windowSeconds 동안 가장 낮았던 발바닥(=디딘 발)이 바닥에 오도록 골반 뼈를 위/아래로 옮김.
/// 달리기에서 두 발이 다 뜬 순간(0.1초 안팎)은 구간 최솟값이라 따라 올라가지 않음.
/// </summary>
[DisallowMultipleComponent]
public class CreatureFootGrounding : MonoBehaviour
{
    [Serializable]
    public struct SolePoint
    {
        public Transform bone;
        public Vector3 local;
    }

    [Tooltip("옮길 뼈 (휴머노이드는 Hips, Xenosaurid는 Pelvis)")]
    [SerializeField] private Transform hips;
    [Tooltip("발바닥 정점 — 발 뼈 로컬 좌표. 에디터 메뉴로 생성")]
    [SerializeField] private SolePoint[] solePoints = Array.Empty<SolePoint>();
    [Tooltip("발바닥이 바닥보다 이만큼 위에 오게 (음수면 살짝 파묻힘 — 발톱·발끝이 바닥에 닿아 보이게)")]
    [SerializeField] private float soleClearance = 0f;
    [Tooltip("디딘 발을 찾는 구간 — 달리기의 두 발 다 뜬 순간은 넘기고, 절뚝 걸음의 출렁임은 따라가게")]
    [SerializeField] private float windowSeconds = 0.25f;
    [SerializeField] private float smoothing = 15f;
    [SerializeField] private float maxDrop = 0.5f;
    [SerializeField] private float maxRaise = 0.35f;
    [SerializeField] private LayerMask groundMask = ~0;

    private const int BufferSize = 256; // 고주사율(200fps 이상)에서도 windowSeconds를 담도록

    private readonly float[] lowest = new float[BufferSize];
    private readonly float[] times = new float[BufferSize];
    private int head;
    private int count;
    private float offset;

    /// <summary>이번 프레임에 골반을 옮긴 양(+ = 내림). 점프스케어 등에서 참고.</summary>
    public float CurrentDrop => offset;
    public bool IsConfigured => hips != null && solePoints != null && solePoints.Length > 0;

    public void Configure(Transform hipsBone, SolePoint[] points)
    {
        hips = hipsBone;
        solePoints = points;
    }

    /// <summary>골반을 내리고/올리는 한도 — 추격 때 다리가 길어지는 크리처(탈춤귀신)는 올림 한도를 크게.</summary>
    public void SetLimits(float drop, float raise)
    {
        maxDrop = drop;
        maxRaise = raise;
    }

    private void Awake()
    {
        if (!IsConfigured)
        {
            Debug.LogWarning("[CreatureFootGrounding] 발바닥 정점이 없음 — Tools > Creatures > Setup Foot Grounding 실행 필요: " + name, this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        count = 0;
        offset = 0f;
    }

    private void LateUpdate()
    {
        float groundY = GetGroundY();
        Push(LowestSoleY() - groundY - soleClearance);

        float target = Mathf.Clamp(WindowMin(), -maxRaise, maxDrop);
        offset = Mathf.Lerp(offset, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        if (Mathf.Abs(offset) > 0.0005f)
        {
            hips.position += Vector3.down * offset;
        }
    }

    private float LowestSoleY()
    {
        float min = float.MaxValue;
        for (int i = 0; i < solePoints.Length; i++)
        {
            Transform bone = solePoints[i].bone;
            if (bone == null)
            {
                continue;
            }

            float y = bone.TransformPoint(solePoints[i].local).y;
            if (y < min)
            {
                min = y;
            }
        }

        return min == float.MaxValue ? transform.position.y : min;
    }

    private float GetGroundY()
    {
        // 자기 캡슐 안(+1m)에서 쏘므로 자기 콜라이더는 맞지 않음
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return transform.position.y;
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
