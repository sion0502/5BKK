using UnityEngine;
using UnityEngine.AI;

public class EnemyBase : MonoBehaviour
{
    public enum State { Patrol, Investigate, Chase }
    public State currentState;

    [SerializeField] private Monsters data;
    [SerializeField] private Transform player;
    [SerializeField] private Transform eyePoint;

    private NavMeshAgent agent;
    private Animator animator;

    protected NavMeshAgent Agent => agent;
    protected Animator Anim => animator;
    protected Transform Player => player;
    protected Monsters Data => data;

    float runSpeed;
    float walkSpeed;

    float forcedChaseTimer;
    float investigateTimer;

    Vector3 lastKnownPos;
    float patrolTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        runSpeed = data.moveSpeed;
        walkSpeed = data.moveSpeed * 0.35f;

        currentState = State.Patrol;
    }

    void Update()
    {
        bool visible = CheckVision();

        if (visible)
        {
            currentState = State.Chase;
            lastKnownPos = Player.position;
            forcedChaseTimer = 5f;
        }
        else if (currentState == State.Chase)
        {
            forcedChaseTimer -= Time.deltaTime;

            if (forcedChaseTimer <= 0f)
            {
                currentState = State.Investigate;
                investigateTimer = 10f;
            }
        }

        switch (currentState)
        {
            case State.Patrol:
                Agent.speed = walkSpeed;
                UpdatePatrol();
                break;

            case State.Chase:
                Agent.speed = runSpeed;
                UpdateChase();
                break;

            case State.Investigate:
                Agent.speed = walkSpeed;
                UpdateInvestigate();
                break;
        }

        UpdateAnimation();
    }

    bool CheckVision()
    {
        if (Player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.3f;
        Vector3 dir = (Player.position - origin).normalized;
        float dist = Vector3.Distance(origin, Player.position);

        if (dist > Data.detectRange) return false;

        int mask = Data.obstacleLayer | Data.playerLayer;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, mask))
        {
            if (((1 << hit.collider.gameObject.layer) & Data.obstacleLayer) != 0)
                return false;

            if (((1 << hit.collider.gameObject.layer) & Data.playerLayer) != 0)
                return true;
        }

        return false;
    }

    void UpdatePatrol()
    {
        patrolTimer -= Time.deltaTime;

        if (patrolTimer > 0f) return;

        patrolTimer = Random.Range(2f, 4f);

        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;
            dir.Normalize();

            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, dir);

            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                if (((1 << hit.collider.gameObject.layer) & Data.obstacleLayer) != 0)
                    continue;

                if (hit.collider.CompareTag("Door"))
                    continue;
            }

            Vector3 target = transform.position + dir * Random.Range(15f, 30f);

            if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
            {
                Agent.SetDestination(navHit.position);
                return;
            }
        }
    }

    void UpdateInvestigate()
    {
        investigateTimer -= Time.deltaTime;

        if (Agent.remainingDistance < 2f)
        {
            Vector3 rand = lastKnownPos + Random.insideUnitSphere * 5f;
            rand.y = 0;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                Agent.SetDestination(hit.position);
            }
        }

        if (investigateTimer <= 0f)
        {
            currentState = State.Patrol;

            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0;

            Vector3 farTarget = transform.position + dir * Random.Range(20f, 35f);

            if (NavMesh.SamplePosition(farTarget, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
            {
                Agent.SetDestination(navHit.position);
            }
        }
    }

    void UpdateChase()
    {
        Agent.SetDestination(Player.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, 5f);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Door")) continue;

            DoorController door = hit.GetComponentInParent<DoorController>();

            if (door != null && !door.IsBroken())
            {
                Agent.SetDestination(door.transform.position);
                HandleDoor(door);
                return;
            }
        }
    }

    protected virtual void HandleDoor(DoorController door) { }

    void UpdateAnimation()
    {
        if (currentState == State.Patrol)
            Anim.SetFloat("Speed", 0.3f);
        else if (currentState == State.Chase)
            Anim.SetFloat("Speed", 1f);
        else if (currentState == State.Investigate)
            Anim.SetFloat("Speed", 0.3f);
    }
}