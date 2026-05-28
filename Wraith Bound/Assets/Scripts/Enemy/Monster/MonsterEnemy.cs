using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    bool attacking;

    protected override void HandleDoor(
        DoorController door)
    {
        if (attacking)
            return;

        attacking = true;

        Agent.isStopped = true;

        Vector3 lookPos =
            door.transform.position;

        lookPos.y =
            transform.position.y;

        transform.LookAt(lookPos);

        Anim.SetTrigger("Attack");

        DoorBrokenTest broken =
            door.GetComponent<DoorBrokenTest>();

        if (broken != null)
        {
            broken.SendMessage("HitDoor");
        }

        Invoke(
            nameof(EndAttack),
            1.2f
        );
    }

    void EndAttack()
    {
        attacking = false;

        Agent.isStopped = false;
    }
}