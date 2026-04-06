using UnityEngine;
using UnityEngine.AI;

public enum State
{
    PATROL,
    CHASE,
    ATTACK
}

public class EnemyController : MonoBehaviour
{
    public Monsters data;
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;

    public float runSpeed = 8f;

    public State currentState;

    private float attackCooldown = 2f;
    private float lastAttackTime = 0f;

    private enum PatrolType { IDLE, WALK, RUN }
    private PatrolType patrolState;

    float actionTimer = 0f;
    float actionTime = 2f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        currentState = State.PATROL;

        agent.acceleration = 20f;
        agent.angularSpeed = 300f;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.PATROL:
                Patrol();
                break;

            case State.CHASE:
                Chase();
                break;

            case State.ATTACK:
                Attack();
                break;
        }

        UpdateAnimation();
    }

    // 🔥 corridor 체크
    public bool IsInCorridor(Transform target)
    {
        RaycastHit hit;

        if (Physics.Raycast(target.position, Vector3.down, out hit, 5f))
        {
            return hit.collider.gameObject.layer == LayerMask.NameToLayer("corridor");
        }

        return false;
    }

    // 🔥 배회 (완성)
    void Patrol()
    {
        agent.isStopped = false;

        actionTimer += Time.deltaTime;

        if (actionTimer >= actionTime)
        {
            actionTimer = 0f;
            actionTime = Random.Range(2f, 5f);

            patrolState = (PatrolType)Random.Range(0, 3);

            if (patrolState == PatrolType.IDLE)
            {
                agent.ResetPath();
            }
            else
            {
                Vector3 randomDir = Random.insideUnitSphere * 40f;
                randomDir += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDir, out hit, 40f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }

        if (patrolState == PatrolType.WALK)
            agent.speed = data.moveSpeed;

        if (patrolState == PatrolType.RUN)
            agent.speed = runSpeed;
    }

    // 🔥 추적
    void Chase()
    {
        if (player == null)
        {
            currentState = State.PATROL;
            return;
        }

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(player.position);
    }

    // 🔥 공격
    void Attack()
    {
        if (player == null)
        {
            currentState = State.PATROL;
            return;
        }

        agent.ResetPath();
        transform.LookAt(player);

        if (Time.time - lastAttackTime > attackCooldown)
        {
            lastAttackTime = Time.time;

            anim.ResetTrigger("attack");
            anim.SetTrigger("attack");
        }
    }

    // 🔥 감지
    public void OnDetectPlayer(Transform target)
    {
        if (currentState == State.ATTACK) return;

        if (IsInCorridor(target))
        {
            OnLosePlayer();
            return;
        }

        player = target;
        currentState = State.CHASE;
    }

    public void OnLosePlayer()
    {
        if (currentState == State.ATTACK) return;

        player = null;
        currentState = State.PATROL;
    }

    // 🔥 공격 진입
    public void OnAttackEnter()
    {
        if (player == null) return;

        currentState = State.ATTACK;
    }

    public void OnAttackExit()
    {
        currentState = State.CHASE;
    }

    // 🔥 애니메이션
    void UpdateAnimation()
    {
        if (currentState == State.ATTACK)
        {
            anim.SetBool("isRun", false);
            anim.SetBool("isWalk", false);
            return;
        }

        if (currentState == State.CHASE)
        {
            anim.SetBool("isRun", true);
            anim.SetBool("isWalk", false);
            return;
        }

        anim.SetBool("isRun", patrolState == PatrolType.RUN);
        anim.SetBool("isWalk", patrolState == PatrolType.WALK);
    }
}