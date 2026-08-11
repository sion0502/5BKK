using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 순찰 목적지 — PatrolPointZone의 PatrolPoint만 사용.
/// </summary>
public sealed class PatrolPlanner
{
    const float PatrolSnapRadius = 0.45f;

    readonly NavMotor _motor;
    readonly PatrolPointSelector _pointSelector;

    public Vector3 CurrentDestination { get; private set; }
    public bool HasDestination { get; private set; }

    public PatrolPlanner(NavMotor motor, PatrolPointSelector pointSelector)
    {
        _motor = motor;
        _pointSelector = pointSelector;
    }

    public bool PickRandomDestination()
    {
        if (_pointSelector != null && _pointSelector.TryPickRandomPoint(out Vector3 pointDestination))
        {
            ApplyDestination(pointDestination);
            return true;
        }

        HasDestination = false;
        return false;
    }

    public void ApplyDestination(Vector3 point)
    {
        Vector3 destination = point;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, PatrolSnapRadius, NavMesh.AllAreas))
            destination = hit.position;

        CurrentDestination = destination;
        HasDestination = true;
        _motor.Agent.ResetPath();
        _motor.SetDestination(destination, PatrolSnapRadius);
    }

    public bool HasReached(float reachExtra)
    {
        if (!HasDestination)
            return false;

        return _motor.HasReachedDestination(CurrentDestination, reachExtra);
    }

    internal static bool IsDestinationReachable(NavMotor motor, Vector3 point, bool autoOpenDoors, LayerMask doorLayer)
    {
        if (!motor.CalculatePath(point, out NavMeshPathStatus status))
            return false;

        Vector3 pathEnd = motor.GetCalculatedPathEnd();

        if (status == NavMeshPathStatus.PathComplete)
            return NavMotor.GetHorizontalDistance(pathEnd, point) <= 1f;

        if (!autoOpenDoors || status != NavMeshPathStatus.PathPartial)
            return false;

        DoorClick door = EnemyDoorUtility.FindClosedDoorNearPosition(pathEnd, doorLayer, 2.5f);
        if (door == null)
            return false;

        Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(door.transform);
        return NavMotor.GetHorizontalDistance(pathEnd, doorPos) <= 2.5f &&
               NavMotor.GetHorizontalDistance(point, doorPos) >= 0.75f;
    }
}
