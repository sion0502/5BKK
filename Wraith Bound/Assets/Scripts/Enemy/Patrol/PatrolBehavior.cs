using UnityEngine;
using UnityEngine.AI;

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
    public string CurrentPointLabel => _planner.CurrentPointLabel;
    public string DebugStatus { get; private set; } = "대기";

    public bool PickRandomDestination() => _planner.PickRandomDestination();

    public PatrolUpdateResult Update(float patrolSpeed, bool autoOpenDoors)
    {
        _motor.Agent.speed = patrolSpeed;
        _motor.TickProgressTracking();

        if (!_planner.HasDestination)
        {
            DebugStatus = "선택 중";
            return PatrolUpdateResult.NeedDestination;
        }

        if (_planner.HasReached(_reachExtra))
        {
            DebugStatus = "도착";
            return PatrolUpdateResult.ReachedDestination;
        }

        if (autoOpenDoors && _door.TryOpenNearbyDoor())
        {
            DebugStatus = "문 여는 중";
            return PatrolUpdateResult.HandlingDoor;
        }

        if (autoOpenDoors && _door.HoldDestination)
        {
            DebugStatus = "문 통과";
            if (_motor.PathStatus == NavMeshPathStatus.PathComplete)
            {
                _motor.Agent.isStopped = false;
                _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
            }
            else
                _motor.Stop();

            return PatrolUpdateResult.Moving;
        }

        if (IsPressedAgainstCut())
        {
            if (autoOpenDoors && _door.IsApproachingDoorToDestination())
            {
                DebugStatus = "문 앞 대기";
                _motor.Stop();
                return PatrolUpdateResult.HandlingDoor;
            }

            DebugStatus = "막힘";
            _motor.Stop();
            return PatrolUpdateResult.StuckNeedRepath;
        }

        bool blocked = _motor.IsStuckAtPathEnd() ||
                       _motor.IsNotMakingProgress() ||
                       _motor.IsIdleWithPendingDestination(_planner.CurrentDestination, _reachExtra);

        if (blocked)
        {
            if (autoOpenDoors && _door.HoldDestination)
            {
                _motor.Agent.isStopped = false;
                _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
                return PatrolUpdateResult.Moving;
            }

            DebugStatus = "막힘";
            _motor.Stop();
            return PatrolUpdateResult.StuckNeedRepath;
        }

        DebugStatus = _motor.PathStatus == NavMeshPathStatus.PathComplete ? "이동" : "문까지 이동";
        _motor.Agent.isStopped = false;
        _motor.EnsureDestination(_planner.CurrentDestination, 0.5f);
        return PatrolUpdateResult.Moving;
    }

    bool IsPressedAgainstCut()
    {
        if (!_motor.HasPath || _motor.PathPending || _motor.PathStatus != NavMeshPathStatus.PathPartial)
            return false;

        Vector3 end = _motor.GetPathEnd();
        return NavMotor.GetHorizontalDistance(_motor.Agent.transform.position, end) <= 0.85f;
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
