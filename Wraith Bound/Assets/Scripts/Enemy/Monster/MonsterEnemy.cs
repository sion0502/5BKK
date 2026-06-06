using UnityEngine;
using System.Collections;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorDetectDistance = 3f;
    [SerializeField] private float idleBeforeAttackTime = 0.5f;
    [SerializeField] private float doorHitDelay = 0.35f;
    [SerializeField] private float attackEndDelay = 0.7f;
    [SerializeField] private LayerMask doorLayer;

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

        Vector3 dir = eyePoint.forward;

        if (!Physics.Raycast(
            eyePoint.position,
            dir,
            out RaycastHit hit,
            doorDetectDistance,
            doorLayer,
            QueryTriggerInteraction.Collide))
        {
            return;
        }

        DoorClick click =
            hit.collider.GetComponentInParent<DoorClick>();

        if (click == null)
            return;

        if (click.IsOpen())
            return;

        if (click.IsBroken())
            return;

        DoorBrokenTest door =
            hit.collider.GetComponentInParent<DoorBrokenTest>();

        if (door == null)
            return;

        if (door.IsBroken())
            return;

        StartCoroutine(
            AttackDoorRoutine(door));
    }

    private IEnumerator AttackDoorRoutine(
    DoorBrokenTest door)
    {
        attacking = true;
        isBusy = true;
        lockAnimator = true;

        agent.isStopped = true;
        agent.ResetPath();

        anim.SetInteger(
            AnimState,
            0);

        float timer = 0f;

        while (timer < 0.5f)
        {
            if (door == null)
            {
                EndDoorAttack();
                yield break;
            }

            Vector3 lookPos =
                door.transform.position;

            lookPos.y =
                transform.position.y;

            transform.LookAt(
                lookPos);

            timer += Time.deltaTime;

            yield return null;
        }

        DoorClick click =
            door.GetComponent<DoorClick>();

        if (click != null)
        {
            if (click.IsOpen())
            {
                EndDoorAttack();
                yield break;
            }
        }

        anim.SetTrigger(
            AnimAttack);

        yield return new WaitForSeconds(
            doorHitDelay);

        if (!door.IsBroken())
        {
            door.HitDoor(
                transform.position);
        }

        yield return new WaitForSeconds(
            attackEndDelay);

        EndDoorAttack();
    }

    private void EndDoorAttack()
    {
        anim.SetInteger(
            AnimState,
            2);

        agent.isStopped = false;

        if (player != null)
        {
            agent.SetDestination(
                player.position);
        }

        lockAnimator = false;
        isBusy = false;
        attacking = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            doorDetectDistance);
    }
#endif
}