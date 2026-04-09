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
    [Header("참조")]
    public Monsters data;
    public Transform player;

    private NavMeshAgent agent;
    private Animator anim;

    [Header("속도 설정 🔥 여기서 수정하세요")]
    public float walkSpeed = 6f; // 👈 걷기 속도 (여기 바꾸면 됨)
    public float runSpeed = 12f;  // 👈 뛰기 속도 (여기 바꾸면 됨)

    private State state;

    private float patrolTimer;
    private float suspiciousTimer;
    private float suspiciousMoveTimer;

    private Vector3 lastKnownPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        state = State.PATROL;
        agent.speed = walkSpeed;
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        switch (state)
        {
            case State.PATROL:
                HandlePatrol(distance);
                break;

            case State.CHASE:
                HandleChase(distance);
                break;

            case State.SUSPICIOUS:
                HandleSuspicious(distance);
                break;
        }

        UpdateAnimation();
    }

    // =========================
    // 감지
    // =========================
    bool CanDetectPlayer(float distance)
    {
        // 가까우면 무조건 감지
        if (distance < data.detectRange * 0.7f)
            return true;

        // 범위 밖
        if (distance > data.detectRange)
            return false;

        // 부채꼴 시야
        Vector3 dir = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dir);

        return angle < data.viewAngle * 0.5f;
    }

    // =========================
    // PATROL
    // =========================
    void HandlePatrol(float distance)
    {
        patrolTimer += Time.deltaTime;

        agent.isStopped = false;
        agent.speed = walkSpeed;

        if (patrolTimer > data.checkInterval)
        {
            patrolTimer = 0f;

            if (Random.value < 0.3f)
            {
                agent.ResetPath(); // Idle
            }
            else
            {
                MoveRandom(transform.position, 10f);
            }
        }

        if (CanDetectPlayer(distance))
            state = State.CHASE;
    }

    // =========================
    // CHASE
    // =========================
    void HandleChase(float distance)
    {
        agent.isStopped = false;
        agent.speed = runSpeed;

        // 🔥 문 감지 (SphereCast로 안정성 ↑)
        if (DetectDoor())
            return;

        agent.SetDestination(player.position);

        // 공격 거리
        if (distance < 2f)
        {
            anim.SetTrigger("Attack");
            Debug.Log("플레이어 사망");
        }

        // 너무 멀어지면 의심모드
        if (distance > data.detectRange * 1.8f)
        {
            lastKnownPos = player.position;
            suspiciousTimer = 0f;
            suspiciousMoveTimer = 0f;
            state = State.SUSPICIOUS;
        }
    }

    // =========================
    // SUSPICIOUS
    // =========================
    void HandleSuspicious(float distance)
    {
        suspiciousTimer += Time.deltaTime;
        suspiciousMoveTimer += Time.deltaTime;

        agent.isStopped = false;
        agent.speed = walkSpeed;

        // 주변 탐색
        if (suspiciousMoveTimer > 1.5f)
        {
            suspiciousMoveTimer = 0f;
            MoveRandom(lastKnownPos, 6f);
        }

        if (CanDetectPlayer(distance))
            state = State.CHASE;

        if (suspiciousTimer > 6f)
            state = State.PATROL;
    }

    // =========================
    // 랜덤 이동 함수 (재사용)
    // =========================
    void MoveRandom(Vector3 center, float radius)
    {
        Vector3 randomDir = center + Random.insideUnitSphere * radius;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // =========================
    // 문 감지 (핵심)
    // =========================
    bool DetectDoor()
    {
        if (Physics.SphereCast(transform.position + Vector3.up, 0.8f, transform.forward, out RaycastHit hit, 3f))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Door"))
            {
                agent.isStopped = true;
                anim.SetTrigger("Attack");

                DoorController door = hit.collider.GetComponent<DoorController>();
                if (door != null)
                    door.TakeDamage(1);

                return true;
            }
        }

        return false;
    }

    // =========================
    // 애니메이션 (상태 기반)
    // =========================
    void UpdateAnimation()
    {
        switch (state)
        {
            case State.PATROL:
            case State.SUSPICIOUS:
                if (agent.velocity.magnitude < 0.1f)
                    anim.SetFloat("Speed", 0f);   // Idle
                else
                    anim.SetFloat("Speed", walkSpeed); // Walk
                break;

            case State.CHASE:
                anim.SetFloat("Speed", runSpeed); // Run
                break;
        }
    }
}