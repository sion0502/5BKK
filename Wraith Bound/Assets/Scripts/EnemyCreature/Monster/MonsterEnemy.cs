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

        Agent.isStopped = true;

        Anim.ResetTrigger("Attack");
        Anim.SetTrigger("Attack");

        door.TakeDamage(999);
    }

    void LateUpdate()
    {
        if (!isAttacking) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            isAttacking = false;
            Agent.isStopped = false;
        }
    }
}