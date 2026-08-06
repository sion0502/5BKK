using UnityEngine;
using UnityEngine.AI;

public sealed class NavMotor
{
    readonly NavMeshAgent _agent;
    readonly NavMeshPath _path;

    public NavMotor(NavMeshAgent agent)
    {
        _agent = agent;
        _path = new NavMeshPath();
    }

    public NavMeshAgent Agent => _agent;
    public NavMeshPathStatus PathStatus => _agent.pathStatus;
    public bool HasPath => _agent.hasPath;
    public bool PathPending => _agent.pathPending;
    public Vector3 Velocity => _agent.velocity;

    Vector3 _activeDestination;
    bool _hasActiveDestination;

    Vector3 _progressAnchor;
    float _progressAnchorTime;

    public bool EnsureDestination(Vector3 target, float sampleRadius = 3f)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;

        _agent.isStopped = false;

        if (_hasActiveDestination &&
            GetHorizontalDistance(_activeDestination, hit.position) < 0.3f &&
            _agent.hasPath &&
            !_agent.pathPending)
            return true;

        _activeDestination = hit.position;
        _hasActiveDestination = true;
        _agent.SetDestination(hit.position);
        ResetProgressTracking();
        return true;
    }

    public bool SetDestination(Vector3 target, float sampleRadius = 3f)
    {
        _hasActiveDestination = false;
        return EnsureDestination(target, sampleRadius);
    }

    public bool SetDestinationUnsafe(Vector3 target)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        _agent.isStopped = false;
        _agent.SetDestination(target);
        ResetProgressTracking();
        return true;
    }

    public bool TrySetDestinationWithCompletePath(Vector3 target, float sampleRadius = 1.5f)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;

        if (!CalculatePath(hit.position, out NavMeshPathStatus status) || status != NavMeshPathStatus.PathComplete)
            return false;

        _agent.isStopped = false;
        _agent.SetDestination(hit.position);
        return true;
    }

    public bool CalculatePath(Vector3 target, out NavMeshPathStatus status)
    {
        status = NavMeshPathStatus.PathInvalid;

        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (!_agent.CalculatePath(target, _path))
            return false;

        status = _path.status;
        return true;
    }

    public bool HasCompletePath(Vector3 target)
    {
        return CalculatePath(target, out NavMeshPathStatus status) && status == NavMeshPathStatus.PathComplete;
    }

    public Vector3 GetCalculatedPathEnd()
    {
        if (_path.corners != null && _path.corners.Length > 0)
            return _path.corners[_path.corners.Length - 1];

        return _agent.transform.position;
    }

    public Vector3 GetPathEnd()
    {
        if (_agent.hasPath && _agent.path.corners != null && _agent.path.corners.Length > 0)
            return _agent.path.corners[_agent.path.corners.Length - 1];

        return GetCalculatedPathEnd();
    }

    public Vector3 GetMoveDirection()
    {
        if (_agent != null && _agent.hasPath)
        {
            Vector3 dir = _agent.steeringTarget - _agent.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = _agent.transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public bool HasReachedDestination(Vector3 destination, float extraDistance)
    {
        if (_agent.pathPending)
            return false;

        float reach = _agent.stoppingDistance + extraDistance;
        float flatDist = GetHorizontalDistance(_agent.transform.position, destination);

        if (flatDist <= reach)
            return true;

        if (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathPartial)
            return false;

        if (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            if (!float.IsNaN(_agent.remainingDistance) && !float.IsInfinity(_agent.remainingDistance) &&
                _agent.remainingDistance <= reach)
                return true;
        }

        if (!_agent.hasPath && flatDist <= reach * 4f)
            return true;

        if (_agent.velocity.sqrMagnitude < 0.12f && flatDist <= reach * 4f)
        {
            if (!_agent.hasPath)
                return true;

            if (!float.IsNaN(_agent.remainingDistance) && _agent.remainingDistance <= reach * 2f)
                return true;
        }

        return false;
    }

    public void Stop()
    {
        _agent.isStopped = true;
    }

    public void ResetPath()
    {
        _hasActiveDestination = false;
        _agent.ResetPath();
        ResetProgressTracking();
    }

    public void ResetProgressTracking()
    {
        if (_agent == null)
            return;

        _progressAnchor = _agent.transform.position;
        _progressAnchorTime = Time.time;
    }

    public void TickProgressTracking()
    {
        if (_agent == null)
            return;

        if (_progressAnchorTime <= 0f)
        {
            ResetProgressTracking();
            return;
        }

        if (GetHorizontalDistance(_progressAnchor, _agent.transform.position) >= 0.25f)
            ResetProgressTracking();
    }

    public bool IsNotMakingProgress(float windowSeconds = 0.35f, float minMoveDistance = 0.12f)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (_agent.pathPending)
            return false;

        if (Time.time - _progressAnchorTime < windowSeconds)
            return false;

        if (GetHorizontalDistance(_progressAnchor, _agent.transform.position) >= minMoveDistance)
            return false;

        if (_agent.pathStatus == NavMeshPathStatus.PathPartial)
            return true;

        if (!_agent.hasPath)
            return _agent.velocity.sqrMagnitude < 0.25f;

        return _agent.velocity.sqrMagnitude < 0.2f &&
               !float.IsNaN(_agent.remainingDistance) &&
               _agent.remainingDistance > _agent.stoppingDistance + 0.15f;
    }

    public bool IsIdleWithPendingDestination(Vector3 destination, float reachExtra, float windowSeconds = 0.4f, float minMoveDistance = 0.12f)
    {
        if (_agent == null || !_agent.isOnNavMesh)
            return false;

        if (_agent.pathPending)
            return false;

        float reach = _agent.stoppingDistance + reachExtra;
        if (GetHorizontalDistance(_agent.transform.position, destination) <= reach)
            return false;

        if (Time.time - _progressAnchorTime < windowSeconds)
            return false;

        if (GetHorizontalDistance(_progressAnchor, _agent.transform.position) >= minMoveDistance)
            return false;

        return _agent.velocity.sqrMagnitude < 0.25f;
    }

    public bool IsStuckAtPathEnd()
    {
        if (_agent.pathPending || !_agent.hasPath)
            return false;

        if (_agent.pathStatus != NavMeshPathStatus.PathPartial)
            return false;

        if (_agent.velocity.sqrMagnitude > 0.35f)
            return false;

        if (!float.IsNaN(_agent.remainingDistance) && _agent.remainingDistance > _agent.stoppingDistance + 1.2f)
            return false;

        return IsNotMakingProgress(0.25f, 0.08f);
    }

    public static float GetHorizontalDistance(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to);
    }
}
