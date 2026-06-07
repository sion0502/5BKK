using System.Collections.Generic;
using UnityEngine;

public class GhostEnemy : EnemyBase
{
    [Header("Ghost Pass Door")]
    [SerializeField] private Collider ghostBodyCollider;
    [SerializeField] private float doorIgnoreRadius = 2.5f;

    private readonly List<Collider> ignoredDoors = new List<Collider>();

    protected override void Awake()
    {
        base.Awake();

        if (ghostBodyCollider == null)
            ghostBodyCollider = GetComponent<Collider>();
    }

    protected override void Update()
    {
        base.Update();

        if (ghostBodyCollider == null)
            return;

        if (currentState == State.Chase)
            IgnoreClosedDoorsNearby();
        else
            RestoreIgnoredDoors();
    }

    private void IgnoreClosedDoorsNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, doorIgnoreRadius, doorLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || col == ghostBodyCollider)
                continue;

            DoorClick door = col.GetComponentInParent<DoorClick>();
            if (door == null)
                continue;

            if (door.IsOpen() || door.IsBroken())
                continue;

            if (ignoredDoors.Contains(col))
                continue;

            Physics.IgnoreCollision(ghostBodyCollider, col, true);
            ignoredDoors.Add(col);
        }
    }

    private void RestoreIgnoredDoors()
    {
        for (int i = ignoredDoors.Count - 1; i >= 0; i--)
        {
            Collider col = ignoredDoors[i];

            if (col != null && ghostBodyCollider != null)
                Physics.IgnoreCollision(ghostBodyCollider, col, false);

            ignoredDoors.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        RestoreIgnoredDoors();
    }
}
