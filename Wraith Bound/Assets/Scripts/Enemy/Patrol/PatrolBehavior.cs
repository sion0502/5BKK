using UnityEngine;

/// <summary>
/// 순찰 틱 — 이동, 도착, 가까운 Door 레이어 문 열기.
/// </summary>
public sealed class PatrolBehavior
{
    readonly NavMotor _motor;
    readonly PatrolPlanner _planner;
    readonly PatrolDoorService _door;
    readonly float _reachExtra;

    public PatrolBehavior(
        NavMotor motor,
        PatrolPlanner planner,
        PatrolDoorService door,
        float reachExtra)
    {
        _motor = motor;
        _planner = planner;
        _door = door;
        _reachExtra = reachExtra;
    }

    public Vector3 CurrentDestination => _planner.CurrentDestination;
    public bool HasDestination => _planner.HasDestination;

    public bool PickRandomDestination() => _planner.PickRandomDestination();

    public PatrolUpdateResult Update(float patrolSpeed, bool autoOpenDoors)
    {
        _motor.Agent.speed = patrolSpeed;
        _motor.TickProgressTracking();

        if (!_planner.HasDestination)
            return PatrolUpdateResult.NeedDestination;

        if (_planner.HasReached(_reachExtra))
            return PatrolUpdateResult.ReachedDestination;

        if (autoOpenDoors && _door.TryOpenNearbyDoor())
            return PatrolUpdateResult.HandlingDoor;

        bool blocked = _motor.IsStuckAtPathEnd() ||
                       _motor.IsNotMakingProgress() ||
                       _motor.IsIdleWithPendingDestination(_planner.CurrentDestination, _reachExtra);

        if (blocked)
        {
            if (autoOpenDoors && _door.IsApproachingDoorToDestination())
            {
                _motor.Agent.isStopped = false;
                _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
                return PatrolUpdateResult.Moving;
            }

            return PatrolUpdateResult.StuckNeedRepath;
        }

        _motor.Agent.isStopped = false;
        _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
        return PatrolUpdateResult.Moving;
    }

    public enum PatrolUpdateResult
    {
        Moving,
        HandlingDoor,
        ReachedDestination,
        NeedDestination,
        StuckNeedRepath
    }
}
