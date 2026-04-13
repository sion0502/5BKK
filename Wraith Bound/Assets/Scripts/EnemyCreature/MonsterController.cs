using UnityEngine;
using UnityEngine.AI;

public enum State
{
    PATROL,
    CHASE,
    SEARCH
}

public class MonsterController : MonoBehaviour
{
    public Monsters data;
    public Transform player;
    public Transform eye;

    public Transform patrolCenter;
    public float patrolRange = 60f;

    private NavMeshAgent agent;
    private Animator anim;

    private State state;

    private Vector3 lastKnownPos;
    private float searchTimer;
    private float loseTimer;

    private bool isAttacking;
    private float attackTimer;

    private DoorController currentDoor;

    private int doorMask;

    private float idleTimer = 0f;
    private float moveTimer = 0f;

    private float currentRange;
    private float currentAngle;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        doorMask = LayerMask.GetMask("Door");

        state = State.PATROL;

        currentRange = data.detectRange;
        currentAngle = data.viewAngle;
    }

    void Update()
    {
        if (!player || !eye) return;

        if (isAttacking)
        {
            HandleAttack();
            return;
        }

        bool detected = CanSeePlayer();

        switch (state)
        {
            case State.PATROL:
                Patrol();

                if (detected)
                {
                    currentRange = 500f;
                    currentAngle = 100f;

                    lastKnownPos = player.position;
                    state = State.CHASE;
                }
                break;

            case State.CHASE:
                if (detected)
                {
                    lastKnownPos = player.position;
                    loseTimer = 0f;
                }
                else
                {
                    loseTimer += Time.deltaTime;
                }

                Chase();

                if (loseTimer > 5f && Vector3.Distance(transform.position, lastKnownPos) < 3f)
                {
                    state = State.SEARCH;
                    searchTimer = 7f;
                }
                break;

            case State.SEARCH:
                Search();

                if (detected)
                {
                    state = State.CHASE;
                }
                break;
        }

        UpdateAnimation();
    }

    bool CanSeePlayer()
    {
        Vector3 dir = player.position - eye.position;
        float sqrDist = dir.sqrMagnitude;

        if (sqrDist > currentRange * currentRange)
            return false;

        dir.Normalize();

        if (Vector3.Angle(eye.forward, dir) > currentAngle)
            return false;

        if (Physics.Raycast(eye.position, dir, out RaycastHit hit, currentRange))
        {
            if (hit.collider.CompareTag("Player"))
                return true;
        }

        return false;
    }

    void Patrol()
    {
        agent.speed = data.moveSpeed * 0.6f;

        if (idleTimer > 0f)
        {
            idleTimer -= Time.deltaTime;

            agent.velocity = Vector3.zero;
            transform.Rotate(0f, 60f * Time.deltaTime, 0f);

            anim.SetFloat("Speed", 0f);
            return;
        }

        moveTimer -= Time.deltaTime;

        if (!agent.hasPath || agent.remainingDistance < 2f || moveTimer <= 0f)
        {
            agent.SetDestination(GetRandomPoint());

            moveTimer = Random.Range(4f, 7f);
            idleTimer = Random.Range(1.5f, 3f);
        }
    }

    Vector3 GetRandomPoint()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 rand = patrolCenter.position + Random.insideUnitSphere * patrolRange;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, patrolRange, NavMesh.AllAreas))
                return hit.position;
        }

        return patrolCenter.position;
    }

    void Chase()
    {
        agent.speed = data.moveSpeed;
        agent.SetDestination(lastKnownPos);

        DetectDoor();
    }

    void Search()
    {
        agent.speed = data.moveSpeed * 0.5f;

        searchTimer -= Time.deltaTime;

        if (!agent.hasPath || agent.remainingDistance < 1.5f)
        {
            Vector3 rand = lastKnownPos + Random.insideUnitSphere * 8f;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        if (searchTimer <= 0f)
        {
            currentRange = data.detectRange;
            currentAngle = data.viewAngle;

            state = State.PATROL;
        }
    }

    void DetectDoor()
    {
        if (state != State.CHASE) return;

        if (agent.velocity.sqrMagnitude < 0.1f) return;

        Vector3 toTarget = (lastKnownPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, toTarget);

        if (dot < 0.5f) return;

        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, 2f, doorMask))
        {
            DoorController door = hit.collider.GetComponentInParent<DoorController>();

            if (door != null)
            {
                currentDoor = door;

                agent.ResetPath();

                Vector3 dir = (door.transform.position - transform.position).normalized;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);

                StartAttack(door);
            }
        }
    }

    void StartAttack(DoorController door)
    {
        isAttacking = true;
        attackTimer = 0f;
        currentDoor = door;

        agent.velocity = Vector3.zero;

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