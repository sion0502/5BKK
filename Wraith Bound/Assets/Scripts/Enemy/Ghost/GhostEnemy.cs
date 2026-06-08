using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [Header("Door Pass-Through")]
    [SerializeField] private bool canPassThroughDoors = true;

    protected override void HandleChaseSpecial()
    {
        if (currentState != State.Chase) return;
        if (!canPassThroughDoors) return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(chaseDoorDetectDistance);

        if (door == null) return;

        agent.isStopped = false;

        if (canDetectPlayer)
        {
            agent.SetDestination(lastKnownPosition);
        }
    }

    protected override Vector3 DetectPlayerPosition()
    {
        if (player != null)
            return player.position;

        return base.DetectPlayerPosition();
    }
}
