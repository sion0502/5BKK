using UnityEngine;

public class GhostEnemy : EnemyBase
{
    bool isPassingDoor = false;

    protected override void HandleDoor(
        DoorController door)
    {
        if (currentState != State.Chase)
            return;

        Agent.SetDestination(
            Player.position);
    }

    void Update()
    {
        if (currentState != State.Chase)
        {
            ExitDoorPassMode();
            return;
        }

        Vector3 center =
            transform.position + Vector3.up;

        Collider[] hits =
            Physics.OverlapSphere(
                center,
                1.5f);

        bool foundDoor = false;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Door"))
                continue;

            foundDoor = true;
            break;
        }

        if (foundDoor)
        {
            EnterDoorPassMode();

            Vector3 dir =
                (Player.position -
                transform.position).normalized;

            dir.y = 0;

            transform.position +=
                dir * 4f * Time.deltaTime;
        }
        else
        {
            ExitDoorPassMode();
        }
    }

    void EnterDoorPassMode()
    {
        if (isPassingDoor)
            return;

        Agent.enabled = false;

        isPassingDoor = true;
    }

    void ExitDoorPassMode()
    {
        if (!isPassingDoor)
            return;

        Agent.enabled = true;

        isPassingDoor = false;
    }
}