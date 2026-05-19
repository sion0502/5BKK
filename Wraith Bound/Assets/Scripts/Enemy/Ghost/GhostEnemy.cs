using UnityEngine;

public class GhostEnemy : EnemyBase
{
    DoorController currentDoor;

    protected override void HandleDoor(
        DoorController door)
    {
        if (currentState != State.Chase)
            return;

        if (door == null)
            return;

        if (currentDoor == door)
            return;

        currentDoor = door;

        currentDoor.OpenPath();
    }

    void LateUpdate()
    {
        if (currentState == State.Chase)
            return;

        if (currentDoor == null)
            return;

        currentDoor.ClosePath();

        currentDoor = null;
    }
}