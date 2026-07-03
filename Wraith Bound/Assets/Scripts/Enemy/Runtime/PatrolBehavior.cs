using UnityEngine;
using UnityEngine.AI;

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

    public void RestoreDestination(Vector3 destination)
    {
        _planner.ApplyDestination(destination);
        _door.ClearThroughDoor();
    }

    public bool PickRandomDestination()
    {
        _door.ClearThroughDoor();
        return _planner.PickRandomDestination();
    }

    public PatrolUpdateResult Update(float patrolSpeed)
    {
        _motor.Agent.speed = patrolSpeed;
        _motor.TickProgressTracking();

        if (_door.UpdateThroughDoor())
            return PatrolUpdateResult.ThroughDoor;

        if (!_planner.HasDestination)
            return PatrolUpdateResult.NeedDestination;

        if (_planner.HasReached(_reachExtra))
            return PatrolUpdateResult.ReachedDestination;

        bool stuckAtEnd = _motor.IsStuckAtPathEnd();
        bool notMoving = _motor.IsNotMakingProgress() ||
                         _motor.IsIdleWithPendingDestination(_planner.CurrentDestination, _reachExtra);
        bool blocked = stuckAtEnd || notMoving;

        // 벽/코너 PathPartial에서 멈춤 — 문 없으면 새 목적지
        if (blocked && !_door.HasClosedDoorBlocking())
            return PatrolUpdateResult.StuckNeedRepath;

        if (_door.HasClosedDoorBlocking())
        {
            if (_door.TryHandleDoor())
                return PatrolUpdateResult.HandlingDoor;

            if (blocked)
            {
                _motor.Stop();

                // 문까지 NavMesh가 끊기면 계속 idle만 하므로 새 목적지
                if (_motor.IsIdleWithPendingDestination(_planner.CurrentDestination, _reachExtra, 0.65f))
                    return PatrolUpdateResult.StuckNeedRepath;

                return PatrolUpdateResult.HandlingDoor;
            }
        }

        _motor.Agent.isStopped = false;
        _motor.EnsureDestination(_planner.CurrentDestination, 3f);

        return PatrolUpdateResult.Moving;
    }

    public enum PatrolUpdateResult
    {
        Moving,
        HandlingDoor,
        ThroughDoor,
        ReachedDestination,
        NeedDestination,
        StuckNeedRepath
    }
}
