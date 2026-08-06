using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 순찰 목적지 — PatrolPointZone 우선, 없으면 NavMesh 랜덤.
/// </summary>
public sealed class PatrolPlanner
{
    const float PatrolSnapRadius = 0.45f;

    readonly Transform _transform;
    readonly NavMotor _motor;
    readonly int _pickAttempts;
    readonly bool _autoOpenDoors;
    readonly LayerMask _doorLayer;
    readonly PatrolPointSelector _pointSelector;

    NavMeshTriangulation _triangulation;

    public Vector3 CurrentDestination { get; private set; }
    public bool HasDestination { get; private set; }

    public PatrolPlanner(
        Transform transform,
        NavMotor motor,
        int pickAttempts,
        bool autoOpenDoors,
        LayerMask doorLayer,
        float doorCheckHeight,
        float pathDoorCheckRadius,
        PatrolPointSelector pointSelector = null)
    {
        _transform = transform;
        _motor = motor;
        _pickAttempts = pickAttempts;
        _autoOpenDoors = autoOpenDoors;
        _doorLayer = doorLayer;
        _pointSelector = pointSelector;
    }

    public bool PickRandomDestination()
    {
        if (_pointSelector != null && _pointSelector.TryPickRandomPoint(out Vector3 pointDestination))
        {
            ApplyDestination(pointDestination);
            return true;
        }

        return PickRandomNavMeshDestination();
    }

    bool PickRandomNavMeshDestination()
    {
        CacheNavMesh();

        Vector3 previous = CurrentDestination;

        for (int i = 0; i < _pickAttempts; i++)
        {
            if (!TryGetRandomPoint(out Vector3 rawPoint))
                continue;

            if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                continue;

            if (HasDestination && NavMotor.GetHorizontalDistance(hit.position, previous) < 1f)
                continue;

            if (NavMotor.GetHorizontalDistance(_transform.position, hit.position) < 0.5f)
                continue;

            if (!IsDestinationReachable(hit.position))
                continue;

            ApplyDestination(hit.position);
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

    internal bool IsDestinationReachable(Vector3 point) =>
        IsDestinationReachable(_motor, point, _autoOpenDoors, _doorLayer);

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

    void CacheNavMesh()
    {
        _triangulation = NavMesh.CalculateTriangulation();
    }

    bool TryGetRandomPoint(out Vector3 result)
    {
        result = _transform.position;

        Vector3[] vertices = _triangulation.vertices;
        int[] indices = _triangulation.indices;
        if (vertices == null || indices == null || indices.Length < 3)
            return false;

        int triCount = indices.Length / 3;
        int triStart = Random.Range(0, triCount) * 3;

        Vector3 v0 = vertices[indices[triStart]];
        Vector3 v1 = vertices[indices[triStart + 1]];
        Vector3 v2 = vertices[indices[triStart + 2]];

        float r1 = Random.value;
        float r2 = Random.value;
        if (r1 + r2 > 1f)
        {
            r1 = 1f - r1;
            r2 = 1f - r2;
        }

        result = v0 + (v1 - v0) * r1 + (v2 - v0) * r2;
        return true;
    }
}
