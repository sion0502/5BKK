using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [SerializeField] private LayerMask doorLayer;

    bool isPassing = false;

    protected override void HandleDoor(DoorController door)
    {
        Agent.SetDestination(Player.position);
    }

    void Update()
    {
        if (currentState != State.Chase)
        {
            if (isPassing)
            {
                Agent.enabled = true;
                isPassing = false;
            }
            return;
        }

        Vector3 center = transform.position + Vector3.up;

        Collider[] doors = Physics.OverlapSphere(center, 1.5f, doorLayer);

        if (doors.Length > 0)
        {
            if (!isPassing)
            {
                Agent.enabled = false; // 🔥 핵심
                isPassing = true;
            }

            // 🔥 플레이어 방향으로 이동 (문 통과)
            Vector3 dir = (Player.position - transform.position).normalized;
            dir.y = 0;

            transform.position += dir * 4f * Time.deltaTime;
        }
        else
        {
            if (isPassing)
            {
                Agent.enabled = true;
                isPassing = false;
            }
        }
    }
}