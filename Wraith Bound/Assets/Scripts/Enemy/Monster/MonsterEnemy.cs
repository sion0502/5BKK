using UnityEngine;
using System.Collections;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorDetectDistance = 2f;
    [SerializeField] private float idleBeforeAttackTime = 0.5f;
    [SerializeField] private float doorHitDelay = 0.3f;
    [SerializeField] private float attackEndDelay = 1f;

    private bool attacking;

    protected override void HandleChaseSpecial()
    {
        if (currentState != State.Chase)
            return;

        if (attacking)
            return;

        CheckDoor();
    }

    private void CheckDoor()
    {
        if (eyePoint == null)
            return;

        if (Physics.Raycast(
            eyePoint.position,
            transform.forward,
            out RaycastHit hit,
            doorDetectDistance))
        {
            if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Door"))
                return;

            DoorClick click = hit.collider.GetComponentInParent<DoorClick>();

            if (click == null)
                return;

            if (click.IsOpen())
                return;

            DoorBrokenTest door = hit.collider.GetComponentInParent<DoorBrokenTest>();

            if (door == null)
                return;

            StartCoroutine(AttackDoorRoutine(door));
        }
    }

    private IEnumerator AttackDoorRoutine(DoorBrokenTest door)
    {
        attacking = true;
        isBusy = true;
        lockAnimator = true;

        agent.isStopped = true;
        agent.ResetPath();

        anim.SetInteger(AnimState, 0);

        Vector3 lookPos = door.transform.position;
        lookPos.y = transform.position.y;
        transform.LookAt(lookPos);

        yield return new WaitForSeconds(idleBeforeAttackTime);

        anim.SetTrigger(AnimAttack);

        yield return new WaitForSeconds(doorHitDelay);

        if (door != null)
            door.HitDoor();

        yield return new WaitForSeconds(attackEndDelay);

        anim.SetInteger(AnimState, 2);

        agent.isStopped = false;

        lockAnimator = false;
        isBusy = false;
        attacking = false;
    }
}
