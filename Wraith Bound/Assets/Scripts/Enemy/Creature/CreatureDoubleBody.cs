using UnityEngine;

/// <summary>
/// 추격할 때 몸이 하나 더 겹쳐 나타남 (탈춤귀신). 같은 자리에서 같은 동작을 하지만 관절 꺾임(CreatureJointSnap)은
/// 각자 무작위라 팔·부채·고개가 여러 개로 어긋나 보이고, 안광도 두 쌍이 겹쳐 번짐.
/// (테스트 중 복제본이 실수로 두 개 겹쳤던 모습을 사용자가 마음에 들어 해서 정식 연출로 만듦, 2026-11-07)
/// 실행 중 자기 자신을 복제해 보이는 부분(Animator·스킨 메쉬·발 붙이기·관절 꺾기·몸 늘어남·안광)만 남기고 자식으로 붙임.
/// AI·이동·판정은 원본만. 복제본 Animator는 원본의 State·속도 파라미터를 따라가고, 동작 타이밍만 살짝 엇나감.
/// </summary>
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureDoubleBody : MonoBehaviour
{
    [Tooltip("겹친 몸의 동작 타이밍 엇남 (정규화 시간)")]
    [SerializeField, Range(0f, 0.5f)] private float phaseOffset = 0.18f;
    [Tooltip("겹친 몸의 재생 속도 배수 — 1에서 살짝 벗어나면 시간이 지날수록 어긋남이 변함")]
    [SerializeField] private float speedRatio = 1.07f;

    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int SpeedHash = Animator.StringToHash(CreatureLocomotionSync.SpeedParameter);

    private Animator animator;
    private Animator twinAnimator;
    private GameObject twin;
    private bool hasSpeedParam;
    private bool wasChasing;
    private bool suppressed;

    /// <summary>겹친 몸을 잠시 숨김 (점프스케어 클로즈업에서 얼굴을 가리지 않게 — 두 몸이 하나로 합쳐지는 순간).</summary>
    public void SetSuppressed(bool value)
    {
        suppressed = value;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == SpeedHash)
            {
                hasSpeedParam = true;
            }
        }

        CreateTwin();
    }

    private void CreateTwin()
    {
        // 비활성 부모 아래서 복제해 Awake가 돌기 전에 필요 없는 컴포넌트를 떼어 냄
        var holder = new GameObject("DoubleBodyHolder");
        holder.SetActive(false);
        twin = Instantiate(gameObject, holder.transform);
        twin.name = name + "_Double";

        // 역순으로 지움 — 의존하는 쪽(예: MaskGhostSound → MonsterEnemy)이 먼저 지워져야 AI가 남지 않음
        // (AI가 남으면 겹친 몸이 두 번째 적으로 움직여 플레이어를 잡음)
        MonoBehaviour[] behaviours = twin.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            MonoBehaviour mb = behaviours[i];
            bool keep = mb is CreatureFootGrounding || mb is CreatureJointSnap
                || mb is CreatureChaseStretch || mb is CreatureEyeGlow;
            if (mb != null && !keep)
            {
                DestroyImmediate(mb);
            }
        }

        if (twin.GetComponentInChildren<EnemyBase>(true) != null)
        {
            Debug.LogError("[CreatureDoubleBody] 겹친 몸에서 AI를 떼지 못함 — 겹친 몸을 만들지 않음: " + name, this);
            Destroy(holder);
            twin = null;
            return;
        }

        foreach (Component c in twin.GetComponentsInChildren<Component>(true))
        {
            if (c is UnityEngine.AI.NavMeshAgent || c is Collider || c is Rigidbody || c is AudioSource
                || c is AudioListener || c is Light)
            {
                DestroyImmediate(c);
            }
        }

        // 원본 CreatureEyeGlow가 먼저 만들어 둔 눈 오브젝트가 같이 복사됐으면 지움 (복제본이 자기 것을 새로 만듦)
        foreach (Transform t in twin.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == "EyeGlow")
            {
                DestroyImmediate(t.gameObject);
            }
        }

        twin.transform.SetParent(transform, false);
        twin.transform.localPosition = Vector3.zero;
        twin.transform.localRotation = Quaternion.identity;
        twin.transform.localScale = Vector3.one;
        Destroy(holder);

        twinAnimator = twin.GetComponent<Animator>();
        twinAnimator.applyRootMotion = false;
        twinAnimator.cullingMode = animator.cullingMode;
        twin.SetActive(false);
    }

    private void Update()
    {
        if (twin == null)
        {
            return;
        }

        bool chasing = CreatureChaseState.IsChasing(animator);
        bool hold = CreatureChaseState.IsJumpscare && wasChasing;
        if (!CreatureChaseState.IsJumpscare)
        {
            wasChasing = chasing;
        }

        bool show = (chasing || hold) && !suppressed;
        if (twin.activeSelf != show)
        {
            twin.SetActive(show);
            if (show)
            {
                twinAnimator.SetInteger(StateHash, 2);
                twinAnimator.Play("Chase", 0, Mathf.Repeat(CurrentNormalizedTime() + phaseOffset, 1f));
            }
        }

        if (!show)
        {
            return;
        }

        twinAnimator.SetInteger(StateHash, animator.GetInteger(StateHash));
        twinAnimator.speed = animator.enabled ? animator.speed * speedRatio : speedRatio;
        if (hasSpeedParam)
        {
            twinAnimator.SetFloat(SpeedHash, animator.GetFloat(SpeedHash));
        }
    }

    private float CurrentNormalizedTime()
    {
        return animator.enabled ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
    }

    private void OnDestroy()
    {
        if (twin != null)
        {
            Destroy(twin);
        }
    }
}
