using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemyCombat
{
    readonly EnemyBase _owner;
    readonly List<DoorClick> _chaseUnblockedDoors = new List<DoorClick>();

    public EnemyCombat(EnemyBase owner)
    {
        _owner = owner;
    }

    public void TickChase()
    {
        EnemyState s = _owner.RuntimeState;

        if (s.IsBusy)
            return;

        _owner.Agent.speed = GetChaseSpeed();
        _owner.Agent.autoBraking = false;
        PrepareChasePathThroughDoors(s.LastKnownPosition);
        _owner.HandleChaseSpecial();

        if (s.IsBusy)
            return;

        _owner.Agent.isStopped = false;

        if (s.CanDetectPlayer && !s.HiddenKillTargetActive)
        {
            s.HiddenKillTargetActive = false;
            s.ChaseGraceEndTime = 0f;

            if (_owner.Player != null && !(s.LastHeardPlayer && !s.LastSawPlayer))
                s.LastKnownPosition = _owner.Player.position;

            s.LastDetectTime = Time.time;
            SetChaseDestination(s.LastKnownPosition, true);
            _owner.LogAIThrottled("추적 중");
            return;
        }

        float limit = GetTargetLostTime();
        float graceEnd = GetChaseGraceEndTime(limit);
        float remaining = graceEnd - Time.time;

        if (s.HiddenKillTargetActive)
        {
            if (!IsPlayerHiding())
                s.HiddenKillTargetActive = false;
            else if (HasReachedLastKnownPosition())
            {
                s.HiddenKillTargetActive = false;
                _owner.LogAI("들킴 · 숨은 위치 도착 → Death");
                _owner.Sense.KillPlayerDirect();
                return;
            }
            else
            {
                SetChaseDestination(s.LastKnownPosition);
                _owner.Sense.TryKillOnColliderTouch();
                return;
            }
        }

        if (HasReachedLastKnownPosition())
        {
            _owner.LogAI("마지막 위치 도착 · 감지 없음 → 수색");
            StartInvestigateIfReady();
            return;
        }

        if (remaining <= 0f)
        {
            _owner.LogAI($"{limit:F0}초 추격 종료 · 순찰");
            ReturnToPatrolRoute();
            return;
        }

        SetChaseDestination(s.LastKnownPosition);
        _owner.LogAIThrottled($"추격 유지 · {remaining:F1}초 / 도착 시 수색");
    }

    public void ArmChaseGraceTimer()
    {
        EnemyState s = _owner.RuntimeState;
        float limit = GetTargetLostTime();
        s.LastDetectTime = Time.time;
        s.ChaseGraceEndTime = Time.time + limit;
        s.NextChaseRepathTime = 0f;
        s.LastChaseDestination = _owner.transform.position - Vector3.one * 999f;
    }

    float GetChaseGraceEndTime(float limit)
    {
        EnemyState s = _owner.RuntimeState;

        if (s.ChaseGraceEndTime > 0f)
            return s.ChaseGraceEndTime;

        return s.LastDetectTime + limit;
    }

    void StartInvestigateIfReady()
    {
        EnemyState s = _owner.RuntimeState;
        if (s.InvestigateRoutineRunning)
            return;

        _owner.StartCoroutine(BeginInvestigate());
    }

    bool HasReachedLastKnownPosition()
    {
        EnemyState s = _owner.RuntimeState;
        if (_owner.Agent != null && _owner.Agent.pathPending)
            return false;

        if (_owner.Agent != null && _owner.Agent.hasPath &&
            _owner.Agent.pathStatus == NavMeshPathStatus.PathPartial)
            return false;

        if (NavMesh.Raycast(
                _owner.transform.position,
                s.LastKnownPosition,
                out NavMeshHit navHit,
                NavMesh.AllAreas) &&
            NavMotor.GetHorizontalDistance(navHit.position, s.LastKnownPosition) > 0.6f)
            return false;

        float dist = Vector3.Distance(_owner.transform.position, s.LastKnownPosition);
        return dist <= _owner.InvestigateStartDistance ||
               (_owner.Agent.hasPath && !_owner.Agent.pathPending &&
                _owner.Agent.pathStatus == NavMeshPathStatus.PathComplete &&
                HasReachedDestination(_owner.InvestigateReachDistance));
    }

    float GetTargetLostTime() => Mathf.Max(0.01f, _owner.Data.targetLostTIme);

    public void TickInvestigate()
    {
        EnemyState s = _owner.RuntimeState;

        if (s.IsBusy) return;

        _owner.LogAIThrottled("수색 중");

        _owner.Agent.speed = GetPatrolSpeed();
        _owner.Agent.isStopped = false;

        if (IsClosedDoorOnCurrentPath(_owner.PatrolDoorFrontCheckDistance))
        {
            _owner.LogAI("수색 중 닫힌 문 감지 → 순찰 복귀");
            ReturnToPatrolRoute();
            return;
        }

        if (!s.ReachedLastKnownPosition)
        {
            if (HasClosedDoorBetween(_owner.transform.position, s.LastKnownPosition))
            {
                _owner.LogAI("수색 위치까지 닫힌 문 존재 → 순찰 복귀");
                ReturnToPatrolRoute();
                return;
            }

            SafeSetDestination(s.LastKnownPosition);

            if (!HasReachedDestination(_owner.InvestigateReachDistance))
                return;

            s.ReachedLastKnownPosition = true;
            s.InvestigateTimer = GetTargetLostTime();

            _owner.LogAI("마지막 위치 도착 완료 → 주변 랜덤 수색 시작");
            s.NextInvestigateRepathTime = Time.time + 0.5f;
            SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());
            StabilizeInvestigateRotation();
            return;
        }

        s.InvestigateTimer -= Time.deltaTime;

        if (s.InvestigateTimer <= 0f)
        {
            _owner.LogAI("수색 실패 → 순찰 복귀");
            ReturnToPatrolRoute();
            return;
        }

        if (!HasReachedDestination(_owner.InvestigateReachDistance))
        {
            StabilizeInvestigateRotation();
            return;
        }

        if (Time.time < s.NextInvestigateRepathTime)
        {
            StabilizeInvestigateRotation();
            return;
        }

        s.NextInvestigateRepathTime = Time.time + 1.4f;
        _owner.LogAI($"수색 중 → 남은 시간 {s.InvestigateTimer:F1}초, 다음 수색 지점 선택");
        SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());
        StabilizeInvestigateRotation();
    }

    void StabilizeInvestigateRotation()
    {
        NavMeshAgent agent = _owner.Agent;
        if (agent == null)
            return;

        agent.updateRotation = true;
    }

    public void TickObstacleAvoidance()
    {
        EnemyState s = _owner.RuntimeState;
        NavMeshAgent agent = _owner.Agent;

        if (agent == null || !agent.isOnNavMesh)
            return;

        if (s.IsBusy || agent.isStopped)
        {
            ResetObstacleStuckCheck();
            return;
        }

        if (s.CurrentState != EnemyBase.State.Patrol && s.CurrentState != EnemyBase.State.Investigate)
        {
            ResetObstacleStuckCheck();
            return;
        }

        if (!agent.hasPath || agent.pathPending || HasReachedDestination(_owner.PatrolReachDistance))
        {
            ResetObstacleStuckCheck();
            return;
        }

        float speed = (_owner.transform.position - s.LastObstacleCheckPosition).magnitude /
                      Mathf.Max(Time.deltaTime, 0.0001f);
        bool stuckBySpeed = speed <= _owner.ObstacleStuckSpeed;
        bool obstacleAhead = HasObstacleDirectlyAhead();
        bool blockedPath = obstacleAhead || agent.pathStatus != NavMeshPathStatus.PathComplete;

        s.LastObstacleCheckPosition = _owner.transform.position;

        if (!stuckBySpeed || !blockedPath)
        {
            s.ObstacleStuckTimer = 0f;
            return;
        }

        s.ObstacleStuckTimer += Time.deltaTime;

        if (s.ObstacleStuckTimer < _owner.ObstacleStuckTime)
            return;

        if (Time.time < s.NextObstacleAvoidTime)
            return;

        s.NextObstacleAvoidTime = Time.time + _owner.ObstacleAvoidCooldown;
        s.ObstacleStuckTimer = 0f;

        if (TrySetObstacleSideStepDestination())
        {
            _owner.LogAI("장애물에 막힘 → 옆 지점으로 우회");
            return;
        }

        if (s.CurrentState == EnemyBase.State.Patrol)
        {
            _owner.LogAI("장애물에 막힘 → 새 순찰 목적지 선택");
            _owner.SetNextGlobalPatDestination();
        }
        else if (s.CurrentState == EnemyBase.State.Investigate)
        {
            _owner.LogAI("장애물에 막힘 → 새 수색 지점 선택");
            SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());
        }
    }

    public void TickAnimation()
    {
        EnemyState s = _owner.RuntimeState;

        if (!_owner.HasPlayableAnimator) return;
        if (s.LockAnimator) return;

        switch (s.CurrentState)
        {
            case EnemyBase.State.Patrol:
                _owner.Anim.SetInteger(_owner.AnimStateHash, IsAgentVisuallyMoving() ? 1 : 0);
                break;

            case EnemyBase.State.Chase:
                _owner.Anim.SetInteger(_owner.AnimStateHash, 2);
                break;

            case EnemyBase.State.Investigate:
                _owner.Anim.SetInteger(_owner.AnimStateHash, IsAgentVisuallyMoving() ? 1 : 0);
                break;
        }
    }

    public void ChangeState(EnemyBase.State nextState)
    {
        EnemyState s = _owner.RuntimeState;

        if (s.CurrentState == nextState)
            return;

        EnemyBase.State prevState = s.CurrentState;
        s.CurrentState = nextState;

        if (prevState == EnemyBase.State.Chase && nextState != EnemyBase.State.Chase)
            RestoreChaseDoorCarves();

        switch (s.CurrentState)
        {
            case EnemyBase.State.Patrol:
                _owner.Agent.speed = GetPatrolSpeed();
                _owner.Agent.autoBraking = true;
                break;

            case EnemyBase.State.Chase:
                _owner.Agent.speed = GetChaseSpeed();
                _owner.Agent.autoBraking = false;
                s.NextChaseRepathTime = 0f;
                s.LastChaseDestination = _owner.transform.position - Vector3.one * 999f;
                break;

            case EnemyBase.State.Investigate:
                _owner.Agent.speed = GetPatrolSpeed();
                _owner.Agent.autoBraking = true;
                s.NextInvestigateRepathTime = 0f;
                break;

            default:
                _owner.Agent.updateRotation = true;
                break;
        }

        if (s.CurrentState != EnemyBase.State.Investigate)
            _owner.Agent.updateRotation = true;

        _owner.LogAI($"상태 변경: {prevState} → {s.CurrentState}");

        if (!s.LockAnimator)
            TickAnimation();
    }

    public DoorBrokenTest GetClosedDoorOnChasePath(float distance)
    {
        EnemyState s = _owner.RuntimeState;
        Vector3 origin = _owner.transform.position + Vector3.up * _owner.DoorCheckHeight;
        Vector3 targetDir = GetChaseTargetDirection();

        DoorBrokenTest directTargetDoor = GetClosedBreakableDoorInDirection(origin, targetDir, distance);
        if (directTargetDoor != null)
            return directTargetDoor;

        Vector3 dir = GetChaseMoveDirection();
        DoorBrokenTest moveDirectionDoor = GetClosedBreakableDoorInDirection(origin, dir, distance);
        if (moveDirectionDoor != null)
            return moveDirectionDoor;

        return FindBestClosedBreakableDoorTowardTarget(s.LastKnownPosition, _owner.ChaseDoorSearchRadius);
    }

    public void ReturnToPatrolRoute()
    {
        EnemyState s = _owner.RuntimeState;

        s.TargetLostActive = false;
        s.DoorSpecialAllowed = false;
        s.HiddenKillTargetActive = false;
        s.ChaseGraceEndTime = 0f;

        ChangeState(EnemyBase.State.Patrol);
        _owner.SetNextGlobalPatDestination();
    }

    public bool SafeSetDestination(Vector3 target)
    {
        if (_owner.NavMotor != null)
            return _owner.NavMotor.TrySetDestinationWithCompletePath(target);

        if (!_owner.Agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;

        if (!HasCompletePath(hit.position))
            return false;

        _owner.Agent.SetDestination(hit.position);
        return true;
    }

    IEnumerator BeginInvestigate()
    {
        EnemyState s = _owner.RuntimeState;

        s.InvestigateRoutineRunning = true;
        s.IsBusy = true;

        _owner.Agent.isStopped = true;
        _owner.Agent.ResetPath();

        yield return new WaitForSeconds(0.15f);

        // Detection can resume during the short pause; do not overwrite a renewed chase.
        if (s.CanDetectPlayer || s.HiddenKillTargetActive ||
            PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            s.IsBusy = false;
            s.InvestigateRoutineRunning = false;
            if (!PlayerDeathDebug.IsDying && !PlayerDeathDebug.IsDead)
                _owner.Agent.isStopped = false;
            yield break;
        }

        ChangeState(EnemyBase.State.Investigate);
        _owner.LogAI("수색모드 시작: 마지막 위치 주변 5m 수색");

        // BeginInvestigate starts after reaching the last known position.
        // Start the search countdown now, alongside the first random destination.
        s.ReachedLastKnownPosition = true;
        s.InvestigateTimer = GetTargetLostTime();

        _owner.Agent.speed = GetPatrolSpeed();
        _owner.Agent.isStopped = false;

        SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());

        s.IsBusy = false;
        s.InvestigateRoutineRunning = false;
    }

    void SetChaseDestination(Vector3 target, bool force = false)
    {
        EnemyState s = _owner.RuntimeState;

        if (!force && Time.time < s.NextChaseRepathTime)
            return;

        if (!force &&
            Vector3.Distance(s.LastChaseDestination, target) < _owner.ChaseRepathDistance)
            return;

        s.NextChaseRepathTime = Time.time + _owner.ChaseDestinationUpdateInterval;
        s.LastChaseDestination = target;

        SetChaseDestinationInternal(target);
    }

    void SetChaseDestinationInternal(Vector3 target)
    {
        PrepareChasePathThroughDoors(target);

        if (!TryResolveChaseNavPoint(target, out Vector3 dest))
            dest = target;

        if (_owner.NavMotor != null)
        {
            if (_owner.NavMotor.EnsureDestination(dest, 0.75f))
                return;
        }

        if (!_owner.Agent.isOnNavMesh)
            return;

        if (!NavMesh.SamplePosition(dest, out NavMeshHit hit, 0.75f, NavMesh.AllAreas) &&
            !NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas))
            return;

        _owner.Agent.isStopped = false;
        _owner.Agent.SetDestination(hit.position);
    }

    bool TryResolveChaseNavPoint(Vector3 target, out Vector3 dest)
    {
        dest = target;
        Vector3 best = default;
        float bestScore = float.MaxValue;
        bool found = false;

        TryScoreChaseSample(target, 0.5f, ref found, ref best, ref bestScore);
        TryScoreChaseSample(target, 1.2f, ref found, ref best, ref bestScore);
        TryScoreChaseSample(target, 2f, ref found, ref best, ref bestScore);
        TryScoreChaseSample(target, 3f, ref found, ref best, ref bestScore);

        Vector3[] offsets =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            (Vector3.forward + Vector3.right).normalized,
            (Vector3.forward + Vector3.left).normalized,
            (Vector3.back + Vector3.right).normalized,
            (Vector3.back + Vector3.left).normalized
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            TryScoreChaseSample(target + offsets[i] * 0.9f, 0.8f, ref found, ref best, ref bestScore);
            TryScoreChaseSample(target + offsets[i] * 1.6f, 0.8f, ref found, ref best, ref bestScore);
        }

        if (!found)
            return false;

        dest = PushChasePointOffWall(best);
        return true;
    }

    void TryScoreChaseSample(
        Vector3 point,
        float radius,
        ref bool found,
        ref Vector3 best,
        ref float bestScore)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return;

        Vector3 sampled = hit.position;
        if (IsChaseSampleOnWrongSideOfWall(sampled, point))
            return;

        if (_owner.NavMotor == null || !_owner.NavMotor.CalculatePath(sampled, out NavMeshPathStatus status))
            return;

        if (status == NavMeshPathStatus.PathInvalid)
            return;

        if (status == NavMeshPathStatus.PathPartial)
        {
            float sampleToAgent = NavMotor.GetHorizontalDistance(_owner.transform.position, sampled);
            float sampleToTarget = NavMotor.GetHorizontalDistance(sampled, point);
            if (sampleToAgent + 0.5f < sampleToTarget && !IsClosedDoorOnChaseRoute(sampled))
                return;
        }

        float score = NavMotor.GetHorizontalDistance(sampled, point);
        if (status == NavMeshPathStatus.PathComplete)
            score -= 0.5f;

        if (found && score >= bestScore)
            return;

        found = true;
        best = sampled;
        bestScore = score;
    }

    bool IsChaseSampleOnWrongSideOfWall(Vector3 navPoint, Vector3 target)
    {
        Vector3 from = navPoint + Vector3.up;
        Vector3 to = target + Vector3.up;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.35f)
            return false;

        int mask = (1 << 0) | _owner.ObstacleLayer.value;
        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;

        if (!Physics.Raycast(from, delta / dist, out RaycastHit hit, dist - 0.12f, mask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider.GetComponentInParent<HidingSpot>() != null)
            return false;
        if (hit.collider.GetComponentInParent<DoorClick>() != null)
            return false;
        if (_owner.Player != null &&
            (hit.transform == _owner.Player || hit.transform.IsChildOf(_owner.Player)))
            return false;

        return true;
    }

    bool IsClosedDoorOnChaseRoute(Vector3 target)
    {
        return EnemyDoorUtility.FindClosedDoorBetween(
                   _owner.transform.position,
                   target,
                   _owner.DoorLayer,
                   _owner.DoorCheckHeight,
                   _owner.PathDoorCheckRadius) != null ||
               EnemyDoorUtility.FindClosedDoorOnRoute(_owner.transform.position, target) != null;
    }

    Vector3 PushChasePointOffWall(Vector3 pos)
    {
        if (EnemyDoorUtility.FindClosedDoorNearPosition(pos, _owner.DoorLayer, 1.4f) != null)
            return pos;

        float want = Mathf.Max(0.28f, _owner.Agent.radius + 0.12f);
        if (!NavMesh.FindClosestEdge(pos, out NavMeshHit edge, NavMesh.AllAreas))
            return pos;
        if (edge.distance >= want)
            return pos;

        Vector3 pushed = pos + edge.normal * (want - edge.distance);
        if (NavMesh.SamplePosition(pushed, out NavMeshHit hit, 0.8f, NavMesh.AllAreas))
            return hit.position;

        return pos;
    }

    void PrepareChasePathThroughDoors(Vector3 target)
    {
        DoorClick door = EnemyDoorUtility.FindClosedDoorBetween(
            _owner.transform.position,
            target,
            _owner.DoorLayer,
            _owner.DoorCheckHeight,
            _owner.PathDoorCheckRadius);

        if (door == null)
            door = EnemyDoorUtility.FindClosedDoorOnRoute(_owner.transform.position, target, 3f);

        if (door == null)
            return;

        DoorNavMeshUtility.SetNavMeshBlocked(door.transform, false);
        if (!_chaseUnblockedDoors.Contains(door))
            _chaseUnblockedDoors.Add(door);
    }

    void RestoreChaseDoorCarves()
    {
        for (int i = 0; i < _chaseUnblockedDoors.Count; i++)
        {
            DoorClick door = _chaseUnblockedDoors[i];
            if (door == null || door.IsOpen() || door.IsBroken())
                continue;

            DoorNavMeshUtility.SetNavMeshBlocked(door.transform, true);
        }

        _chaseUnblockedDoors.Clear();
    }

    void SetRandomInvestigatePointAround(Vector3 center, float radius)
    {
        EnemyState s = _owner.RuntimeState;

        if (!_owner.Agent.isOnNavMesh) return;

        if (radius <= 0f)
            radius = 0.05f;

        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * radius;
            randomDir.y = 0f;

            Vector3 pos = center + randomDir;

            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, radius, NavMesh.AllAreas))
                continue;

            if (!IsValidDestination(hit.position, _owner.MinWallClearance * 0.5f))
                continue;

            if (HasClosedDoorBetween(_owner.transform.position, hit.position))
                continue;

            if (!HasCompletePath(hit.position))
                continue;

            SafeSetDestination(hit.position);
            s.CurrentPatrolDestination = hit.position;
            s.HasPatDestination = true;
            return;
        }

        SafeSetDestination(center);
        s.CurrentPatrolDestination = center;
        s.HasPatDestination = false;
    }

    bool IsValidDestination(Vector3 point, float clearance)
    {
        if (!NavMesh.SamplePosition(point, out NavMeshHit sampleHit, 1.0f, NavMesh.AllAreas))
            return false;

        if (NavMesh.FindClosestEdge(sampleHit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
        {
            if (edgeHit.distance < clearance)
                return false;
        }

        return true;
    }

    bool HasCompletePath(Vector3 target)
    {
        if (_owner.NavMotor != null)
            return _owner.NavMotor.HasCompletePath(target);

        NavMeshPath path = new NavMeshPath();
        if (!_owner.Agent.CalculatePath(target, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    bool SetPatrolDestination(Vector3 target)
    {
        if (_owner.NavMotor != null)
            return _owner.NavMotor.SetDestination(target);

        if (!_owner.Agent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;

        _owner.Agent.SetDestination(hit.position);
        return true;
    }

    bool HasReachedDestination(float extraDistance)
    {
        if (_owner.Agent.pathPending) return false;
        if (!_owner.Agent.hasPath) return true;

        float reachDistance = _owner.Agent.stoppingDistance + extraDistance;
        return _owner.Agent.remainingDistance <= reachDistance;
    }

    bool HasClosedDoorBetween(Vector3 from, Vector3 to)
    {
        return EnemyDoorUtility.HasClosedDoorBetween(
            from, to, _owner.DoorLayer, _owner.DoorCheckHeight, _owner.PathDoorCheckRadius);
    }

    bool IsClosedDoorOnCurrentPath(float distance)
    {
        return TryGetClosedDoorOnCurrentPath(distance, out _, out _);
    }

    bool TryGetClosedDoorOnCurrentPath(float distance, out DoorClick door, out RaycastHit doorHit)
    {
        door = null;
        doorHit = default;

        if (_owner.DoorLayer.value == 0)
            return false;

        if (_owner.Agent == null || !_owner.Agent.hasPath)
            return false;

        Vector3 origin = _owner.transform.position + Vector3.up * _owner.DoorCheckHeight;
        Vector3 dir = GetAgentMoveDirection();

        if (dir.sqrMagnitude <= 0.0001f)
            return false;

        if (!Physics.SphereCast(
                origin,
                _owner.PathDoorCheckRadius,
                dir.normalized,
                out RaycastHit hit,
                distance,
                _owner.DoorLayer,
                QueryTriggerInteraction.Collide))
            return false;

        door = hit.collider.GetComponentInParent<DoorClick>();
        if (door == null) return false;
        if (door.IsOpen() || door.IsBroken()) return false;
        if (!IsCurrentDestinationBeyondDoor(hit.point, dir.normalized))
            return false;

        doorHit = hit;
        return true;
    }

    bool IsCurrentDestinationBeyondDoor(Vector3 doorPoint, Vector3 moveDirection)
    {
        EnemyState s = _owner.RuntimeState;
        Vector3 target = s.CurrentPatrolDestination;

        if (s.CurrentState == EnemyBase.State.Investigate)
            target = s.LastKnownPosition;

        Vector3 toTarget = target - _owner.transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.01f)
            return false;

        float targetProjection = Vector3.Dot(toTarget, moveDirection);
        float doorProjection = Vector3.Dot(doorPoint - _owner.transform.position, moveDirection);

        return targetProjection > doorProjection + 0.5f;
    }

    DoorBrokenTest GetClosedBreakableDoorInDirection(Vector3 origin, Vector3 dir, float distance)
    {
        if (_owner.DoorLayer.value == 0)
            return null;

        if (dir.sqrMagnitude <= 0.0001f)
            return null;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            0.25f,
            dir,
            distance,
            _owner.DoorLayer,
            QueryTriggerInteraction.Collide
        );

        float nearest = float.MaxValue;
        DoorBrokenTest result = null;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick click = hits[i].collider.GetComponentInParent<DoorClick>();
            if (click == null) continue;
            if (click.IsOpen() || click.IsBroken()) continue;

            DoorBrokenTest broken = hits[i].collider.GetComponentInParent<DoorBrokenTest>();
            if (broken == null || broken.IsBroken()) continue;

            if (hits[i].distance < nearest)
            {
                nearest = hits[i].distance;
                result = broken;
            }
        }

        return result;
    }

    DoorBrokenTest FindBestClosedBreakableDoorTowardTarget(Vector3 target, float searchRadius)
    {
        if (_owner.DoorLayer.value == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(
            _owner.transform.position,
            searchRadius,
            _owner.DoorLayer,
            QueryTriggerInteraction.Collide
        );

        DoorBrokenTest bestDoor = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick click = hits[i].GetComponentInParent<DoorClick>();
            if (click == null) continue;
            if (click.IsOpen() || click.IsBroken()) continue;

            DoorBrokenTest broken = hits[i].GetComponentInParent<DoorBrokenTest>();
            if (broken == null || broken.IsBroken()) continue;

            Vector3 doorPos = EnemyDoorUtility.GetDoorWorldPosition(click.transform);
            if (!EnemyDoorUtility.IsDoorUsefulForTarget(_owner.transform.position, doorPos, target))
                continue;

            float score = Vector3.Distance(_owner.transform.position, doorPos) +
                          Vector3.Distance(doorPos, target);
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = broken;
            }
        }

        return bestDoor;
    }

    Vector3 GetChaseTargetDirection()
    {
        EnemyState s = _owner.RuntimeState;
        Vector3 target = s.LastKnownPosition;

        if (s.CanDetectPlayer && _owner.Player != null)
            target = _owner.Player.position;

        Vector3 dir = target - _owner.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        return Vector3.zero;
    }

    Vector3 GetChaseMoveDirection()
    {
        if (_owner.Agent != null && _owner.Agent.hasPath)
        {
            Vector3 dir = _owner.Agent.steeringTarget - _owner.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        if (_owner.Player != null)
        {
            Vector3 dir = _owner.Player.position - _owner.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = _owner.transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    Vector3 GetAgentMoveDirection()
    {
        if (_owner.NavMotor != null)
            return _owner.NavMotor.GetMoveDirection();

        if (_owner.Agent != null && _owner.Agent.hasPath)
        {
            Vector3 dir = _owner.Agent.steeringTarget - _owner.transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = _owner.transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    void ResetObstacleStuckCheck()
    {
        EnemyState s = _owner.RuntimeState;
        s.ObstacleStuckTimer = 0f;
        s.LastObstacleCheckPosition = _owner.transform.position;
    }

    bool HasObstacleDirectlyAhead()
    {
        int mask = GetObstacleAvoidanceMask();
        if (mask == 0) return false;

        Vector3 origin = _owner.transform.position + Vector3.up * _owner.DoorCheckHeight;
        Vector3 direction = GetAgentMoveDirection();

        if (direction.sqrMagnitude <= 0.0001f)
            direction = _owner.transform.forward;

        return Physics.SphereCast(
            origin,
            _owner.ObstacleCheckRadius,
            direction.normalized,
            out _,
            _owner.ObstacleCheckDistance,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    int GetObstacleAvoidanceMask()
    {
        int mask = _owner.ObstacleLayer.value;

        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;

        int obstacleNamedLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleNamedLayer >= 0)
            mask |= 1 << obstacleNamedLayer;

        if (_owner.DoorLayer.value != 0)
            mask &= ~_owner.DoorLayer.value;

        if (_owner.Data != null && _owner.Data.playerLayer.value != 0)
            mask &= ~_owner.Data.playerLayer.value;

        return mask;
    }

    bool TrySetObstacleSideStepDestination()
    {
        EnemyState s = _owner.RuntimeState;

        Vector3 forward = GetAgentMoveDirection();
        if (forward.sqrMagnitude <= 0.0001f)
            forward = _owner.transform.forward;

        forward.y = 0f;
        forward.Normalize();

        Vector3[] directions =
        {
            Quaternion.Euler(0f, 70f, 0f) * forward,
            Quaternion.Euler(0f, -70f, 0f) * forward,
            Quaternion.Euler(0f, 35f, 0f) * forward,
            Quaternion.Euler(0f, -35f, 0f) * forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 candidate = _owner.transform.position + directions[i] * _owner.ObstacleSideStepDistance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _owner.ObstacleSideStepDistance,
                    NavMesh.AllAreas))
                continue;

            if (!IsValidDestination(hit.position, _owner.MinWallClearance * 0.5f))
                continue;

            if (HasClosedDoorBetween(_owner.transform.position, hit.position))
                continue;

            if (!SetPatrolDestination(hit.position))
                continue;

            s.CurrentPatrolDestination = hit.position;
            s.HasPatDestination = true;
            return true;
        }

        return false;
    }

    bool IsAgentVisuallyMoving()
    {
        if (_owner.Agent == null)
            return false;

        if (_owner.Agent.isStopped)
            return false;

        return _owner.Agent.velocity.sqrMagnitude > 0.05f;
    }

    bool IsPlayerHiding()
    {
        return _owner.PlayerHidingController != null && _owner.PlayerHidingController.isHiding;
    }

    float GetPatrolSpeed() => _owner.Data.moveSpeed * 0.5f;
    float GetChaseSpeed() => _owner.Data.moveSpeed;
    float GetInvestigateRadius() => Mathf.Max(5f, _owner.InvestigateRadius);
}
