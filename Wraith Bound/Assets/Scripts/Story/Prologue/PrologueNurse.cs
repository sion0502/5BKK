using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 프롤로그 연출 전용 NurseGhost 조종. 같은 오브젝트의 일반 AI(EnemyBase 계열)는 끄고(수정하지 않음) NavMeshAgent·Animator만 씀.
/// 애니메이터 State: 0 Idle / 1 Patrol(절뚝 걷기) / 2 Chase(달리기), Attack 트리거.
/// PrologueDirector가 WalkTo / Chase / Stop / Vanish 로 지시하고, 추격 중 잡으면 Caught 이벤트.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PrologueNurse : MonoBehaviour
{
    private static readonly int StateId = Animator.StringToHash("State");
    private static readonly int AttackId = Animator.StringToHash("Attack");

    [SerializeField] private float walkSpeed = 1.3f;
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private float sightRange = 11f;
    [SerializeField] private Transform eyePoint;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip screamClip;

    public event Action Caught;

    public bool IsChasing => chaseTarget != null;
    public bool HasArrived => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;
    public Transform EyePoint => eyePoint != null ? eyePoint : transform;

    private NavMeshAgent agent;
    private Animator animator;
    private Transform chaseTarget;
    private Action arrived;
    private bool caughtFired;
    private bool turning;
    private Quaternion turnTarget;
    [Tooltip("TurnTowards로 제자리에서 도는 빠르기(도/초) — 빠르면 디딘 발이 미끄러져 보임")]
    [SerializeField] private float turnSpeed = 110f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false; // 이동은 NavMeshAgent가 (발 붙이기는 NurseGhost 프리팹의 CreatureFootGrounding)
        }

        // 일반 적 AI(팀원 코드)는 이 개체에선 끔 — 연출대로만 움직이게
        foreach (EnemyBase enemy in GetComponents<EnemyBase>())
        {
            enemy.enabled = false;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
        }

        // 경유 지점마다 멈칫하지 않게 (지점 도착 판정은 약간 넉넉히)
        agent.stoppingDistance = 0.25f;
        agent.autoBraking = false;
    }

    public void Appear(Vector3 position, Quaternion rotation)
    {
        gameObject.SetActive(true);
        agent.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        agent.enabled = true;
        agent.Warp(position);
        Stop();
    }

    public Animator Animator => animator;

    /// <summary>컷씬용: NavMesh에서 떼고 지정한 자리에 세움(이후 위치는 연출이 직접 옮김). 대기 자세.</summary>
    public void Pin(Vector3 position, Vector3 lookAt)
    {
        gameObject.SetActive(true);
        chaseTarget = null;
        arrived = null;
        agent.enabled = false;
        transform.position = position;
        FaceTowards(lookAt);
        SetState(0);
    }

    public void SetAnimatorState(int state)
    {
        SetState(state);
    }

    public void Vanish()
    {
        chaseTarget = null;
        arrived = null;
        gameObject.SetActive(false);
    }

    public void WalkTo(Vector3 destination, Action onArrived = null)
    {
        chaseTarget = null;
        turning = false;
        arrived = onArrived;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.SetDestination(destination);
        SetState(1);
    }

    public void Chase(Transform target, float speed, bool scream = true)
    {
        chaseTarget = target;
        arrived = null;
        caughtFired = false;
        agent.isStopped = false;
        agent.speed = speed;
        SetState(2);

        if (scream && audioSource != null && screamClip != null)
        {
            audioSource.PlayOneShot(screamClip);
        }
    }

    public void SetChaseSpeed(float speed)
    {
        agent.speed = speed;
    }

    public void Stop()
    {
        chaseTarget = null;
        arrived = null;
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        SetState(0);
    }

    /// <summary>제자리에서 천천히 돌아섬 (멈춘 상태에서만, 초당 turnSpeed도). 순간적으로 확 도는 FaceTowards 대신 숨는 곳 앞 연출용.</summary>
    public void TurnTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            turnTarget = Quaternion.LookRotation(dir);
            turning = true;
        }
    }

    public bool IsTurning => turning;

    public void FaceTowards(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    public void PlayAttack()
    {
        Stop();
        animator.SetTrigger(AttackId);
    }

    /// <summary>눈 위치에서 대상까지 가리는 것 없이 보이는지 (sightRange 안).</summary>
    public bool CanSee(Transform target)
    {
        Vector3 eye = EyePoint.position;
        Vector3 to = target.position + Vector3.up * 1.4f - eye;
        if (to.magnitude > sightRange)
        {
            return false;
        }

        if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        return true;
    }

    private void Update()
    {
        if (turning)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, turnTarget, turnSpeed * Time.deltaTime);
            turning = Quaternion.Angle(transform.rotation, turnTarget) > 0.5f;
        }

        if (chaseTarget != null)
        {
            agent.SetDestination(chaseTarget.position);
            Vector3 flat = chaseTarget.position - transform.position;
            flat.y = 0f;
            if (!caughtFired && flat.magnitude <= catchDistance)
            {
                caughtFired = true;
                Caught?.Invoke();
            }

            return;
        }

        if (arrived != null && HasArrived)
        {
            Action callback = arrived;
            arrived = null;
            SetState(0);
            callback();
        }
    }

    private void SetState(int state)
    {
        if (animator != null)
        {
            animator.SetInteger(StateId, state);
        }
    }
}
