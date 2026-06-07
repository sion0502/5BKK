using System.Collections;
using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorDetectDistance = 2.2f;
    [SerializeField] private float idleBeforeAttackTime = 0.5f;
    [SerializeField] private float doorHitDelay = 0.35f;
    [SerializeField] private float attackEndDelay = 0.7f;

    private bool attacking;

    protected override void HandleChaseSpecial()
    {
        if (currentState != State.Chase)
            return;

        if (attacking)
            return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(doorDetectDistance);
        if (door == null)
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

        if (click.IsOpen() || click.IsBroken() || door.IsBroken())
            yield break;

        attacking = true;
        isBusy = true;
        lockAnimator = true;

        agent.isStopped = true;
        agent.ResetPath();

        anim.SetInteger(AnimState, 0);

        float timer = 0f;

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

            Vector3 lookPos = door.transform.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);

            timer += Time.deltaTime;
            yield return null;
        }

        DoorClick finalClick = door.GetComponent<DoorClick>();
        if (finalClick == null || finalClick.IsOpen() || finalClick.IsBroken() || door.IsBroken())
        {
            EndDoorAttack();
            yield break;
        }

        anim.SetTrigger(AnimAttack);

        yield return new WaitForSeconds(doorHitDelay);

        if (!door.IsBroken())
        {
            DoorClick hitClick = door.GetComponent<DoorClick>();
            if (hitClick != null && !hitClick.IsOpen() && !hitClick.IsBroken())
                door.HitDoor(transform.position);
        }

        yield return new WaitForSeconds(attackEndDelay);

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
