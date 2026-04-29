using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [SerializeField] private Collider[] ownColliders;
    [SerializeField] private LayerMask doorLayer;

    protected override void HandleDoor(DoorController door)
    {
        agent.SetDestination(player.position);
    }

    void Update()
    {
        Collider[] doors = Physics.OverlapSphere(transform.position, 1.5f, doorLayer);

        bool ignore = (currentState == State.Chase);

        for (int i = 0; i < doors.Length; i++)
        {
            for (int j = 0; j < ownColliders.Length; j++)
            {
                Physics.IgnoreCollision(ownColliders[j], doors[i], ignore);
            }
        }
    }
}