using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    bool isAttacking;
    float timer;

    protected override void HandleDoor(DoorController door)
    {
        if (isAttacking) return;

        isAttacking = true;
        timer = 1.2f;

        agent.isStopped = true;

        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");

        door.TakeDamage(999);
    }

    void LateUpdate()
    {
        if (!isAttacking) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            isAttacking = false;
            agent.isStopped = false;
        }
    }
}