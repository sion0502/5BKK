using UnityEngine;

public class TheMimicController : EnemyBase
{
    bool isAttacking = false;

    protected override void HandleDoor(DoorController door)
    {
        if (isAttacking) return;

        isAttacking = true;

        Agent.isStopped = true;

        if (Anim != null)
            Anim.SetTrigger("Attack");

        door.TakeDamage(1);

        Invoke(nameof(EndAttack), 2f);
    }

    void EndAttack()
    {
        isAttacking = false;
        Agent.isStopped = false;
    }
}