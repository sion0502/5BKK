using UnityEngine;
using UnityEngine.AI;

public enum State
{
    PATROL,
    SUSPICIOUS,
    CHASE
}

public class MonsterController : MonoBehaviour
{
    public Monsters data;
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;

    private float walkSpeed;
    private float runSpeed;

    private State state;

    private float patrolTimer;
    private float doorCheckTimer;
    private float suspiciousTimer;

    private Vector3 lastKnownPos;

    private float attackCooldown = 1.2f;
    private float lastAttackTime;

    private bool isAttacking = false;

    private int attackDamage = 999;

    private bool doorTransition = false;
    private float doorTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        agent.updateUpAxis = false;
        agent.angularSpeed = 180f;
        agent.acceleration = 25f;

        runSpeed = data.moveSpeed;
        walkSpeed = data.moveSpeed * 0.5f;

        state = State.PATROL;
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (CanDetectPlayer(distance))
        {
            lastKnownPos = player.position;
            state = State.CHASE;
        }

        // 🔥 문 부순 직후 처리
        if (doorTransition)
        {
            doorTimer += Time.deltaTime;

            // 👉 핵심: 강제로 플레이어 방향 재지정
            agent.isStopped = false;
            agent.SetDestination(player.position);

            if (doorTimer > 0.5f)
            {
                doorTransition = false;
            }

            UpdateAnimation();
            return;
        }

        switch (state)
        {
            case State.PATROL:
                HandlePatrol();
                break;

            case State.CHASE:
                HandleChase(distance);
                break;

            case State.SUSPICIOUS:
                HandleSuspicious();
                break;
        }

        UpdateAnimation();
    }

    bool CanDetectPlayer(float distance)
    {
        if (distance < 3f) return true;
        if (distance > data.detectRange) return false;

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = (player.position - origin).normalized;

        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > data.viewAngle * 0.6f) return false;

        int mask = data.playerLayer | data.obstacleLayer | LayerMask.GetMask("Door");

        if (Physics.SphereCast(origin, 0.5f, dir, out RaycastHit hit, data.detectRange, mask))
        {
            if (!hit.transform.CompareTag("Player"))
                return false;
        }

        return true;
    }

    void HandlePatrol()
    {
        patrolTimer += Time.deltaTime;

        agent.speed = walkSpeed;
        agent.isStopped = false;

        if (patrolTimer > data.checkInterval)
        {
            patrolTimer = 0f;

            if (Random.value < 0.3f)
                agent.ResetPath();
            else
                MoveRandom(transform.position, 20f);
        }
    }

    void HandleChase(float distance)
    {
        agent.speed = runSpeed;

        doorCheckTimer += Time.deltaTime;

        if (doorCheckTimer > 0.2f)
        {
            doorCheckTimer = 0f;

            if (HandleDoor())
                return;
        }

        agent.isStopped = false;

        // 🔥 핵심: 계속 플레이어로 갱신
        agent.SetDestination(player.position);

        if (distance > data.detectRange)
        {
            suspiciousTimer = 0f;
            state = State.SUSPICIOUS;
        }

        if (distance < 2f && !isAttacking && Time.time > lastAttackTime + attackCooldown)
        {
            isAttacking = true;
            lastAttackTime = Time.time;
            anim.SetTrigger("Attack");
        }

        // 🔥 추가: 벽 stuck 방지
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            agent.SetDestination(player.position);
        }
    }

    void HandleSuspicious()
    {
        suspiciousTimer += Time.deltaTime;

        agent.speed = walkSpeed;

        if (!agent.hasPath || agent.remainingDistance < 1f)
        {
            MoveRandom(lastKnownPos, 6f);
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (CanDetectPlayer(distance))
        {
            state = State.CHASE;
            return;
        }

        if (suspiciousTimer > 5f)
        {
            state = State.PATROL;
        }
    }

    void MoveRandom(Vector3 center, float radius)
    {
        Vector3 randomDir = center + Random.insideUnitSphere * radius;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    bool HandleDoor()
    {
        int mask = LayerMask.GetMask("Door");

        Vector3 origin = transform.position + Vector3.up * 1f;

        if (Physics.SphereCast(origin, 1.2f, transform.forward, out RaycastHit hit, 4f, mask))
        {
            DoorController door = hit.collider.GetComponentInParent<DoorController>();

            if (door != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();

                Vector3 dir = (door.transform.position - transform.position).normalized;
                dir.y = 0;

                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);

                if (!isAttacking && Time.time > lastAttackTime + attackCooldown)
                {
                    isAttacking = true;
                    lastAttackTime = Time.time;

                    anim.SetTrigger("Attack");

                    door.TakeDamage(attackDamage);

                    // 🔥 핵심
                    doorTransition = true;
                    doorTimer = 0f;
                }

                return true;
            }
        }

        return false;
    }

    void UpdateAnimation()
    {
        float speed = agent.velocity.magnitude;

        if (speed < 0.1f)
            anim.SetFloat("Speed", 0f);
        else if (state == State.CHASE)
            anim.SetFloat("Speed", 1f);
        else
            anim.SetFloat("Speed", 0.5f);
    }

    public void EndAttack()
    {
        isAttacking = false;
        agent.isStopped = false;
    }
}