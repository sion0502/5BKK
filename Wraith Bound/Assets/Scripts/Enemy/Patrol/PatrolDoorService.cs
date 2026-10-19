using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 순찰 중 Door 레이어 문에 가까워지면 DoorClick과 동일하게 열기.
/// </summary>
public sealed class PatrolDoorService
{
    readonly Transform _transform;
    readonly NavMotor _motor;
    readonly PatrolPlanner _planner;
    readonly LayerMask _doorLayer;
    readonly float _openDistance;
    DoorClick _openingDoor;
    float _openingDeadline;
    float _holdDestinationUntil;

    public PatrolDoorService(
        Transform transform,
        NavMotor motor,
        PatrolPlanner planner,
        LayerMask doorLayer,
        float openDistance)
    {
        _transform = transform;
        _motor = motor;
        _planner = planner;
        _doorLayer = doorLayer;
        _openDistance = openDistance;
    }

    public bool HoldDestination => Time.time < _holdDestinationUntil;

    public bool TryOpenNearbyDoor()
    {
        if (_openingDoor != null)
        {
            if (_openingDoor.IsPassageReady())
            {
                _openingDoor = null;
                _holdDestinationUntil = Time.time + 1.5f;
                ResumePatrolMovement();
                return false;
            }

            if (Time.time < _openingDeadline)
            {
                _motor.Stop();
                return true;
            }

            _openingDoor = null;
        }

        if (!_planner.HasDestination)
            return false;

        if (!TryFindDoorForDestination(out DoorClick door, out Vector3 doorPos))
            return false;

        float distance = NavMotor.GetHorizontalDistance(_transform.position, doorPos);
        bool atCut = _motor.HasPath &&
                     _motor.PathStatus == NavMeshPathStatus.PathPartial &&
                     NavMotor.GetHorizontalDistance(_transform.position, _motor.GetPathEnd()) <= 1.1f &&
                     NavMotor.GetHorizontalDistance(_motor.GetPathEnd(), doorPos) <= 2.5f;
        if (distance > Mathf.Max(_openDistance, 1.6f) && !atCut)
            return false;

        if (!EnemyDoorUtility.TryOpenDoor(door, _transform.position))
            return false;

        _openingDoor = door;
        _openingDeadline = Time.time + 8f;
        _motor.Stop();
        return true;
    }

    public bool IsApproachingDoorToDestination()
    {
        if (!_planner.HasDestination || !_motor.HasPath)
            return false;

        if (_motor.PathStatus != NavMeshPathStatus.PathPartial)
            return false;

        Vector3 pathEnd = _motor.GetPathEnd();
        if (NavMotor.GetHorizontalDistance(pathEnd, _planner.CurrentDestination) < 1f)
            return false;

        DoorClick door = EnemyDoorUtility.FindClosedDoorNearPosition(pathEnd, _doorLayer, 2f);
        return door != null && EnemyDoorUtility.IsDoorUsefulForTarget(
            _transform.position, EnemyDoorUtility.GetDoorWorldPosition(door.transform), _planner.CurrentDestination);
    }

    bool TryFindDoorForDestination(out DoorClick door, out Vector3 doorPos)
    {
        door = null;
        doorPos = Vector3.zero;
        Vector3 destination = _planner.CurrentDestination;

        DoorClick routeDoor = EnemyDoorUtility.FindClosedDoorOnRoute(_transform.position, destination, 2.5f);
        if (routeDoor != null)
        {
            Vector3 routePos = EnemyDoorUtility.GetDoorWorldPosition(routeDoor.transform);
            float distance = NavMotor.GetHorizontalDistance(_transform.position, routePos);
            bool pathCut = _motor.HasPath &&
                           _motor.PathStatus == NavMeshPathStatus.PathPartial &&
                           NavMotor.GetHorizontalDistance(_motor.GetPathEnd(), routePos) <= 2.5f;
            if (distance <= 3.5f || pathCut)
            {
                door = routeDoor;
                doorPos = routePos;
                return true;
            }
        }

        if (_doorLayer.value == 0)
            return false;

        DoorClick nearby = EnemyDoorUtility.FindClosedDoorNearPosition(
            _transform.position, _doorLayer, Mathf.Max(_openDistance, 1.6f));
        if (nearby == null)
            return false;

        Vector3 nearbyPos = EnemyDoorUtility.GetDoorWorldPosition(nearby.transform);
        if (!EnemyDoorUtility.IsDoorUsefulForTarget(_transform.position, nearbyPos, destination))
            return false;

        door = nearby;
        doorPos = nearbyPos;
        return true;
    }

    void ResumePatrolMovement()
    {
        _motor.Agent.isStopped = false;
        _motor.Agent.ResetPath();
        _motor.ResetProgressTracking();
        _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
    }
}
