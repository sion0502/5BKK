using UnityEngine;

/// <summary>
/// 추격 중 탈춤을 추듯 쫓아옴 (탈춤귀신, 2026-11-18 — "진짜 탈춤을 추듯이 추격했으면" 사용자 요청).
/// 달리기 애니메이션 위에 장단(beatHz, 약 1초 주기)에 맞춘 춤사위를 덧씌우는 실시간 레이어:
///  · 어깨춤: 양 어깨를 번갈아 들썩 · 팔: 부채 든 팔을 옆으로 높이 들고 장단에 맞춰 앞뒤로 휘두름(팔꿈치·손목도 꺾음)
///  · 몸통: 좌우로 흔들고 비틂 · 고개: 첫 박(덩)에 '뚝' 끄덕이고 몸과 반대로 갸웃 · 다리는 달리기 그대로(무릎 들기는 부자연스러워 뺌)
/// 각도는 크리처 기준 축(roll=앞축 회전·pitch=오른축 회전·yaw=위축 회전, CreatureJointSnap과 같은 방식)으로 뼈 월드 회전에 곱함.
/// Animator가 매 프레임 포즈를 다시 써서 누적 안 됨. 몸 늘어남(−30) 뒤, 관절 '뚝' 꺾임(−20) 앞.
/// 점프스케어 중·추격 아닐 땐 꺼짐(천천히 들어오고 빠짐).
/// </summary>
[DefaultExecutionOrder(-25)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureTalDance : MonoBehaviour
{
    [Tooltip("장단 빠르기 (초당 박)")]
    [SerializeField] private float beatHz = 1.1f;
    [Tooltip("전체 세기 배수")]
    [SerializeField] private float strength = 1f;

    [Header("팔 (도)")]
    [Tooltip("팔을 옆으로 드는 기본 각도 + 박에 따라 출렁이는 폭")]
    [SerializeField] private float armRaise = 70f;
    [SerializeField] private float armBob = 20f;
    [Tooltip("팔을 앞뒤로 휘두르는 폭")]
    [SerializeField] private float armSwing = 40f;
    [SerializeField] private float elbowBend = 50f;
    [SerializeField] private float wristFlick = 45f;
    [SerializeField] private float shoulderShrug = 18f;

    [Header("몸통·고개 (도)")]
    [SerializeField] private float torsoSway = 12f;
    [SerializeField] private float torsoTwist = 12f;
    [SerializeField] private float headTilt = 25f;
    [SerializeField] private float headNod = 22f;

    [Header("다리 (도)")]
    [SerializeField] private float kneeLift = 0f;

    [SerializeField] private float fadeSeconds = 0.35f;

    private Animator animator;
    private Transform head, neck, chest, spine;
    private Transform lShoulder, rShoulder, lArm, rArm, lFore, rFore, lHand, rHand;
    private Transform lThigh, rThigh, lShin, rShin;
    private float weight;
    private float phase;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            enabled = false;
            return;
        }

        head = Bone(HumanBodyBones.Head);
        neck = Bone(HumanBodyBones.Neck);
        chest = Bone(HumanBodyBones.Chest);
        spine = Bone(HumanBodyBones.Spine);
        lShoulder = Bone(HumanBodyBones.LeftShoulder);
        rShoulder = Bone(HumanBodyBones.RightShoulder);
        lArm = Bone(HumanBodyBones.LeftUpperArm);
        rArm = Bone(HumanBodyBones.RightUpperArm);
        lFore = Bone(HumanBodyBones.LeftLowerArm);
        rFore = Bone(HumanBodyBones.RightLowerArm);
        lHand = Bone(HumanBodyBones.LeftHand);
        rHand = Bone(HumanBodyBones.RightHand);
        lThigh = Bone(HumanBodyBones.LeftUpperLeg);
        rThigh = Bone(HumanBodyBones.RightUpperLeg);
        lShin = Bone(HumanBodyBones.LeftLowerLeg);
        rShin = Bone(HumanBodyBones.RightLowerLeg);
    }

    private Transform Bone(HumanBodyBones b)
    {
        return animator.GetBoneTransform(b);
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
            return;
        }

        phase += Time.deltaTime * beatHz;
        float w = weight * strength;
        float t = phase * Mathf.PI * 2f;
        float beat = Mathf.Sin(t);                       // 한 박 = 한 주기, 좌우 번갈아
        float half = Mathf.Sin(t * 0.5f);                // 두 박에 한 번 (팔 휘두름·몸 비틀기)
        float f = Mathf.Repeat(phase, 1f);
        float accent = Mathf.Exp(-f * 9f);               // 박 첫머리 '덩' — 뚝 들어갔다 풀림
        float liftL = Mathf.Max(0f, beat);
        float liftR = Mathf.Max(0f, -beat);

        Vector3 fwd = transform.forward, right = transform.right, up = transform.up;

        // 몸통: 좌우로 흔들고(roll) 비틂(yaw)
        Rotate(spine, torsoSway * 0.5f * beat, 0f, torsoTwist * 0.5f * half, w, fwd, right, up);
        Rotate(chest, torsoSway * 0.5f * beat, 0f, torsoTwist * 0.5f * half, w, fwd, right, up);

        // 어깨춤: 박에 맞춰 한쪽씩 들썩
        Rotate(lShoulder, -shoulderShrug * liftL, 0f, 0f, w, fwd, right, up);
        Rotate(rShoulder, shoulderShrug * liftR, 0f, 0f, w, fwd, right, up);

        // 팔: 옆으로 높이 들고(roll) 박에 출렁, 두 박에 한 번 앞뒤로 휘두름(pitch) — 좌우 반대
        Rotate(lArm, -(armRaise + armBob * beat), armSwing * half, 0f, w, fwd, right, up);
        Rotate(rArm, armRaise - armBob * beat, -armSwing * half, 0f, w, fwd, right, up);
        // 팔꿈치: 고정 축으로 굽히면 팔이 그 축 방향으로 뻗어 있을 때 팔뚝이 비틀리기만 하고 안 굽음(팔꿈치가 없어 보였음 —
        // 옆으로 든 팔은 오른축, 머리 위로 든 팔은 앞축에서). → 매 프레임 팔뚝에 수직인 축을 구해 머리 쪽으로 접음
        Vector3 bendTo = head != null ? head.position + up * 0.3f : transform.position + up * 3f;
        Bend(lArm, lFore, lHand, bendTo, elbowBend * (0.8f + 0.5f * liftL) * w);
        Bend(rArm, rFore, rHand, bendTo, elbowBend * (0.8f + 0.5f * liftR) * w);
        // 손목: 같은 이유로 앞축으로 젖힘 — 첫 박에 '착' 뿌림
        Rotate(lHand, -wristFlick * (0.3f + accent), 0f, wristFlick * 0.5f * half, w, fwd, right, up);
        Rotate(rHand, wristFlick * (0.3f + accent), 0f, -wristFlick * 0.5f * half, w, fwd, right, up);

        // 고개: 몸과 반대로 갸웃 + 첫 박에 뚝 끄덕
        Rotate(neck, -headTilt * 0.4f * beat, headNod * 0.4f * accent, 0f, w, fwd, right, up);
        Rotate(head, -headTilt * 0.6f * beat, headNod * 0.6f * accent, -torsoTwist * 0.5f * half, w, fwd, right, up);

        // 다리는 건드리지 않음 — 달리기 위에 무릎 들기를 얹었더니 부자연스럽다는 반응(2026-11-18). kneeLift > 0이면만 씀
        if (kneeLift > 0f)
        {
            Rotate(lThigh, 0f, -kneeLift * liftL, 0f, w, fwd, right, up);
            Rotate(rThigh, 0f, -kneeLift * liftR, 0f, w, fwd, right, up);
            Rotate(lShin, 0f, kneeLift * 1.4f * liftL, 0f, w, fwd, right, up);
            Rotate(rShin, 0f, kneeLift * 1.4f * liftR, 0f, w, fwd, right, up);
        }
    }

    private const float MinElbowAngle = 70f; // 팔꿈치 안쪽 각도 하한 (180=쭉 폄) — 팔뚝이 위팔에 포개지지 않게

    // 팔뚝(elbow→wrist)을 toward 쪽으로 angle만큼 접음 — 회전축은 팔뚝 방향과 toward 방향의 수직(팔꿈치 경첩)
    private static void Bend(Transform shoulder, Transform elbow, Transform wrist, Vector3 toward, float angle)
    {
        if (shoulder == null || elbow == null || wrist == null || angle <= 0f)
        {
            return;
        }

        Vector3 fore = wrist.position - elbow.position;
        Vector3 to = toward - elbow.position;
        Vector3 axis = Vector3.Cross(fore, to);
        if (axis.sqrMagnitude < 1e-6f)
        {
            return;
        }

        // 목표를 지나치지 않게, 그리고 팔꿈치가 MinElbowAngle보다 접히지 않게(머리가 위팔 뒤쪽이면 팔뚝이 위팔로 포개졌음)
        angle = Mathf.Min(angle, Vector3.Angle(fore, to));
        Quaternion q = Quaternion.AngleAxis(angle, axis.normalized);
        Vector3 back = shoulder.position - elbow.position;
        float after = Vector3.Angle(back, q * fore);
        if (after < MinElbowAngle)
        {
            float now = Vector3.Angle(back, fore);
            angle = Mathf.Max(0f, angle - (MinElbowAngle - after)) * (now > MinElbowAngle ? 1f : 0f);
            q = Quaternion.AngleAxis(angle, axis.normalized);
        }

        elbow.rotation = q * elbow.rotation;
    }

    private static void Rotate(Transform bone, float roll, float pitch, float yaw, float w, Vector3 fwd, Vector3 right, Vector3 up)
    {
        if (bone == null)
        {
            return;
        }

        bone.rotation =
            Quaternion.AngleAxis(yaw * w, up) *
            Quaternion.AngleAxis(roll * w, fwd) *
            Quaternion.AngleAxis(pitch * w, right) * bone.rotation;
    }
}
