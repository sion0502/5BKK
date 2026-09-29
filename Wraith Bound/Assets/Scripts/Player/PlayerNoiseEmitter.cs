using UnityEngine;
using Custom.NoiseSystem;

public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private PlayerNoiseData noiseData;
    [SerializeField] private LayerMask aiLayerMask;
    [SerializeField] private PlayerController playerController;

    [Header("State Thresholds")]
    [SerializeField] private float walkSpeedThreshold = 0.1f;
    [SerializeField] private float runSpeedThreshold = 4.5f;

    private CharacterController characterController;

    // 움직임 상태와 소리 범위 프로퍼티
    public MovementState CurrentState { get; private set; } = MovementState.Idle;
    public float CurrentNoiseRadius { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        DetermineMovementState();
        UpdateNoiseRadius();
        //EmitNoiseToAI();
    }

    // 속도 벡터의 Horizontal 평면 투영 및 입력 상태 조합으로 플레이어 상태 정의
    private void DetermineMovementState()
    {
        // 1. Horizontal 속도 계산 (Y축 제거)
        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        // 2. 가만히 있는지 판별
        if (currentSpeed < walkSpeedThreshold)
        {
            CurrentState = MovementState.Idle;
            return;
        }

        // 3. 움직이는 중일 때 상태 세분화
        if (playerController.isRun && currentSpeed > runSpeedThreshold)
        {
            CurrentState = MovementState.Run;
        }
    }

    // 상태 변화에 따른 소음 범위 보간 처리 (급격한 변화 방지)
    private void UpdateNoiseRadius()
    {
        float targetRadius = noiseData.GetRadius(CurrentState);
        CurrentNoiseRadius = Mathf.Lerp(CurrentNoiseRadius, targetRadius, Time.deltaTime * noiseData.lerpSpeed);
    }

    // OverlapSphere를 사용하여 가청 범위 내의 AI 에이전트에 자극 전달 (Polling 방식)
    /*
    private void EmitNoiseToAI()
    {
        if (CurrentNoiseRadius <= 0.1f) return;

        // 소음 범위 내의 AI Collider 수집
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, CurrentNoiseRadius, aiLayerMask);

        foreach (var col in hitColliders)
        {
            // 인터페이스 기반의 AI 수신 처리 (예: INoiseHearable)
            if (col.TryGetComponent<INoiseHearable>(out var hearable))
            {
                hearable.OnNoiseHeard(transform.position, CurrentNoiseRadius, CurrentState);
            }
        }
    } */

    // 소리 범위 시각화
    private void OnDrawGizmosSelected()
    {
        if (noiseData == null) return;

        Color gizmoColor = CurrentState switch
        {
            MovementState.Idle => Color.gray,
            MovementState.Run => Color.red,
            _ => Color.white
        };

        gizmoColor.a = 0.25f;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, CurrentNoiseRadius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1.0f);
        Gizmos.DrawWireSphere(transform.position, CurrentNoiseRadius);
    }
}
