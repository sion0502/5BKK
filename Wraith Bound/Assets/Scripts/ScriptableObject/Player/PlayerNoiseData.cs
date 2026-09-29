using UnityEngine;

namespace Custom.NoiseSystem
{
    // 플레이어의 이동 및 소음 유발 상태 정의
    public enum MovementState
    {
        Idle,
        Run
    }



    [CreateAssetMenu(fileName = "PlayerNoiseData", menuName = "Custom/Player Noise Data")]
public class PlayerNoiseData : ScriptableObject
{
    [Header("Noise Radius")]
    [Tooltip("가만히 있을 때 발생하는 소음 범위 (0에 수렴 추천)")]
    public float idleRadius = 0f;
    [Tooltip("달리기 시 발생하는 소음 범위")]
    public float runRadius = 15.0f;

    [Header("Interpolation Settings")]
    [Tooltip("상태 전환 시 소음 범위 변화의 부드러운 정도 (Lerp Speed)")]
    public float lerpSpeed = 8.0f;

    public float GetRadius(MovementState state)
    {
        return state switch
        {
            MovementState.Idle => idleRadius,
            MovementState.Run => runRadius,
            _ => 0f
        };
    }
}
}