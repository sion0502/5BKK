using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 크리처 전용 점프스케어 공통 부모. 크리처 프리팹에 붙어 있으면 EnemyJumpscare가 기본 "돌진" 연출 대신 Play를 재생.
/// 시작 시점: 카메라가 크리처 쪽으로 돈 직후(0.14초), 크리처는 카메라를 보게 회전돼 있음. 끝나면 EnemyJumpscare가
/// 카메라·크리처 위치를 되돌리고 게임오버로 넘김. 전부 실제 시간 — timeScale은 0.
/// </summary>
public abstract class CreatureJumpscare : MonoBehaviour
{
    [Tooltip("전용 비명 — 비우면 EnemyBase의 jumpscareSound")]
    [SerializeField] protected AudioClip scareSound;

    public abstract IEnumerator Play(JumpscareStage stage);

    /// <summary>seconds 동안 매 프레임 tick(0→1 진행도, 경과 초) 호출. 실제 시간.</summary>
    protected static IEnumerator Run(float seconds, Action<float, float> tick)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            tick(seconds > 0f ? elapsed / seconds : 1f, elapsed);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        tick(1f, seconds);
    }

    protected static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    protected static float Smooth(float t)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    /// <summary>목표를 살짝 넘었다 돌아오는 꺾임 (목 꺾기·고개 돌리기).</summary>
    protected static float Overshoot(float t, float amount = 1.6f)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + (amount + 1f) * t * t * t + amount * t * t;
    }
}
