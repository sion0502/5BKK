using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorAttackStartDistance = 1.0f;
    [SerializeField] private float doorStopDistance = 0.7f;
    [SerializeField] private string doorAttackState = "Base Layer.Attack";
    [SerializeField] private float doorHitDelay = 0.45f;
    [SerializeField] private float faceRotateSpeed = 360f;
    [SerializeField] private float postAttackCooldown = 0.2f;
    [SerializeField] private float attackFaceAngle = 6f;
    [SerializeField] private float approachTimeout = 3f;
    [SerializeField] private float finalAlignTimeout = 1.5f;

    private bool attacking;
    private float nextPossibleAttackTime;
    private bool resumeThroughDoor;
    private Vector3 resumeDoorPos;
    private Vector3 resumeThrough;

    protected override void Awake()
    {
        autoOpenDoorsOnPatrol = true;
        base.Awake();
    }

    protected internal override void HandleChaseSpecial()
    {
        if (currentState != State.Chase) return;
        if (data != null && data.monsterType == MonsterType.Ghost) return;
        if (attacking) return;
        if (Time.time < nextPossibleAttackTime) return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(chaseDoorDetectDistance);
        if (door == null) return;
        if (!IsDoorStillAttackable(door)) return;
        if (GetDistanceToDoorCollider(door) > chaseDoorDetectDistance) return;

        StartCoroutine(DoorAttackRoutine(door));
    }

    private IEnumerator DoorAttackRoutine(DoorBrokenTest door)
    {
        if (!IsDoorStillAttackable(door)) yield break;
        attacking = true;
        isBusy = true;
        lockAnimator = true;
        bool originalRootMotion = anim.applyRootMotion;
        bool originalRotation = agent.updateRotation;
        float originalStoppingDistance = agent.stoppingDistance;
        anim.applyRootMotion = false;
        agent.stoppingDistance = 0f;
        float reach = Mathf.Max(doorAttackStartDistance, agent.radius + 0.2f);

        try
        {
            // Pick the approach point once, on the current side of the door.
            Vector3 doorPoint = GetDoorClosestPoint(door);
            Vector3 through = GetDoorThroughDirection(door, RuntimeState.LastKnownPosition);
            if (through.sqrMagnitude < 0.001f)
            {
                through = doorPoint - transform.position;
                through.y = 0f;
                if (through.sqrMagnitude < 0.001f) through = transform.forward;
                through.Normalize();
            }
            Vector3 approach = doorPoint - through * Mathf.Min(doorStopDistance, reach * 0.75f);
            approach.y = transform.position.y;
            float timer = 0f;
            float nextApproachUpdate = 0f;
            anim.SetInteger(AnimState, 2);
            while (GetDistanceToDoorCollider(door) > reach)
            {
                if (!IsDoorStillAttackable(door) || timer >= approachTimeout) yield break;
                agent.updateRotation = true;
                if (Time.time >= nextApproachUpdate)
                {
                    if (NavMotor == null || !NavMotor.EnsureDestination(approach, 0.35f))
                        yield break;
                    nextApproachUpdate = Time.time + 0.2f;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            // Stop translation before turning; never walk backwards to align an attack.
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
            anim.SetInteger(AnimState, 0);
            timer = 0f;
            while (GetAngleToDoorCollider(door) > Mathf.Max(attackFaceAngle, 5f))
            {
                if (!IsDoorStillAttackable(door) || timer >= finalAlignTimeout) yield break;
                FaceDoorCollider(door);
                timer += Time.deltaTime;
                yield return null;
            }
            if (!IsDoorStillAttackable(door)) yield break;

            // Enter the attack directly: the controller's trigger only works from Idle.
            int attackState = Animator.StringToHash(doorAttackState);
            if (!anim.HasState(0, attackState))
                attackState = Animator.StringToHash("Base Layer.Rush");
            if (!anim.HasState(0, attackState))
            {
                yield return FallbackDoorHit(door, reach);
                yield break;
            }
            // Prevent State=0 transitions from interrupting the attack with Idle.
            anim.SetInteger(AnimState, -1);
            anim.ResetTrigger(AnimAttack);
            anim.CrossFadeInFixedTime(attackState, 0.05f, 0, 0f);
            timer = 0f;
            while (anim.GetCurrentAnimatorStateInfo(0).fullPathHash != attackState)
            {
                if (!IsDoorStillAttackable(door) || timer >= 0.5f) yield break;
                timer += Time.deltaTime;
                yield return null;
            }

            AnimatorStateInfo attack = anim.GetCurrentAnimatorStateInfo(0);
            float hitTime = Mathf.Clamp(doorHitDelay / Mathf.Max(0.01f, attack.length), 0.1f, 0.45f);
            timer = 0f;
            while (anim.GetCurrentAnimatorStateInfo(0).normalizedTime < hitTime)
            {
                if (!IsDoorStillAttackable(door) || timer >= 3f ||
                    anim.GetCurrentAnimatorStateInfo(0).fullPathHash != attackState) yield break;
                timer += Time.deltaTime;
                yield return null;
            }
            if (!IsDoorStillAttackable(door) ||
                anim.GetCurrentAnimatorStateInfo(0).fullPathHash != attackState) yield break;
            if (GetDistanceToDoorCollider(door) <= reach + 0.15f)
            {
                resumeDoorPos = GetDoorClosestPoint(door);
                resumeThrough = GetDoorThroughDirection(door, RuntimeState.LastKnownPosition);
                resumeThroughDoor = true;
                door.BreakByEnemy(transform.position);
            }

            // Hold the same facing through recovery, not the flying broken door.
            timer = 0f;
            while (anim.GetCurrentAnimatorStateInfo(0).fullPathHash == attackState &&
                   anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f && timer < 2f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            anim.applyRootMotion = originalRootMotion;
            agent.updateRotation = originalRotation;
            agent.stoppingDistance = originalStoppingDistance;
            nextPossibleAttackTime = Time.time + Mathf.Max(0.2f, postAttackCooldown);
            EndDoorAttack();
        }
    }

    void KeepAgentStoppedForAttack()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    IEnumerator FallbackDoorHit(DoorBrokenTest door, float reach)
    {
        // Models without an attack clip still visibly strike, without moving their collider.
        Transform model = anim.transform;
        Quaternion original = model.localRotation;
        bool wasEnabled = anim.enabled;
        anim.enabled = false;
        bool hit = false;
        float elapsed = 0f;
        try
        {
            while (elapsed < 0.5f)
            {
                if (!hit && !IsDoorStillAttackable(door)) yield break;
                float phase = elapsed / 0.5f;
                model.localRotation = original * Quaternion.Euler(Mathf.Sin(phase * Mathf.PI) * 18f, 0f, 0f);
                if (!hit && phase >= 0.45f)
                {
                    if (GetDistanceToDoorCollider(door) > reach + 0.15f) yield break;
                    resumeDoorPos = GetDoorClosestPoint(door);
                    resumeThrough = GetDoorThroughDirection(door, RuntimeState.LastKnownPosition);
                    resumeThroughDoor = true;
                    door.BreakByEnemy(transform.position);
                    hit = true;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            model.localRotation = original;
            anim.enabled = wasEnabled;
        }
    }
    private bool IsDoorStillAttackable(DoorBrokenTest door)
    {
        if (door == null) return false;

        DoorClick click = door.GetComponent<DoorClick>();
        if (click == null) return false;
        if (click.IsOpen()) return false;
        if (click.IsBroken()) return false;
        if (door.IsBroken()) return false;

        return true;
    }

    private Vector3 GetDoorClosestPoint(DoorBrokenTest door)
    {
        Vector3 origin = transform.position + Vector3.up * doorCheckHeight;
        Vector3 closest = door.transform.position;
        float bestDistance = float.PositiveInfinity;
        foreach (Collider col in door.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled || col.isTrigger) continue;
            Vector3 point = col.ClosestPoint(origin);
            point.y = transform.position.y;
            float distance = (point - transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            closest = point;
        }

        closest.y = transform.position.y;
        return closest;
    }

    private float GetDistanceToDoorCollider(DoorBrokenTest door)
    {
        Vector3 doorPoint = GetDoorClosestPoint(door);
        Vector3 enemyPos = transform.position;

        doorPoint.y = enemyPos.y;

        return Vector3.Distance(enemyPos, doorPoint);
    }

    private void FaceDoorCollider(DoorBrokenTest door)
    {
        Vector3 doorPoint = GetDoorClosestPoint(door);
        Vector3 dir = doorPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, faceRotateSpeed * Time.deltaTime);
    }

    private float GetAngleToDoorCollider(DoorBrokenTest door)
    {
        Vector3 doorPoint = GetDoorClosestPoint(door);
        Vector3 dir = doorPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return 0f;

        return Vector3.Angle(transform.forward, dir.normalized);
    }

    Vector3 GetDoorThroughDirection(DoorBrokenTest door, Vector3 toward)
    {
        Vector3 toTarget = toward - door.transform.position;
        toTarget.y = 0f;

        Vector3 fwd = door.transform.forward;
        Vector3 right = door.transform.right;
        fwd.y = 0f;
        right.y = 0f;

        Vector3 axis = Mathf.Abs(Vector3.Dot(fwd, toTarget)) >= Mathf.Abs(Vector3.Dot(right, toTarget))
            ? fwd
            : right;
        if (axis.sqrMagnitude < 0.001f)
            axis = toTarget;
        if (Vector3.Dot(axis, toTarget) < 0f)
            axis = -axis;

        return axis.sqrMagnitude > 0.001f ? axis.normalized : transform.forward;
    }

    void ResumeThroughBrokenDoor(Vector3 doorPos, Vector3 through)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        doorPos.y = transform.position.y;
        through.y = 0f;
        if (through.sqrMagnitude < 0.001f)
            through = transform.forward;
        through.Normalize();

        Vector3 opening = doorPos - through * 0.35f;
        if (NavMesh.SamplePosition(opening, out NavMeshHit hit, 1.2f, NavMesh.AllAreas) &&
            Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(hit.position.x, 0f, hit.position.z)) < 1.4f)
        {
            agent.Warp(hit.position);
        }

        if (NavMotor != null)
        {
            NavMotor.ResetPath();
            NavMotor.SetDestination(RuntimeState.LastKnownPosition, 2f);
        }
        else
        {
            agent.ResetPath();
            agent.SetDestination(RuntimeState.LastKnownPosition);
        }

        RuntimeState.NextChaseRepathTime = 0f;
        RuntimeState.LastChaseDestination = transform.position - Vector3.one * 999f;
        agent.isStopped = false;
    }

    private void EndDoorAttack()
    {
        KeepAgentStoppedForAttack();

        if (currentState == State.Chase)
            anim.SetInteger(AnimState, 2);

        lockAnimator = false;
        isBusy = false;
        attacking = false;

        if (agent.enabled && agent.isOnNavMesh && !PlayerDeathDebug.IsDying && !PlayerDeathDebug.IsDead)
        {
            agent.isStopped = false;
            if (resumeThroughDoor)
            {
                resumeThroughDoor = false;
                ResumeThroughBrokenDoor(resumeDoorPos, resumeThrough);
            }
        }
        else
        {
            resumeThroughDoor = false;
        }
    }
}
