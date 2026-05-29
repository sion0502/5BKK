using UnityEngine;
using System.Collections;

public class MonsterEnemy : EnemyBase
{
    [SerializeField]
    float doorDetectDistance = 2f;

    bool attacking;

    protected override void Update()
    {
        base.Update();

        if (currentState != State.Chase)
            return;

        if (attacking)
            return;

        CheckDoor();
    }

    void CheckDoor()
    {
        if (Physics.Raycast(
            eyePoint.position,
            transform.forward,
            out RaycastHit hit,
            doorDetectDistance))
        {
            if (hit.collider.gameObject.layer !=
                LayerMask.NameToLayer("Door"))
                return;

            DoorClick click =
                hit.collider.GetComponentInParent<DoorClick>();

            if (click == null)
                return;

            if (click.IsOpen())
                return;

            DoorBrokenTest door =
                hit.collider.GetComponentInParent<DoorBrokenTest>();

            if (door == null)
                return;

            StartCoroutine(
                AttackDoorRoutine(
                    door));
        }
    }

    IEnumerator AttackDoorRoutine(
        DoorBrokenTest door)
    {
        attacking = true;

        isBusy = true;

        agent.isStopped = true;

        anim.SetInteger(
            "State",
            0);

        Vector3 lookPos =
            door.transform.position;

        lookPos.y =
            transform.position.y;

        transform.LookAt(
            lookPos);

        yield return new WaitForSeconds(
            0.5f);

        anim.SetTrigger(
            "Attack");

        yield return new WaitForSeconds(
            0.3f);

        door.HitDoor();

        yield return new WaitForSeconds(
            1f);

        agent.isStopped = false;

        isBusy = false;

        attacking = false;
    }
}