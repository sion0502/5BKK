using UnityEngine;

public class MonsterEnemy : EnemyBase
{
    [Header("Data")]
    [SerializeField] float moveSpeed = 12f;
    [SerializeField] float detectRange = 120f;
    [SerializeField] float detectAngle = 100f;

    [Header("Door Attack")]
    [SerializeField] LayerMask doorLayer;
    [SerializeField] string attackTrigger = "Attack";
    [SerializeField] float attackDuration = 1.5f;

    bool isAttackingDoor = false;
    float attackTimer = 0f;

    protected override void Start()
    {
        base.Start();
        runSpeed = moveSpeed;
        walkSpeed = moveSpeed * 0.4f;
        viewDistance = detectRange;
        viewAngle = detectAngle;
    }

    protected override bool IsActionBlocked()
    {
        return isAttackingDoor;
    }

    protected override void Update()
    {
        if (isAttackingDoor)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttackingDoor = false;
                agent.isStopped = false;
            }
        }

        base.Update();
    }

    protected override void OnChaseUpdate()
    {
        if (isAttackingDoor) return;

        // 플레이어 위치로 강제 따라잡기: 벽에 비껴가지 않도록 문 방향이 아니라 플레이어 방향으로 경로 재설정
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        dirToPlayer.y = 0f;

        // 문 탐지: 문 방향으로 접근하는 로직은 필요시 확장
        Collider[] hits = Physics.OverlapSphere(transform.position + dirToPlayer * 1.2f, 1.0f, doorLayer);
        foreach (var hit in hits)
        {
            DoorController door = hit.GetComponentInParent<DoorController>();
            if (door != null && !door.IsBroken())
            {
                isAttackingDoor = true;
                attackTimer = attackDuration;
                agent.isStopped = true;
                if (animator != null) animator.SetTrigger(attackTrigger);
                door.TakeDamage(999);
                forcedChaseTimer = 2f;
                return;
            }
        }

        // 플레이어 위치를 지속해서 따라가게 이미 SetDestination이 호출되어 있음
        agent.SetDestination(player.position);
        lastKnownPosition = player.position;
    }
}
