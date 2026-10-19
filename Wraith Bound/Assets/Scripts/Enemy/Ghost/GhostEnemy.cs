using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 귀신: 순찰/수색은 괴물과 같음. 추격 중에는 문을 부수지 않고 통과.
/// 애니메이션이 없어도 NavMeshAgent로 이동한다.
/// </summary>
public class GhostEnemy : MonsterEnemy
{
    readonly List<DoorClick> _ignoredDoors = new List<DoorClick>();

    protected override void Awake()
    {
        autoOpenDoorsOnPatrol = true;
        base.Awake();
        DisableGhostAnimator();
    }

    protected override void Start()
    {
        DisableGhostAnimator();
        base.Start();
        DisableGhostAnimator();
        SnapGhostOntoNavMesh();
    }

    protected override void Update()
    {
        base.Update();

        if (currentState != State.Chase)
            RestoreGhostDoorCollisions();
    }

    protected override bool RequiresAnimator() => false;

    protected internal override void HandleChaseSpecial()
    {
        if (currentState != State.Chase)
            return;

        AllowChaseThroughDoors();
    }

    void DisableGhostAnimator()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        if (anim == null)
            return;

        anim.applyRootMotion = false;
        anim.enabled = false;
    }

    void SnapGhostOntoNavMesh()
    {
        if (agent == null)
            return;

        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;

        if (agent.isOnNavMesh)
            return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            return;

        agent.Warp(hit.position);
    }

    void AllowChaseThroughDoors()
    {
        Vector3 target = RuntimeState.LastKnownPosition;
        if (player != null && RuntimeState.CanDetectPlayer)
            target = player.position;

        DoorClick door = EnemyDoorUtility.FindClosedDoorBetween(
            transform.position,
            target,
            doorLayer,
            doorCheckHeight,
            pathDoorCheckRadius);

        if (door == null)
            door = EnemyDoorUtility.FindClosedDoorOnRoute(transform.position, target, 3f);

        if (door == null)
            door = EnemyDoorUtility.FindClosedDoorNearPosition(
                transform.position, doorLayer, chaseDoorDetectDistance);

        if (door == null)
            return;

        DoorNavMeshUtility.SetNavMeshBlocked(door.transform, false);
        IgnoreGhostDoorCollision(door);
    }

    void IgnoreGhostDoorCollision(DoorClick door)
    {
        if (door == null)
            return;

        Collider[] ghostCols = GetComponentsInChildren<Collider>(true);
        Collider[] doorCols = door.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < ghostCols.Length; i++)
        {
            if (ghostCols[i] == null || !ghostCols[i].enabled)
                continue;

            for (int j = 0; j < doorCols.Length; j++)
            {
                if (doorCols[j] == null || !doorCols[j].enabled)
                    continue;

                Physics.IgnoreCollision(ghostCols[i], doorCols[j], true);
            }
        }

        if (!_ignoredDoors.Contains(door))
            _ignoredDoors.Add(door);
    }

    void RestoreGhostDoorCollisions()
    {
        if (_ignoredDoors.Count == 0)
            return;

        Collider[] ghostCols = GetComponentsInChildren<Collider>(true);

        for (int d = 0; d < _ignoredDoors.Count; d++)
        {
            DoorClick door = _ignoredDoors[d];
            if (door == null)
                continue;

            Collider[] doorCols = door.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ghostCols.Length; i++)
            {
                if (ghostCols[i] == null)
                    continue;

                for (int j = 0; j < doorCols.Length; j++)
                {
                    if (doorCols[j] == null)
                        continue;

                    Physics.IgnoreCollision(ghostCols[i], doorCols[j], false);
                }
            }
        }

        _ignoredDoors.Clear();
    }
}
