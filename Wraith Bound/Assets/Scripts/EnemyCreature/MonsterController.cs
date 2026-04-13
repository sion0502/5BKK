using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum State
{
    PATROL,
    CHASE
}

public class MonsterController : MonoBehaviour
{
    public Monsters data;
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;

    private State state;

    private bool isAttacking;
    private float attackTimer;

    private DoorController currentDoor;
    private int doorMask;

    private float patrolTimer;
    private float patrolChangeTime = 2.5f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        doorMask = LayerMask.GetMask("Door");

        state = State.PATROL;
    }

    void Update()
    {
        if (!player) return;

        if (isAttacking)
        {
            HandleAttack();
            return;
        }

        if (CanSeePlayer())
        {
            state = State.CHASE;
            agent.isStopped = false;
        }

        switch (state)
        {
            case State.PATROL:
                Patrol();
                break;

            case State.CHASE:
                Chase();
                break;
        }

        UpdateAnimation();
    }

    // 🔥 감지 (벽/문 뒤 못 봄)
    bool CanSeePlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 target = player.position + Vector3.up * 0.5f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;

        if (dist > data.detectRange)
            return false;

        dir.Normalize();

        Vector3 flatDir = dir;
        flatDir.y = 0;

        Vector3 forward = transform.forward;
        forward.y = 0;

        float angle = Vector3.Angle(forward, flatDir);
        if (angle > data.viewAngle * 0.5f)
            return false;

        // 🔥 첫 충돌만 확인 (벽 뒤 감지 방지)
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist))
        {
            if (hit.collider.CompareTag("Player"))
                return true;
        }

        return false;
    }

    // 🔥 순찰
    void Patrol()
    {
        agent.speed = data.moveSpeed * 0.7f;

        patrolTimer += Time.deltaTime;

        if (patrolTimer > patrolChangeTime || !agent.hasPath)
        {
            patrolTimer = 0f;

            float randomAngle = Random.Range(-120f, 120f);
            Vector3 dir = Quaternion.Euler(0, randomAngle, 0) * transform.forward;

            Vector3 target = transform.position + dir * Random.Range(6f, 15f);

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }

            if (Random.value < 0.4f)
            {
                StartCoroutine(IdleRoutine());
            }
        }
    }

    IEnumerator IdleRoutine()
    {
        agent.isStopped = true;
        anim.SetFloat("Speed", 0);

        float t = 0f;
        float idleTime = Random.Range(1.5f, 3f);

        while (t < idleTime)
        {
            t += Time.deltaTime;
            transform.Rotate(0, 60f * Time.deltaTime, 0);
            yield return null;
        }

        agent.isStopped = false;
    }

    // 🔥 추격
    void Chase()
    {
        agent.speed = data.moveSpeed * 1.5f;

        agent.SetDestination(player.position);

        DetectDoor();
    }

    // 🔥 문 감지 (CHASE에서만)
    void DetectDoor()
    {
        if (state != State.CHASE) return;

        Vector3 forward = transform.forward;

        if (Physics.Raycast(transform.position + Vector3.up, forward, out RaycastHit hit, 5f, doorMask))
        {
            DoorController door = hit.collider.GetComponentInParent<DoorController>();

            if (door != null)
            {
                currentDoor = door;
                StartAttack(door);
            }
        }
    }

    void StartAttack(DoorController door)
    {
        isAttacking = true;
        attackTimer = 0f;

        agent.isStopped = true;

        Vector3 dir = (door.transform.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(dir);

        anim.SetTrigger("Attack");
    }

    void HandleAttack()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer > 0.4f && currentDoor)
        {
            currentDoor.TakeDamage(999);
            currentDoor = null;
        }

        if (attackTimer > 1f)
        {
            isAttacking = false;
            agent.isStopped = false;

            // 🔥 핵심: 경로 재계산
            agent.ResetPath();
            agent.SetDestination(player.position);
        }
    }

    void UpdateAnimation()
    {
        if (isAttacking)
        {
            anim.SetFloat("Speed", 0);
            return;
        }

        float speed = agent.velocity.sqrMagnitude;

        if (speed < 0.01f)
            anim.SetFloat("Speed", 0);
        else if (state == State.CHASE)
            anim.SetFloat("Speed", 1);
        else
            anim.SetFloat("Speed", 0.5f);
    }
}