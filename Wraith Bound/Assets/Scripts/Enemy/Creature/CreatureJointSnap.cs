using System;
using UnityEngine;

/// <summary>
/// 추격 중 관절을 매 순간 '뚝뚝' 꺾으며 발광하듯 쫓아옴 (탈춤귀신). 달리기 애니메이션 위에 덧씌우는 실시간 레이어.
/// · 뼈마다 따로 짧은 간격으로 새 각도를 무작위로 골라 거의 순간적으로 꺾임(가끔 멈칫). 무릎·팔꿈치는 반대로도 꺾임.
/// · 발작: 몇 초마다 잠깐 훨씬 빠르고 크게 경련.
/// · 몸 전체 요동: 골반 위치가 좌우·앞뒤로 덜컥 튐(높이는 발 붙이기가 다시 맞춤).
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
        new SnapBone { bone = HumanBodyBones.LeftFoot, maxAngles = new Vector3(30f, 45f, 30f) },
        new SnapBone { bone = HumanBodyBones.RightFoot, maxAngles = new Vector3(30f, 45f, 30f) },
    };

    [Header("평소")]
    [Tooltip("새 각도로 바뀌는 간격(초) 범위")]
    [SerializeField] private Vector2 interval = new Vector2(0.05f, 0.16f);
    [Tooltip("새 각도로 꺾이는 빠르기 — 클수록 '뚝'")]
    [SerializeField] private float snapSpeed = 60f;
    [Tooltip("간격마다 이 확률로 각도를 바꾸지 않고 멈칫")]
    [SerializeField, Range(0f, 1f)] private float holdChance = 0.15f;

    [Header("발작 — 몇 초마다 잠깐 훨씬 빠르고 크게")]
    [SerializeField] private Vector2 burstEvery = new Vector2(1.0f, 2.5f);
    [SerializeField] private Vector2 burstLength = new Vector2(0.3f, 0.6f);
    [SerializeField] private Vector2 burstInterval = new Vector2(0.02f, 0.05f);
    [SerializeField] private float burstAmplitude = 1.35f;

    [Header("몸 전체 요동 (골반 위치, m — 크리처 기준 x=좌우 y=위아래 z=앞뒤)")]
    [SerializeField] private Vector3 hipJolt = new Vector3(0.16f, 0.08f, 0.14f);

    [Tooltip("추격 시작·끝에 꺾임이 켜지고 꺼지는 시간")]
    [SerializeField] private float fadeSeconds = 0.2f;

    private Animator animator;
    private Transform hips;
    private Transform[] transforms;
    private Vector3[] current;
    private Vector3[] target;
    private float[] nextChange;
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
        for (int i = 0; i < bones.Length; i++)
        {
            transforms[i] = animator.isHuman ? animator.GetBoneTransform(bones[i].bone) : null;
        }

        ScheduleBurst(Time.time);
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

        bool burst = forced || InBurst;
        Vector2 gap = burst ? burstInterval : interval;
        float amp = forced ? burstAmplitude * forcedAmplitude : burst ? burstAmplitude : 1f;
        bool frozen = forced && forcedAmplitude <= 0f;
        float k = frozen ? 0f : 1f - Mathf.Exp(-(burst ? snapSpeed * 1.6f : snapSpeed) * dt);
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
                if (burst || UnityEngine.Random.value >= holdChance)
                {
                    Vector3 m = bones[i].maxAngles * amp;
                    target[i] = new Vector3(
                        UnityEngine.Random.Range(-m.x, m.x),
                        UnityEngine.Random.Range(-m.y, m.y),
                        UnityEngine.Random.Range(-m.z, m.z));
                }
            }

            current[i] = Vector3.Lerp(current[i], target[i], k);
            Vector3 a = current[i] * weight;
            bone.rotation =
                Quaternion.AngleAxis(a.z, up) *
                Quaternion.AngleAxis(a.x, fwd) *
                Quaternion.AngleAxis(a.y, right) * bone.rotation;
        }
    }
}
