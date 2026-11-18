using System;
using UnityEngine;

/// <summary>
/// 추격 중 관절을 기괴하게 꺾으며 쫓아옴 (탈춤귀신). 달리기 애니메이션 위에 덧씌우는 실시간 레이어.
/// 2026-11-18 시연 반응("너무 빠르게 흔들려서 알아보기 어렵다")으로 느리게 → 너무 느려 "바람 빠진 풍선 같다"는 반응으로 중간쯤:
/// · 뼈마다 0.35~0.9초마다 새 자세를 골라 비틀어 감, 절반 가까이(suddenChance) 그 뼈만 '뚝' 빠르게 꺾임. 가끔 멈칫.
/// · 각도는 끝 쪽으로 치우치게 골라(extremeBias) 한 순간에 여러 관절이 크게 꺾여 보이게. 무릎·팔꿈치는 반대로도, 발가락까지.
/// · 발작: 3~5초마다 1초 안팎 — 간격 0.12~0.22초로 '경련'이 보이는 속도.
/// · 몸 전체 요동: 골반 위치가 좌우·앞뒤로 덜컥 튐(작게, 높이는 발 붙이기가 다시 맞춤).
/// · 강제 발작(SetForced — 점프스케어·프롤로그 마지막 간호사)은 예전 빠른 값(forced*)을 따로 씀.
/// 각도는 크리처 기준 축(roll=좌우 갸웃·pitch=앞뒤·yaw=비틀기). Animator가 매 프레임 포즈를 다시 쓰므로 누적 안 됨.
/// Animator가 꺼져 있으면(점프스케어) 쉼. 발 붙이기(CreatureFootGrounding)보다 먼저 실행.
/// </summary>
[DefaultExecutionOrder(-20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureJointSnap : MonoBehaviour
{
    [Serializable]
    public struct SnapBone
    {
        public HumanBodyBones bone;
        [Tooltip("최대 각도: x=roll(좌우 갸웃), y=pitch(앞뒤), z=yaw(비틀기)")]
        public Vector3 maxAngles;
    }

    [SerializeField] private SnapBone[] bones =
    {
        new SnapBone { bone = HumanBodyBones.Head, maxAngles = new Vector3(100f, 70f, 110f) },
        new SnapBone { bone = HumanBodyBones.Neck, maxAngles = new Vector3(45f, 45f, 60f) },
        new SnapBone { bone = HumanBodyBones.UpperChest, maxAngles = new Vector3(25f, 30f, 40f) },
        new SnapBone { bone = HumanBodyBones.Chest, maxAngles = new Vector3(30f, 35f, 45f) },
        new SnapBone { bone = HumanBodyBones.Spine, maxAngles = new Vector3(22f, 28f, 35f) },
        new SnapBone { bone = HumanBodyBones.Hips, maxAngles = new Vector3(14f, 18f, 35f) },
        new SnapBone { bone = HumanBodyBones.LeftShoulder, maxAngles = new Vector3(30f, 30f, 30f) },
        new SnapBone { bone = HumanBodyBones.RightShoulder, maxAngles = new Vector3(30f, 30f, 30f) },
        new SnapBone { bone = HumanBodyBones.LeftUpperArm, maxAngles = new Vector3(85f, 110f, 85f) },
        new SnapBone { bone = HumanBodyBones.RightUpperArm, maxAngles = new Vector3(85f, 110f, 85f) },
        new SnapBone { bone = HumanBodyBones.LeftLowerArm, maxAngles = new Vector3(45f, 140f, 60f) },   // 팔꿈치 역방향 포함
        new SnapBone { bone = HumanBodyBones.RightLowerArm, maxAngles = new Vector3(45f, 140f, 60f) },
        new SnapBone { bone = HumanBodyBones.LeftHand, maxAngles = new Vector3(60f, 90f, 60f) },
        new SnapBone { bone = HumanBodyBones.RightHand, maxAngles = new Vector3(60f, 90f, 60f) },
        new SnapBone { bone = HumanBodyBones.LeftUpperLeg, maxAngles = new Vector3(32f, 50f, 40f) },
        new SnapBone { bone = HumanBodyBones.RightUpperLeg, maxAngles = new Vector3(32f, 50f, 40f) },
        new SnapBone { bone = HumanBodyBones.LeftLowerLeg, maxAngles = new Vector3(18f, 85f, 30f) },    // 무릎 앞뒤로 — 역관절처럼
        new SnapBone { bone = HumanBodyBones.RightLowerLeg, maxAngles = new Vector3(18f, 85f, 30f) },
        new SnapBone { bone = HumanBodyBones.LeftFoot, maxAngles = new Vector3(35f, 60f, 35f) },
        new SnapBone { bone = HumanBodyBones.RightFoot, maxAngles = new Vector3(35f, 60f, 35f) },
        new SnapBone { bone = HumanBodyBones.LeftToes, maxAngles = new Vector3(15f, 70f, 15f) },        // 발가락 젖힘
        new SnapBone { bone = HumanBodyBones.RightToes, maxAngles = new Vector3(15f, 70f, 15f) },
    };

    [Header("평소 (추격 중) — 느린 몸부림")]
    [Tooltip("뼈마다 새 자세로 바뀌는 간격(초) 범위")]
    [SerializeField] private Vector2 interval = new Vector2(0.35f, 0.9f);
    [Tooltip("새 자세로 비틀어 가는 빠르기 — 작을수록 천천히")]
    [SerializeField] private float snapSpeed = 9f;
    [Tooltip("자세를 바꿀 때 이 확률로 그 뼈만 '뚝' 빠르게 꺾음")]
    [SerializeField, Range(0f, 1f)] private float suddenChance = 0.45f;
    [SerializeField] private float suddenSpeed = 55f;
    [Tooltip("각도를 끝 쪽으로 치우치게 (0=고르게, 1=거의 항상 최대) — 여러 관절이 한꺼번에 크게 꺾여 보이게")]
    [SerializeField, Range(0f, 1f)] private float extremeBias = 0.6f;
    [Tooltip("간격마다 이 확률로 각도를 바꾸지 않고 멈칫")]
    [SerializeField, Range(0f, 1f)] private float holdChance = 0.15f;
    [Tooltip("자세를 바꿀 때 이 확률로 꺾지 않고 바른 자세(0)로 돌아감 — 높이면 '가끔 뚝'만 (탈춤 추격과 함께 쓸 때)")]
    [SerializeField, Range(0f, 1f)] private float restChance = 0f;

    [Header("발작 (추격 중) — 가끔 경련, 알아볼 수 있는 속도")]
    [SerializeField] private Vector2 burstEvery = new Vector2(3f, 5f);
    [SerializeField] private Vector2 burstLength = new Vector2(0.6f, 1.0f);
    [SerializeField] private Vector2 burstInterval = new Vector2(0.12f, 0.22f);
    [SerializeField] private float burstAmplitude = 1.3f;
    [SerializeField] private float burstSnapSpeed = 25f;

    [Header("강제 발작 (SetForced — 점프스케어·프롤로그 마지막) — 예전 빠른 값 유지")]
    [SerializeField] private Vector2 forcedInterval = new Vector2(0.02f, 0.05f);
    [SerializeField] private float forcedSnapSpeed = 96f;
    [SerializeField] private float forcedAmplitudeBase = 1.35f;

    [Header("몸 전체 요동 (골반 위치, m — 크리처 기준 x=좌우 y=위아래 z=앞뒤)")]
    [SerializeField] private Vector3 hipJolt = new Vector3(0.12f, 0.05f, 0.1f);

    [Tooltip("추격 시작·끝에 꺾임이 켜지고 꺼지는 시간")]
    [SerializeField] private float fadeSeconds = 0.2f;

    private Animator animator;
    private Transform hips;
    private Transform[] transforms;
    private Vector3[] current;
    private Vector3[] target;
    private float[] nextChange;
    private float[] boneSpeed;
    private Vector3 joltCurrent;
    private Vector3 joltTarget;
    private float nextJolt;
    private float burstStart;
    private float burstEnd;
    private float weight;
    private bool forced;
    private float forcedAmplitude = 1f;
    private bool forcedJolt;

    private bool InBurst => Time.time >= burstStart && Time.time < burstEnd;

    /// <summary>
    /// 점프스케어용 강제 발작 — 추격 여부·점프스케어 중(timeScale 0)과 무관하게 실제 시간으로 계속 발작 간격·각도.
    /// amplitude 0이면 꺾임을 그 자리에서 멈춤(뚝 정지), jolt=false면 골반 요동 없음(얼굴 위치가 흔들리지 않게).
    /// </summary>
    public void SetForced(bool on, float amplitude = 1f, bool jolt = false)
    {
        if (on && !forced)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < nextChange.Length; i++)
            {
                nextChange[i] = now;
            }
            nextJolt = now;
        }

        forced = on;
        forcedAmplitude = amplitude;
        forcedJolt = jolt;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        transforms = new Transform[bones.Length];
        current = new Vector3[bones.Length];
        target = new Vector3[bones.Length];
        nextChange = new float[bones.Length];
        boneSpeed = new float[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            transforms[i] = animator.isHuman ? animator.GetBoneTransform(bones[i].bone) : null;
            boneSpeed[i] = snapSpeed;
            // 처음부터 다 같이 바뀌지 않게 엇갈림
            nextChange[i] = Time.time + UnityEngine.Random.Range(0f, interval.y);
        }

        ScheduleBurst(Time.time);
    }

    // −max~max 무작위, bias만큼 끝 쪽으로 치우침 (|r|^(1−bias))
    private static float Pick(float max, float bias)
    {
        float r = UnityEngine.Random.Range(-1f, 1f);
        return Mathf.Sign(r) * Mathf.Pow(Mathf.Abs(r), 1f - bias * 0.85f) * max;
    }

    private void ScheduleBurst(float from)
    {
        burstStart = from + UnityEngine.Random.Range(burstEvery.x, burstEvery.y);
        burstEnd = burstStart + UnityEngine.Random.Range(burstLength.x, burstLength.y);
    }

    /// <summary>지정한 뼈의 꺾임을 즉시 0으로 (점프스케어에서 멈출 때 고개만 바로 세워 탈이 정면을 보게).</summary>
    public void ResetBones(params HumanBodyBones[] which)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (System.Array.IndexOf(which, bones[i].bone) >= 0)
            {
                current[i] = Vector3.zero;
                target[i] = Vector3.zero;
            }
        }
    }

    private void LateUpdate()
    {
        if (!animator.enabled)
        {
            return;
        }

        if (forced)
        {
            weight = 1f;
        }
        else
        {
            bool on = CreatureChaseState.IsChasing(animator) && !CreatureChaseState.IsJumpscare;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        }
        if (weight <= 0f)
        {
            return;
        }

        float now = forced ? Time.unscaledTime : Time.time;
        float dt = forced ? Time.unscaledDeltaTime : Time.deltaTime;
        if (!forced && now >= burstEnd)
        {
            ScheduleBurst(now);
        }

        bool burst = !forced && InBurst;
        Vector2 gap = forced ? forcedInterval : burst ? burstInterval : interval;
        float amp = forced ? forcedAmplitudeBase * forcedAmplitude : burst ? burstAmplitude : 1f;
        bool frozen = forced && forcedAmplitude <= 0f;
        // 강제·발작은 모든 뼈 같은 빠르기, 평소엔 뼈마다(느리게, 가끔 '뚝')
        float sharedSpeed = forced ? forcedSnapSpeed : burst ? burstSnapSpeed : -1f;
        float k = frozen ? 0f : 1f - Mathf.Exp(-(sharedSpeed > 0f ? sharedSpeed : snapSpeed) * dt);
        Vector3 fwd = transform.forward, right = transform.right, up = transform.up;

        // 몸 전체 요동
        if (hips != null && (!forced || forcedJolt))
        {
            if (now >= nextJolt)
            {
                nextJolt = now + UnityEngine.Random.Range(gap.x, gap.y) * 1.5f;
                joltTarget = new Vector3(
                    UnityEngine.Random.Range(-hipJolt.x, hipJolt.x),
                    UnityEngine.Random.Range(-hipJolt.y, hipJolt.y),
                    UnityEngine.Random.Range(-hipJolt.z, hipJolt.z)) * amp;
            }

            joltCurrent = Vector3.Lerp(joltCurrent, joltTarget, k);
            Vector3 j = joltCurrent * weight;
            hips.position += right * j.x + up * j.y + fwd * j.z;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform bone = transforms[i];
            if (bone == null)
            {
                continue;
            }

            if (!frozen && now >= nextChange[i])
            {
                nextChange[i] = now + UnityEngine.Random.Range(gap.x, gap.y);
                if (forced || burst || UnityEngine.Random.value >= holdChance)
                {
                    Vector3 m = bones[i].maxAngles * amp;
                    float bias = forced ? 0f : extremeBias; // 강제 발작은 예전처럼 고르게
                    bool rest = !forced && !burst && UnityEngine.Random.value < restChance;
                    target[i] = rest ? Vector3.zero : new Vector3(Pick(m.x, bias), Pick(m.y, bias), Pick(m.z, bias));
                    boneSpeed[i] = UnityEngine.Random.value < suddenChance ? suddenSpeed : snapSpeed;
                }
            }

            float kb = frozen ? 0f : sharedSpeed > 0f ? k : 1f - Mathf.Exp(-boneSpeed[i] * dt);
            current[i] = Vector3.Lerp(current[i], target[i], kb);
            Vector3 a = current[i] * weight;
            bone.rotation =
                Quaternion.AngleAxis(a.z, up) *
                Quaternion.AngleAxis(a.x, fwd) *
                Quaternion.AngleAxis(a.y, right) * bone.rotation;
        }
    }
}
