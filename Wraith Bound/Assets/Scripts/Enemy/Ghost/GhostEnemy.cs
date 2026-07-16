using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [Header("Door Pass-Through")]
    [SerializeField] private bool canPassThroughDoors = true;

    protected internal override void HandleChaseSpecial()
    {
        if (currentState != State.Chase) return;
        if (!doorSpecialAllowed) return;
        if (!canPassThroughDoors) return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(chaseDoorDetectDistance);

        if (door == null) return;

        agent.isStopped = false;

        if (canDetectPlayer)
        {
            agent.SetDestination(lastKnownPosition);
        }
    }

    protected internal override Vector3 DetectPlayerPosition()
    {
        if (player != null)
            return player.position;

        return base.DetectPlayerPosition();
    }
}
