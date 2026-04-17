using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [SerializeField] private Collider[] ownColliders;
    [SerializeField] private LayerMask doorLayer;

    protected override void OnChaseUpdate()
    {
        Collider[] doors = Physics.OverlapSphere(transform.position, 1.5f, doorLayer);

        for (int i = 0; i < doors.Length; i++)
        {
            for (int j = 0; j < ownColliders.Length; j++)
            {
                Physics.IgnoreCollision(ownColliders[j], doors[i], true);
            }
        }
    }
}