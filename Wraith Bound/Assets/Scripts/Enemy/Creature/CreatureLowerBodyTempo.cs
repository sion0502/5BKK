using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 추격 중 하반신만 따로 빠르게 (탈춤귀신, 2026-11-18 — "다리 걸음이랑 싱크가 안 맞아 미끄러진다, 하반신 애니메이션 속도 올릴 수 있어?").
/// 컨트롤러의 하반신 레이어(LowerBody: 다리만 마스크, 상체와 같은 순찰 춤)를 추격할 때만 켜고,
/// 재생 속도(LegSpeedMul)를 실제 이동 속도 ÷ 춤 보폭 속도로 맞춰 발이 덜 미끄러지게. 상체는 Base Layer 속도(MoveSpeedMul) 그대로 춤.
/// 춤 보폭 = 클립이 한 번 도는 동안 앞으로 가는 거리 ÷ 길이 (Jazz Dancing: 1.72m/2.72초 ≈ 0.63m/s, 모델 크기 1 기준) × 크리처 크기.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureLowerBodyTempo : MonoBehaviour
{
    [SerializeField] private string layerName = "LowerBody";
    [SerializeField] private string speedParameter = "LegSpeedMul";
    [Tooltip("하반신 클립을 1배로 틀 때 앞으로 가는 속도(m/s, 모델 크기 1 기준)")]
    [SerializeField] private float strideSpeed = 0.63f;
    [SerializeField] private float minMultiplier = 0.8f;
    [SerializeField] private float maxMultiplier = 4.5f;
    [SerializeField] private float smoothing = 6f;
    [SerializeField] private float fadeSeconds = 0.25f;

    private Animator animator;
    private NavMeshAgent agent;
    private int layer = -1;
    private int speedHash;
    private float weight;
    private float multiplier = 1f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponentInParent<NavMeshAgent>();
        layer = animator.GetLayerIndex(layerName);
        speedHash = Animator.StringToHash(speedParameter);
        if (layer < 0)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        bool on = CreatureChaseState.IsChasing(animator) && !CreatureChaseState.IsJumpscare;
        weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        animator.SetLayerWeight(layer, weight);
        if (weight <= 0f)
        {
            return;
        }

        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        float stride = Mathf.Max(0.01f, strideSpeed * transform.lossyScale.y);
        float target = Mathf.Clamp(speed / stride, minMultiplier, maxMultiplier);
        multiplier = Mathf.Lerp(multiplier, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        animator.SetFloat(speedHash, multiplier);
    }
}
