using UnityEngine;

public class TheMimicController : EnemyBase
{
    bool isAttacking = false;

    protected override void HandleDoor(DoorController door)
    {
        if (isAttacking) return;

        isAttacking = true;

        agent.isStopped = true;

        if (animator != null)
            animator.SetTrigger("Attack");

        door.TakeDamage(1);

        Invoke(nameof(EndAttack), 2f);
    }

    void EndAttack()
    {
        isAttacking = false;
        agent.isStopped = false;
    }
}