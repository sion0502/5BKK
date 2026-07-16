using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemyCombat
{
    readonly EnemyBase _owner;

    public EnemyCombat(EnemyBase owner)
    {
        _owner = owner;
    }

    public void TickChase()
    {
        EnemyState s = _owner.RuntimeState;

        if (s.IsBusy) return;

        _owner.LogAIThrottled("추적 중");

        _owner.Agent.speed = GetChaseSpeed();
        _owner.HandleChaseSpecial();

        if (s.IsBusy) return;

        _owner.Agent.isStopped = false;

        if (s.CanDetectPlayer)
        {
            if (_owner.Player != null && !(s.LastHeardPlayer && !s.LastSawPlayer))
                s.LastKnownPosition = _owner.Player.position;

            SetChaseDestination(s.LastKnownPosition, true);

            if (s.LastSawPlayer && s.LastHeardPlayer)
                _owner.LogSenseThrottled("시야 + 소리 둘 다 감지 CHASE 중");
            else if (s.LastSawPlayer)
                _owner.LogSenseThrottled("시야만 감지 CHASE 중");
            else if (s.LastHeardPlayer)
                _owner.LogSenseThrottled("소리만 감지 CHASE 중");

            return;
        }

        if (s.HiddenKillTargetActive)
        {
            if (!IsPlayerHiding())
            {
                s.HiddenKillTargetActive = false;
                _owner.LogAI("대놓고 숨은 플레이어가 숨기 해제 → 즉사 예약 취소");
            }
            else
            {
                float dist = Vector3.Distance(_owner.transform.position, s.LastKnownPosition);

                if (dist <= _owner.PlayerCatchDistance)
                {
                    _owner.LogAI("대놓고 숨은 위치 도착 → Player Dead");
                    _owner.Sense.KillPlayerDirect();
                    return;
                }

                SetChaseDestination(s.LastKnownPosition);
                _owner.LogAIThrottled("대놓고 숨은 플레이어 위치까지 추격 중");
                return;
            }
        }

        if (s.HiddenSearchTargetActive)
        {
            float dist = Vector3.Distance(_owner.transform.position, s.LastKnownPosition);

            if (dist <= _owner.InvestigateStartDistance ||
                HasReachedDestination(_owner.InvestigateReachDistance))
            {
                _owner.LogAI("플레이어 숨은 위치 도착 → 수색모드 시작");

                if (!s.InvestigateRoutineRunning)
                    _owner.StartCoroutine(BeginInvestigate());

                return;
            }

            SetChaseDestination(s.LastKnownPosition);
            _owner.LogAIThrottled("시야/소리 감지 X → 플레이어가 숨은 마지막 위치까지 추격 중");
            return;
        }

        float lostTime = Time.time - s.LastDetectTime;

        if (lostTime < _owner.Data.targetLostTIme)
        {
            if (!s.TargetLostActive)
            {
                s.TargetLostActive = true;
                _owner.LogAI("시야/소리 감지 X → targetLostTime 발동");
            }

            float distToPlayer = Vector3.Distance(_owner.transform.position, s.LastKnownPosition);

            if (distToPlayer <= _owner.InvestigateStartDistance ||
                HasReachedDestination(_owner.InvestigateReachDistance))
            {
                _owner.LogAI("targetLostTime 중 플레이어 위치 도달 → 수색 시작");

                if (!s.InvestigateRoutineRunning)
                    _owner.StartCoroutine(BeginInvestigate());

                return;
            }

            SetChaseDestination(s.LastKnownPosition);
            _owner.LogAIThrottled(
                $"시야/소리 감지 X → targetLostTime 진행 중, 남은 시간 {_owner.Data.targetLostTIme - lostTime:F1}초");
            return;
        }

        float finalDist = Vector3.Distance(_owner.transform.position, s.LastKnownPosition);

        if (finalDist <= _owner.InvestigateStartDistance ||
            HasReachedDestination(_owner.InvestigateReachDistance))
        {
            _owner.LogAI("targetLostTime 종료 시 플레이어 위치 도달 → 수색 시작");

            if (!s.InvestigateRoutineRunning)
                _owner.StartCoroutine(BeginInvestigate());

            return;
        }

        _owner.LogAI("targetLostTime 종료 + 플레이어 위치 도달 실패 → 순찰 복귀");
        ReturnToPatrolRoute();
    }

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
            s.InvestigateTimer = _owner.Data.targetLostTIme;

            _owner.LogAI("마지막 위치 도착 완료 → 주변 랜덤 수색 시작");
            SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());
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
            return;

        _owner.LogAI($"수색 중 → 남은 시간 {s.InvestigateTimer:F1}초, 다음 수색 지점 선택");
        SetRandomInvestigatePointAround(s.LastKnownPosition, GetInvestigateRadius());
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

        if (_owner.Anim == null) return;
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

        switch (s.CurrentState)
        {
            case EnemyBase.State.Patrol:
                _owner.Agent.speed = GetPatrolSpeed();
                break;

            case EnemyBase.State.Chase:
                _owner.Agent.speed = GetChaseSpeed();
                s.NextChaseRepathTime = 0f;
                s.LastChaseDestination = _owner.transform.position - Vector3.one * 999f;
                break;

            case EnemyBase.State.Investigate:
                _owner.Agent.speed = GetPatrolSpeed();
                break;
        }

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
        s.HiddenSearchTargetActive = false;
        s.HiddenKillTargetActive = false;
        s.DoorSpecialAllowed = false;

        ChangeState(EnemyBase.State.Patrol);

        if (s.HasPatDestination)
        {
            _owner.Agent.isStopped = false;
            _owner.Agent.speed = GetPatrolSpeed();
            _owner.Patrol.RestoreDestination(s.CurrentPatrolDestination);
            _owner.SyncPatrolDestinationFromRuntime();
        }
        else
        {
            _owner.SetNextGlobalPatDestination();
        }
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

        ChangeState(EnemyBase.State.Investigate);
        _owner.LogAI("수색모드 시작: 마지막 위치 주변 5m 수색");

        s.ReachedLastKnownPosition = false;
        s.InvestigateTimer = _owner.Data.targetLostTIme;
        s.HiddenSearchTargetActive = false;

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
        if (_owner.NavMotor != null)
        {
            if (_owner.NavMotor.EnsureDestination(target, 3f))
                return;
        }

        if (!_owner.Agent.isOnNavMesh)
            return;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return;

        _owner.Agent.isStopped = false;
        _owner.Agent.SetDestination(hit.position);
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
            Quaternion.Euler(0f, 120f, 0f) * forward,
            Quaternion.Euler(0f, -120f, 0f) * forward,
            -forward
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
