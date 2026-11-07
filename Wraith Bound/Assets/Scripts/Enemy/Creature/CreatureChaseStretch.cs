using System;
using UnityEngine;

/// <summary>
/// 추격할 때만 몸이 기괴하게 길어짐 (탈춤귀신). 뼈를 늘리는(스케일) 대신 관절 사이 간격(자식 뼈 localPosition)을
/// 벌려서, 팔다리가 굵어지지 않고 가늘고 길게 늘어남. 늘어날 때는 매끄럽게가 아니라 몇 번에 걸쳐 '덜컥덜컥'.
/// 추격이 끝나면 원래대로. 점프스케어 중엔 추격하던 모습 그대로 유지.
/// 휴머노이드 Animator는 Hips 외 뼈 위치를 매 프레임 쓰지 않아서 값을 직접 넣어 둠.
/// 다리가 길어지면 발이 바닥에 묻히므로 CreatureFootGrounding의 maxRaise가 충분해야 함.
/// </summary>
[DefaultExecutionOrder(-30)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureChaseStretch : MonoBehaviour
{
    [Serializable]
    public struct StretchBone
    {
        [Tooltip("이 뼈의 부모와의 간격(localPosition)을 늘림 — 예: LowerArm이면 위팔이 길어짐")]
        public HumanBodyBones bone;
        public float factor;
    }

    [SerializeField] private StretchBone[] bones =
    {
        new StretchBone { bone = HumanBodyBones.Spine, factor = 1.6f },
        new StretchBone { bone = HumanBodyBones.Chest, factor = 1.6f },
        new StretchBone { bone = HumanBodyBones.UpperChest, factor = 1.6f },
        new StretchBone { bone = HumanBodyBones.Neck, factor = 1.4f },
        new StretchBone { bone = HumanBodyBones.Head, factor = 2.6f },          // 목이 길게
        new StretchBone { bone = HumanBodyBones.LeftLowerArm, factor = 1.7f },  // 위팔
        new StretchBone { bone = HumanBodyBones.RightLowerArm, factor = 1.7f },
        new StretchBone { bone = HumanBodyBones.LeftHand, factor = 1.8f },      // 아래팔
        new StretchBone { bone = HumanBodyBones.RightHand, factor = 1.8f },
        new StretchBone { bone = HumanBodyBones.LeftLowerLeg, factor = 1.2f },
        new StretchBone { bone = HumanBodyBones.RightLowerLeg, factor = 1.2f },
        new StretchBone { bone = HumanBodyBones.LeftFoot, factor = 1.2f },
        new StretchBone { bone = HumanBodyBones.RightFoot, factor = 1.2f },
    };

    [Tooltip("다 늘어나는 데 걸리는 시간")]
    [SerializeField] private float growSeconds = 0.45f;
    [Tooltip("늘어나는 동안 덜컥거리는 단계 수 (0이면 매끄럽게)")]
    [SerializeField] private int growSteps = 5;
    [SerializeField] private float shrinkSeconds = 0.6f;

    private Animator animator;
    private Transform[] transforms;
    private Vector3[] rest;
    private float progress;
    private bool wasChasing;

    /// <summary>0=원래, 1=다 늘어남.</summary>
    public float Amount => Stepped(progress);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        transforms = new Transform[bones.Length];
        rest = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            Transform t = animator.isHuman ? animator.GetBoneTransform(bones[i].bone) : null;
            transforms[i] = t;
            if (t != null)
            {
                rest[i] = t.localPosition;
            }
        }
    }

    private void OnDisable()
    {
        progress = 0f;
        Apply(0f);
    }

    private void LateUpdate()
    {
        bool chasing = CreatureChaseState.IsChasing(animator);
        bool hold = CreatureChaseState.IsJumpscare && wasChasing;
        bool on = chasing || hold;
        if (!CreatureChaseState.IsJumpscare)
        {
            wasChasing = chasing;
        }

        float speed = on ? 1f / Mathf.Max(0.01f, growSeconds) : -1f / Mathf.Max(0.01f, shrinkSeconds);
        progress = Mathf.Clamp01(progress + speed * Time.deltaTime);
        Apply(Stepped(progress));
    }

    private float Stepped(float t)
    {
        if (growSteps <= 0 || t <= 0f || t >= 1f)
        {
            return t;
        }

        return Mathf.Ceil(t * growSteps) / growSteps;
    }

    private void Apply(float amount)
    {
        if (transforms == null)
        {
            return;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
            {
                transforms[i].localPosition = rest[i] * Mathf.Lerp(1f, bones[i].factor, amount);
            }
        }
    }
}
