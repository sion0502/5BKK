using UnityEngine;

/// <summary>추격 전용 연출(관절 꺾기·형태 변화·안광)이 공통으로 쓰는 "지금 추격 중인가" 판정.</summary>
public static class CreatureChaseState
{
    private static readonly int StateHash = Animator.StringToHash("State");

    /// <summary>Animator State 값 2 = 추격(EnemyBase·PrologueNurse 공통 규약).</summary>
    public static bool IsChasing(Animator animator)
    {
        return animator != null && animator.runtimeAnimatorController != null && animator.GetInteger(StateHash) == 2;
    }

    /// <summary>점프스케어(사망 연출) 중 — EnemyJumpscare가 Animator를 끄고 몸을 직접 옮김.</summary>
    public static bool IsJumpscare => PlayerDeathDebug.IsDying;
}
