using UnityEngine;

/// <summary>
/// 추격 중 팔꿈치 아래(아래팔·손)가 풍선처럼 흐느적거림 (탈춤귀신, 2026-11-18 사용자 요청 "팔이 중간지점부터는 풍선처럼 흔들리게").
/// 위팔은 춤 그대로, 아래팔 방향(팔꿈치→손목)만 스프링으로 늦게 따라가며 출렁 + 풍선처럼 살짝 떠오름(buoyancy) + 느린 떠다니는 흔들림(wobble).
/// 손은 아래팔이 휜 만큼 더 휘어 채찍처럼 끝이 더 흔들림(handFollow). 춤(−25)·꺾임(−20)·몸 늘어남(−30) 뒤에 덧씌움.
/// 추격 중에만, 점프스케어 중엔 끔.
/// </summary>
[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureFloppyArms : MonoBehaviour
{
    [Tooltip("원래 자세로 끌려가는 빠르기(고유 진동수, rad/s) — 작을수록 더 늘어지고 늦게 따라옴 (6 ≈ 1초에 한 번 출렁)")]
    [SerializeField] private float stiffness = 6f;
    [Tooltip("출렁임이 잦아드는 정도 — 작을수록 오래 출렁 (2×stiffness면 출렁임 없음)")]
    [SerializeField] private float damping = 3f;
    [Tooltip("풍선처럼 위로 뜨는 정도 (방향에 더하는 위쪽 성분)")]
    [SerializeField] private float buoyancy = 0.35f;
    [Tooltip("떠다니는 흔들림 각도(도)·빠르기(Hz)")]
    [SerializeField] private float wobbleAngle = 28f;
    [SerializeField] private float wobbleHz = 0.6f;
    [Tooltip("손이 아래팔 휨을 따라 더 휘는 비율")]
    [SerializeField, Range(0f, 1.5f)] private float handFollow = 0.7f;
    [Tooltip("한 번에 휠 수 있는 최대 각도(도) — 너무 꺾여 팔이 뒤집히지 않게")]
    [SerializeField] private float maxBend = 110f;
    [SerializeField] private float fadeSeconds = 0.3f;

    private class Arm
    {
        public Transform fore;
        public Transform hand;
        public Vector3 dir;
        public Vector3 vel;
        public bool ready;
        public float seed;
    }

    private Animator animator;
    private Arm left;
    private Arm right;
    private float weight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            enabled = false;
            return;
        }

        left = new Arm { fore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = animator.GetBoneTransform(HumanBodyBones.LeftHand), seed = 3.7f };
        right = new Arm { fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm), hand = animator.GetBoneTransform(HumanBodyBones.RightHand), seed = 11.3f };
    }

    private void LateUpdate()
    {
        if (!animator.enabled)
        {
            return;
        }

        bool on = CreatureChaseState.IsChasing(animator) && !CreatureChaseState.IsJumpscare;
        weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (weight <= 0f)
        {
            left.ready = right.ready = false;
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Simulate(left, dt);
        Simulate(right, dt);
    }

    private void Simulate(Arm arm, float dt)
    {
        if (arm.fore == null || arm.hand == null)
        {
            return;
        }

        Vector3 anim = arm.hand.position - arm.fore.position;
        float len = anim.magnitude;
        if (len < 1e-4f)
        {
            return;
        }

        anim /= len;
        if (!arm.ready)
        {
            arm.dir = anim;
            arm.vel = Vector3.zero;
            arm.ready = true;
        }

        // 목표 방향 = 춤이 만든 방향 + 위로 뜨는 성분, 느린 떠다니는 흔들림으로 비틂
        Vector3 target = (anim + transform.up * buoyancy).normalized;
        float t = Time.time * wobbleHz;
        Vector3 axisA = Vector3.Cross(target, transform.up);
        if (axisA.sqrMagnitude < 1e-4f)
        {
            axisA = transform.right;
        }

        axisA.Normalize();
        Vector3 axisB = Vector3.Cross(target, axisA).normalized;
        float wa = (Mathf.PerlinNoise(t, arm.seed) - 0.5f) * 2f * wobbleAngle;
        float wb = (Mathf.PerlinNoise(arm.seed, t) - 0.5f) * 2f * wobbleAngle;
        target = Quaternion.AngleAxis(wa, axisA) * Quaternion.AngleAxis(wb, axisB) * target;

        // 감쇠 스프링 — 방향 벡터를 목표 쪽으로 끌어당기고(가속도 = 차이 × stiffness²) 출렁임을 남김
        arm.vel += (target - arm.dir) * (stiffness * stiffness * dt);
        arm.vel *= Mathf.Exp(-damping * dt);
        arm.dir = (arm.dir + arm.vel * dt).normalized;

        // 너무 꺾이지 않게
        float bend = Vector3.Angle(anim, arm.dir);
        if (bend > maxBend)
        {
            arm.dir = Vector3.Slerp(anim, arm.dir, maxBend / bend).normalized;
        }

        Quaternion offset = Quaternion.FromToRotation(anim, Vector3.Slerp(anim, arm.dir, weight));
        arm.fore.rotation = offset * arm.fore.rotation;

        // 손은 아래팔이 휜 만큼 더 휨(채찍 끝)
        if (handFollow > 0f)
        {
            offset.ToAngleAxis(out float a, out Vector3 axis);
            if (a > 180f)
            {
                a -= 360f;
            }

            arm.hand.rotation = Quaternion.AngleAxis(a * handFollow, axis) * arm.hand.rotation;
        }
    }
}
