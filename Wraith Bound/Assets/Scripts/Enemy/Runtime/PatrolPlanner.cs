using UnityEngine;
using UnityEngine.AI;

public sealed class PatrolPlanner
{
    readonly Transform _transform;
    readonly NavMotor _motor;
    readonly int _pickAttempts;
    readonly bool _autoOpenDoors;
    readonly LayerMask _doorLayer;
    readonly float _doorCheckHeight;
    readonly float _pathDoorCheckRadius;

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
        float pathDoorCheckRadius)
    {
        _transform = transform;
        _motor = motor;
        _pickAttempts = pickAttempts;
        _autoOpenDoors = autoOpenDoors;
        _doorLayer = doorLayer;
        _doorCheckHeight = doorCheckHeight;
        _pathDoorCheckRadius = pathDoorCheckRadius;
    }

    public void CacheNavMesh()
    {
        _triangulation = NavMesh.CalculateTriangulation();
    }

    public bool PickRandomDestination()
    {
        CacheNavMesh();

        Vector3 previous = CurrentDestination;

        for (int i = 0; i < _pickAttempts; i++)
        {
            if (!TryGetRandomPoint(out Vector3 rawPoint))
                continue;

            if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            if (HasDestination && NavMotor.GetHorizontalDistance(hit.position, previous) < 1f)
                continue;

            if (NavMotor.GetHorizontalDistance(_transform.position, hit.position) < 0.5f)
                continue;

            if (!IsReachablePatrolDestination(hit.position))
                continue;

            ApplyDestination(hit.position);
            return true;
        }

        for (int i = 0; i < 40; i++)
        {
            if (!TryGetRandomPoint(out Vector3 rawPoint))
                continue;

            if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                continue;

            if (NavMotor.GetHorizontalDistance(_transform.position, hit.position) < 0.5f)
                continue;

            if (!IsReachablePatrolDestination(hit.position))
                continue;

            ApplyDestination(hit.position);
            return true;
        }

        HasDestination = false;
        return false;
    }

    public void ApplyDestination(Vector3 point)
    {
        CurrentDestination = point;
        HasDestination = true;
        _motor.Agent.ResetPath();
        _motor.SetDestination(point, 3f);
    }

    public bool HasReached(float reachExtra)
    {
        if (!HasDestination)
            return false;

        return _motor.HasReachedDestination(CurrentDestination, reachExtra);
    }

    bool IsReachablePatrolDestination(Vector3 point)
    {
        if (!_motor.CalculatePath(point, out NavMeshPathStatus status))
            return false;

        Vector3 pathEnd = _motor.GetCalculatedPathEnd();

        // PathComplete = 같은 NavMesh 섬 / 도달 가능
        if (status == NavMeshPathStatus.PathComplete)
            return NavMotor.GetHorizontalDistance(pathEnd, point) <= 1.5f;

        // PathPartial = 닫힌 문 너머만 허용 (끊긴 NavMesh 섬·벽·틈은 거부)
        if (!_autoOpenDoors || status != NavMeshPathStatus.PathPartial)
            return false;

        DoorClick doorAtPathEnd = EnemyDoorUtility.FindClosedDoorNearPosition(pathEnd, _doorLayer, 2f);
        if (doorAtPathEnd == null)
            return false;

        Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(doorAtPathEnd.transform);
        if (NavMotor.GetHorizontalDistance(pathEnd, doorPos) > 2.5f)
            return false;

        if (!EnemyDoorUtility.HasClosedDoorBetween(
                _transform.position, point, _doorLayer, _doorCheckHeight, _pathDoorCheckRadius))
            return false;

        float agentToPoint = NavMotor.GetHorizontalDistance(_transform.position, point);
        float agentToEnd = NavMotor.GetHorizontalDistance(_transform.position, pathEnd);
        float pointToEnd = NavMotor.GetHorizontalDistance(point, pathEnd);

        if (agentToPoint <= agentToEnd + 2f)
            return false;

        if (pointToEnd < 2f)
            return false;

        return true;
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
