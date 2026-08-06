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

    public bool TryOpenNearbyDoor()
    {
        if (!_planner.HasDestination || _doorLayer.value == 0)
            return false;

        DoorClick door = EnemyDoorUtility.FindClosedDoorNearPosition(
            _transform.position, _doorLayer, _openDistance);
        if (door == null)
            return false;

        Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(door.transform);
        if (!EnemyDoorUtility.IsDoorUsefulForTarget(_transform.position, doorPos, _planner.CurrentDestination))
            return false;

        if (door.IsOpen())
        {
            ResumePatrolMovement();
            return true;
        }

        if (!EnemyDoorUtility.TryOpenDoor(door, _transform.position))
            return false;

        ResumePatrolMovement();
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

        return EnemyDoorUtility.IsDoorUsefulForTarget(
            _transform.position, pathEnd, _planner.CurrentDestination);
    }

    void ResumePatrolMovement()
    {
        _motor.Agent.isStopped = false;
        _motor.Agent.ResetPath();
        _motor.ResetProgressTracking();
        _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
    }
}
