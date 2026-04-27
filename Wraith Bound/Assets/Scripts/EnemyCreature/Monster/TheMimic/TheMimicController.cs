using UnityEngine;
using UnityEngine.AI;

public class MonsterController : MonoBehaviour
{
    public Transform player;
    public float detectRange = 15f;
    public float attackRange = 2f;

    private NavMeshAgent agent;
    private Animator animator;

    private bool isJumping = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (isJumping) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist < detectRange)
        {
            agent.SetDestination(player.position);
            animator.SetBool("Run", true);

            // 🔥 문 앞 or 공격 거리
            if (dist < attackRange)
            {
                JumpAttack();
            }
        }
        else
        {
            animator.SetBool("Run", false);
        }
    }

    void JumpAttack()
    {
        isJumping = true;

        agent.enabled = false; // 💣 이거 핵심

        animator.SetTrigger("Jump");
    }

    // 👉 애니메이션 끝날 때 이벤트로 호출
    public void EndJump()
    {
        isJumping = false;
        agent.enabled = true;
    }

    // 👉 문 부수는 타이밍 (애니메이션 이벤트)
    public void BreakDoor()
    {
        Debug.Log("문 파괴!");
    }
}