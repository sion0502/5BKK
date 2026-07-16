using System.Collections;
using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    [Header("Door Attack")]
    [SerializeField] private float doorAttackStartDistance = 1.0f;
    [SerializeField] private float doorStopDistance = 0.7f;
    [SerializeField] private float idleBeforeAttackTime = 0.5f;
    [SerializeField] private float doorHitDelay = 0.45f;
    [SerializeField] private float attackEndDelay = 0.7f;
    [SerializeField] private float postAttackIdleTime = 1.0f;
    [SerializeField] private float faceRotateSpeed = 360f;
    [SerializeField] private float postAttackCooldown = 0.2f;
    [SerializeField] private float attackFaceAngle = 6f;
    [SerializeField] private float approachTimeout = 3f;
    [SerializeField] private float finalAttackDistance = 0.8f;
    [SerializeField] private float finalAttackFaceAngle = 2.5f;
    [SerializeField] private float finalAlignTimeout = 1.5f;

    private bool attacking;
    private float nextPossibleAttackTime;

    protected internal override void HandleChaseSpecial()
    {
        if (currentState != State.Chase) return;
        if (!doorSpecialAllowed) return;
        if (attacking) return;
        if (Time.time < nextPossibleAttackTime) return;

        DoorBrokenTest door = GetClosedDoorOnChasePath(chaseDoorDetectDistance);
        if (door == null) return;
        if (!IsDoorStillAttackable(door)) return;

        StartCoroutine(DoorAttackRoutine(door));
    }

    private IEnumerator DoorAttackRoutine(DoorBrokenTest door)
    {
        if (door == null) yield break;
        if (!IsDoorStillAttackable(door)) yield break;

        attacking = true;
        isBusy = true;
        lockAnimator = true;

        agent.isStopped = true;
        agent.ResetPath();

        anim.ResetTrigger(AnimAttack);
        anim.SetInteger(AnimState, 2);

        float approachTimer = 0f;

        while (approachTimer < approachTimeout)
        {
            if (!IsDoorStillAttackable(door))
            {
                EndDoorAttack();
                yield break;
            }

            float dist = GetDistanceToDoorCollider(door);
            float angle = GetAngleToDoorCollider(door);

            if (dist <= doorAttackStartDistance && angle <= attackFaceAngle)
                break;

            Vector3 attackPos = GetDoorAttackPosition(door);

            agent.isStopped = false;
            SafeSetDestination(attackPos);
            FaceDoorCollider(door);

            approachTimer += Time.deltaTime;
            yield return null;
        }

        if (!IsDoorStillAttackable(door))
        {
            EndDoorAttack();
            yield break;
        }

        if (!IsReadyToAttackDoor(door, doorAttackStartDistance, attackFaceAngle))
        {
            nextPossibleAttackTime = Time.time + postAttackCooldown;
            EndDoorAttack();
            yield break;
        }

        agent.isStopped = true;
        agent.ResetPath();

        float faceTimer = 0f;

        while (faceTimer < 1.0f)
        {
            if (!IsDoorStillAttackable(door))
            {
                EndDoorAttack();
                yield break;
            }

            FaceDoorCollider(door);

            float dist = GetDistanceToDoorCollider(door);
            float angle = GetAngleToDoorCollider(door);

            if (dist <= doorAttackStartDistance + 0.2f && angle <= attackFaceAngle + 4f)
                break;

            faceTimer += Time.deltaTime;
            yield return null;
        }

        if (!IsDoorStillAttackable(door))
        {
            EndDoorAttack();
            yield break;
        }

        float finalAlignTimer = 0f;

        while (finalAlignTimer < finalAlignTimeout)
        {
            if (!IsDoorStillAttackable(door))
            {
                EndDoorAttack();
                yield break;
            }

            FaceDoorCollider(door);

            if (IsReadyToAttackDoor(door, finalAttackDistance, finalAttackFaceAngle))
                break;

            agent.isStopped = false;
            SafeSetDestination(GetDoorAttackPosition(door));

            finalAlignTimer += Time.deltaTime;
            yield return null;
        }

        agent.isStopped = true;
        agent.ResetPath();

        if (!IsReadyToAttackDoor(door, finalAttackDistance, finalAttackFaceAngle))
        {
            nextPossibleAttackTime = Time.time + postAttackCooldown;
            EndDoorAttack();
            yield break;
        }

        anim.SetInteger(AnimState, 0);

        float idleTimer = 0f;

        while (idleTimer < idleBeforeAttackTime)
        {
            if (!IsDoorStillAttackable(door))
            {
                EndDoorAttack();
                yield break;
            }

            FaceDoorCollider(door);

            idleTimer += Time.deltaTime;
            yield return null;
        }

        if (!IsDoorStillAttackable(door))
        {
            EndDoorAttack();
            yield break;
        }

        FaceDoorCollider(door);

        if (!IsReadyToAttackDoor(door, finalAttackDistance, finalAttackFaceAngle))
        {
            nextPossibleAttackTime = Time.time + postAttackCooldown;
            EndDoorAttack();
            yield break;
        }

        anim.SetTrigger(AnimAttack);

        yield return WaitAttackPhase(doorHitDelay, door);

        if (door != null && !door.IsBroken() && IsReadyToAttackDoor(door, finalAttackDistance, finalAttackFaceAngle))
        {
            Debug.Log("Monster Door Hit");
            door.BreakByEnemy(transform.position);
        }

        yield return WaitAttackPhase(attackEndDelay, door, true);

        anim.SetInteger(AnimState, 0);
        yield return WaitAttackPhase(postAttackIdleTime, door, true);

        nextPossibleAttackTime = Time.time + postAttackCooldown;

        EndDoorAttack();
    }

    IEnumerator WaitAttackPhase(float duration, DoorBrokenTest door, bool allowBrokenDoor = false)
    {
        float timer = 0f;

        while (timer < duration)
        {
            if (door == null)
            {
                EndDoorAttack();
                yield break;
            }

            if (!allowBrokenDoor && !IsDoorStillAttackable(door))
            {
                EndDoorAttack();
                yield break;
            }

            KeepAgentStoppedForAttack();
            FaceDoorCollider(door);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    void KeepAgentStoppedForAttack()
    {
        agent.isStopped = true;
        agent.ResetPath();
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

    private bool IsReadyToAttackDoor(DoorBrokenTest door, float maxDistance, float maxAngle)
    {
        if (!IsDoorStillAttackable(door)) return false;

        float dist = GetDistanceToDoorCollider(door);
        float angle = GetAngleToDoorCollider(door);

        return dist <= maxDistance && angle <= maxAngle;
    }

    private Vector3 GetDoorAttackPosition(DoorBrokenTest door)
    {
        Vector3 doorPoint = GetDoorClosestPoint(door);
        Vector3 dir = transform.position - doorPoint;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            dir = -transform.forward;

        Vector3 attackPos = doorPoint + dir.normalized * doorStopDistance;
        attackPos.y = transform.position.y;

        return attackPos;
    }

    private Vector3 GetDoorClosestPoint(DoorBrokenTest door)
    {
        Collider col = door.GetComponentInChildren<Collider>();

        if (col != null)
        {
            Vector3 point = col.ClosestPoint(transform.position);
            point.y = transform.position.y;
            return point;
        }

        Vector3 fallback = door.transform.position;
        fallback.y = transform.position.y;
        return fallback;
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

    private void EndDoorAttack()
    {
        KeepAgentStoppedForAttack();

        if (currentState == State.Chase)
            anim.SetInteger(AnimState, 2);

        lockAnimator = false;
        isBusy = false;
        attacking = false;

        agent.isStopped = false;
    }
}
