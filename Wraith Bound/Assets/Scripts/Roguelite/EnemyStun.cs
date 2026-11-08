using UnityEngine;

/// <summary>
/// 적을 그 자리에서 잠깐 기절시킴 (기 '구사일생' 부활 때). AI(EnemyBase — 팀원 코드, 수정 안 함)는 끄기만 하고,
/// 이동을 멈추고 애니메이션을 굳힌 채 몸을 잘게 떨게 함 → 시간이 지나면 되돌리고 순찰로.
/// </summary>
public class EnemyStun : MonoBehaviour
{
    private EnemyBase enemy;
    private float until;
    private Vector3 origin;
    private float animSpeed = 1f;

    public static void Apply(EnemyBase target, float seconds)
    {
        if (target == null)
        {
            return;
        }

        EnemyStun stun = target.GetComponent<EnemyStun>();
        if (stun == null)
        {
            stun = target.gameObject.AddComponent<EnemyStun>();
        }

        stun.Begin(target, seconds);
    }

    private void Begin(EnemyBase target, float seconds)
    {
        enemy = target;
        until = Time.time + seconds;
        origin = transform.position;
        enemy.StopAllCoroutines();
        enemy.enabled = false;
        if (enemy.Agent != null && enemy.Agent.enabled && enemy.Agent.isOnNavMesh)
        {
            enemy.Agent.isStopped = true;
            enemy.Agent.ResetPath();
            enemy.Agent.velocity = Vector3.zero;
        }

        if (enemy.Anim != null)
        {
            animSpeed = enemy.Anim.speed > 0f ? enemy.Anim.speed : 1f;
            enemy.Anim.speed = 0f; // 그 자세 그대로 굳음
        }
    }

    private void Update()
    {
        if (enemy == null)
        {
            Destroy(this);
            return;
        }

        float left = until - Time.time;
        if (left > 0f)
        {
            // 기절 중 잘게 떨림 (끝으로 갈수록 약해짐)
            float k = Mathf.Clamp01(left / 2f);
            transform.position = origin + new Vector3(Mathf.Sin(Time.time * 61f), 0f, Mathf.Cos(Time.time * 53f)) * 0.025f * k;
            return;
        }

        transform.position = origin;
        if (enemy.Anim != null)
        {
            enemy.Anim.speed = animSpeed;
        }

        enemy.enabled = true;
        if (enemy.Agent != null && enemy.Agent.enabled && enemy.Agent.isOnNavMesh)
        {
            enemy.Agent.isStopped = false;
        }

        enemy.ReturnToPatrolAfterDeath();
        Destroy(this);
    }
}
