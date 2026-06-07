using System.Collections;
using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorDetectDistance = 2.2f;
    [SerializeField] private float idleBeforeAttackTime = 0.5f;
    [SerializeField] private float doorHitDelay = 0.35f;
    [SerializeField] private float attackEndDelay = 0.7f;
    [SerializeField] private float faceRotateSpeed = 12f;
    [SerializeField] private float postAttackCooldown = 0.2f;

    private bool attacking;
    private float nextPossibleAttackTime;

    protected override void HandleChaseSpecial()
    {
        if (currentState != State.Chase)
            return;

        if (attacking)
            return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(doorDetectDistance);

        if (door == null && agent.velocity.sqrMagnitude < 0.05f)
            door = GetClosedDoorOnChasePath(doorDetectDistance * 1.5f);

        if (door == null)
            return;

        // 이미 공격 중이나, 다음 공격 가능 시간 지나야 함
        if (Time.time < nextPossibleAttackTime)
            return;

        StartCoroutine(AttackDoorRoutine(door));
    }

    private IEnumerator AttackDoorRoutine(DoorBrokenTest door)
    {
        if (door == null)
            yield break;

        DoorClick click = door.GetComponent<DoorClick>();
        if (click == null)
            yield break;

        // 공격 가능한지 다시 한번 확인
        if (click.IsOpen() || click.IsBroken() || door.IsBroken())
            yield break;

        // 헛공격 방지: 이미 공격 중이 아니고, cooldown 경과 시 시작
        if (attacking)
            yield break;

        attacking = true;
        isBusy = true;
        lockAnimator = true;

        agent.isStopped = true;
        agent.ResetPath();

        // Idle 상태로 바라보기
        anim.ResetTrigger(AnimAttack);
        anim.SetInteger(AnimState, 0);

        float timer = 0f;

        // 0.5초 동안 문 방향으로 바라보기
        while (timer < idleBeforeAttackTime)
        {
            if (door == null)
            {
                EndDoorAttack();
                yield break;
            }

            DoorClick currentClick = door.GetComponent<DoorClick>();
            if (currentClick == null || currentClick.IsOpen() || currentClick.IsBroken() || door.IsBroken())
            {
                EndDoorAttack();
                yield break;
            }

            Vector3 dir = door.transform.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Time.deltaTime * faceRotateSpeed
                );
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // 최종 확인: 공격 직전 문이 닫혀 있고, 열려 있지 않은지 재확인
        DoorClick finalClick = door.GetComponent<DoorClick>();
        if (finalClick == null || finalClick.IsOpen() || finalClick.IsBroken() || door.IsBroken())
        {
            EndDoorAttack();
            yield break;
        }

        // Attack 애니메이션 트리거
        anim.SetTrigger(AnimAttack);

        // 공격 딜레이 기다린 뒤, 문 파괴 처리
        yield return new WaitForSeconds(doorHitDelay);

        // 실제 파괴
        if (door != null && !door.IsBroken())
        {
            door.HitDoor(transform.position);
        }

        // Attack End Delay 대기
        yield return new WaitForSeconds(attackEndDelay);

        // 공격 종료 후 다음 가능 시간 설정
        nextPossibleAttackTime = Time.time + postAttackCooldown;

        EndDoorAttack();
    }

    private void EndDoorAttack()
    {
        anim.SetInteger(AnimState, 2);

        agent.isStopped = false;

        if (player != null)
            agent.SetDestination(player.position);

        lockAnimator = false;
        isBusy = false;
        attacking = false;
    }
}
