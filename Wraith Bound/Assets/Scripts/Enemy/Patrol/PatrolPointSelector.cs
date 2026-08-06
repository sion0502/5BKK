using UnityEngine;
using UnityEngine.AI;

public sealed class PatrolPointSelector
{
    const float PatrolSnapRadius = 0.45f;

    readonly Transform _transform;
    readonly NavMotor _motor;
    readonly PatrolPointZone _zone;
    readonly bool _autoOpenDoors;
    readonly LayerMask _doorLayer;

    PatrolPoint _lastPoint;

    public PatrolPointSelector(
        Transform transform,
        NavMotor motor,
        PatrolPointZone zone,
        bool autoOpenDoors,
        LayerMask doorLayer)
    {
        _transform = transform;
        _motor = motor;
        _zone = zone;
        _autoOpenDoors = autoOpenDoors;
        _doorLayer = doorLayer;
    }

    public bool TryPickRandomPoint(out Vector3 destination)
    {
        destination = _transform.position;

        if (_zone == null || !_zone.HasPoints)
            return false;

        PatrolPoint[] candidates = _zone.Points;
        int attempts = candidates.Length * 3;

        for (int i = 0; i < attempts; i++)
        {
            PatrolPoint candidate = candidates[Random.Range(0, candidates.Length)];
            if (candidate == null)
                continue;

            if (candidate == _lastPoint && candidates.Length > 1)
                continue;

            if (TryAcceptPoint(candidate.Position, out destination))
            {
                _lastPoint = candidate;
                return true;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null)
                continue;

            if (TryAcceptPoint(candidates[i].Position, out destination))
            {
                _lastPoint = candidates[i];
                return true;
            }
        }

        return false;
    }

    bool TryAcceptPoint(Vector3 raw, out Vector3 destination)
    {
        destination = raw;

        if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, PatrolSnapRadius, NavMesh.AllAreas))
            return false;

        if (NavMotor.GetHorizontalDistance(_transform.position, hit.position) < 0.35f)
            return false;

        if (!PatrolPlanner.IsDestinationReachable(_motor, hit.position, _autoOpenDoors, _doorLayer))
            return false;

        destination = hit.position;
        return true;
    }
}
