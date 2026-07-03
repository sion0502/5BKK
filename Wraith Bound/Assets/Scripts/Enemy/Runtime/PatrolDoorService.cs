using UnityEngine;
using UnityEngine.AI;

public sealed class PatrolDoorService
{
    readonly Transform _transform;
    readonly NavMotor _motor;
    readonly PatrolPlanner _planner;
    readonly LayerMask _doorLayer;
    readonly float _doorCheckHeight;
    readonly float _pathDoorCheckRadius;
    readonly float _doorDetectDistance;
    readonly float _doorOpenRange;
    readonly float _doorSearchRadius;
    readonly float _passThroughDistance;
    readonly float _reachExtra;

    public bool ThroughDoorActive { get; private set; }
    public Vector3 ThroughDoorPoint { get; private set; }

    public LayerMask DoorLayer => _doorLayer;
    public float DoorCheckHeight => _doorCheckHeight;
    public float PathDoorCheckRadius => _pathDoorCheckRadius;

    public PatrolDoorService(
        Transform transform,
        NavMotor motor,
        PatrolPlanner planner,
        LayerMask doorLayer,
        float doorCheckHeight,
        float pathDoorCheckRadius,
        float doorDetectDistance,
        float doorOpenRange,
        float doorSearchRadius,
        float passThroughDistance,
        float reachExtra)
    {
        _transform = transform;
        _motor = motor;
        _planner = planner;
        _doorLayer = doorLayer;
        _doorCheckHeight = doorCheckHeight;
        _pathDoorCheckRadius = pathDoorCheckRadius;
        _doorDetectDistance = doorDetectDistance;
        _doorOpenRange = doorOpenRange;
        _doorSearchRadius = doorSearchRadius;
        _passThroughDistance = passThroughDistance;
        _reachExtra = reachExtra;
    }

    public void ClearThroughDoor()
    {
        ThroughDoorActive = false;
    }

    public bool UpdateThroughDoor()
    {
        if (!ThroughDoorActive)
            return false;

        _motor.SetDestinationUnsafe(ThroughDoorPoint);

        if (Vector3.Distance(_transform.position, ThroughDoorPoint) <= _reachExtra + 0.5f)
        {
            ThroughDoorActive = false;
            if (_planner.HasDestination)
                _motor.SetDestination(_planner.CurrentDestination, 3f);
            return true;
        }

        return true;
    }

    public bool HasClosedDoorBlocking()
    {
        if (!_planner.HasDestination)
            return false;

        if (EnemyDoorUtility.HasClosedDoorBetween(
                _transform.position, _planner.CurrentDestination,
                _doorLayer, _doorCheckHeight, _pathDoorCheckRadius))
            return true;

        return FindClosedDoorNearPathEnd() != null;
    }

    public bool TryHandleDoor()
    {
        if (!_planner.HasDestination || ThroughDoorActive)
            return false;

        Vector3 finalDest = _planner.CurrentDestination;

        DoorClick door = null;

        if (TryGetClosedDoorOnPath(out door) && door != null)
            return TryOpenWhenClose(door);

        if (EnemyDoorUtility.HasClosedDoorBetween(_transform.position, finalDest, _doorLayer, _doorCheckHeight, _pathDoorCheckRadius))
        {
            door = EnemyDoorUtility.FindBestClosedDoorTowardTarget(
                _transform.position, finalDest, _doorSearchRadius, _doorLayer);
            if (door == null)
            {
                door = EnemyDoorUtility.FindClosedDoorBetween(
                    _transform.position, finalDest, _doorLayer, _doorCheckHeight, _pathDoorCheckRadius);
            }

            if (door != null)
                return TryOpenWhenClose(door);
        }

        if (_motor.HasPath && _motor.PathStatus == NavMeshPathStatus.PathPartial)
        {
            door = FindClosedDoorNearPathEnd();
            if (door != null)
                return TryOpenWhenClose(door);
        }

        return false;
    }

    bool TryOpenWhenClose(DoorClick door)
    {
        if (door == null || door.IsOpen() || door.IsBroken())
        {
            if (door != null && door.IsOpen())
                BeginThroughDoor(door);
            return door != null && door.IsOpen();
        }

        Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(door.transform);
        float dist = Vector3.Distance(_transform.position, doorPos);
        float openRange = Mathf.Max(_doorOpenRange + 2f, _doorDetectDistance + 1f);

        if (dist > openRange)
            return false;

        if (!EnemyDoorUtility.TryOpenDoor(door, _transform.position))
            return false;

        BeginThroughDoor(door);
        return true;
    }

    void BeginThroughDoor(DoorClick door)
    {
        if (ThroughDoorActive)
            return;

        if (!TryGetPointBeyondDoor(door, out Vector3 beyondPoint))
            beyondPoint = _planner.CurrentDestination;

        ThroughDoorActive = true;
        ThroughDoorPoint = beyondPoint;
        _motor.Agent.ResetPath();
        _motor.SetDestinationUnsafe(beyondPoint);
    }

    bool TryGetPointBeyondDoor(DoorClick door, out Vector3 result)
    {
        result = default;
        if (door == null)
            return false;

        Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(door.transform);
        Vector3 toTarget = _planner.CurrentDestination - doorPos;
        toTarget.y = 0f;

        Vector3 doorForward = door.transform.forward;
        doorForward.y = 0f;

        for (int dirIndex = 0; dirIndex < 3; dirIndex++)
        {
            Vector3 dir;
            if (dirIndex == 0 && toTarget.sqrMagnitude > 0.01f)
                dir = toTarget.normalized;
            else if (dirIndex == 1 && doorForward.sqrMagnitude > 0.0001f)
                dir = doorForward.normalized;
            else if (dirIndex == 2 && doorForward.sqrMagnitude > 0.0001f)
                dir = -doorForward.normalized;
            else
                continue;

            for (float dist = _passThroughDistance; dist <= _passThroughDistance + 4f; dist += 0.5f)
            {
                Vector3 candidate = doorPos + dir * dist;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        return false;
    }

    bool TryGetClosedDoorOnPath(out DoorClick door)
    {
        door = null;
        if (_doorLayer.value == 0)
            return false;

        Vector3 dir = _motor.GetMoveDirection();
        if (dir.sqrMagnitude <= 0.0001f)
        {
            dir = _planner.CurrentDestination - _transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();
        }

        if (dir.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 origin = _transform.position + Vector3.up * _doorCheckHeight;
        float detectDist = Mathf.Max(_doorDetectDistance, _doorOpenRange + 2.5f);

        if (!Physics.SphereCast(origin, _pathDoorCheckRadius, dir, out RaycastHit hit, detectDist, _doorLayer, QueryTriggerInteraction.Collide))
            return false;

        door = hit.collider.GetComponentInParent<DoorClick>();
        if (door == null || door.IsOpen() || door.IsBroken())
            return false;

        return true;
    }

    DoorClick FindClosedDoorNearPathEnd()
    {
        if (_doorLayer.value == 0)
            return null;

        Vector3 pathEnd = _motor.GetPathEnd();
        Collider[] hits = Physics.OverlapSphere(pathEnd, 2f, _doorLayer, QueryTriggerInteraction.Collide);

        DoorClick bestDoor = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].GetComponentInParent<DoorClick>();
            if (door == null || door.IsOpen() || door.IsBroken()) continue;

            float dist = Vector3.Distance(_transform.position, EnemyDoorUtility.GetDoorWorldPosition(door.transform));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDoor = door;
            }
        }

        return bestDoor;
    }
}
