using System.Collections;
using UnityEngine;

/// <summary>
/// Xenosaurid 점프스케어 — "아가리 → 덮쳐 쓰러뜨림".
/// ① 2.4m 앞에서 몸을 낮췄다 일어서며 턱을 크게 벌리고 혀를 내밂(카메라는 올려다봄)
/// ② 순식간에 덮침 — 입이 눈앞, 플레이어는 뒤로 넘어지며(시점이 내려가고 기울어짐) 크리처가 위에서 내려다봄.
/// 턱은 XenosauridController와 같은 로컬 Z축으로 엶. 이 컴포넌트가 있으면 컨트롤러의 기존 사망 얼굴 연출은 쉼.
/// </summary>
public class XenosauridJumpscare : CreatureJumpscare
{
    [Header("① 아가리")]
    [SerializeField] private float standDistance = 2.4f;
    [SerializeField] private float rearSeconds = 0.85f;
    [SerializeField] private Vector2 tauntPoseRange = new Vector2(0.12f, 0.3f);
    [SerializeField] private float jawOpen = 72f;
    [SerializeField] private float tongueStretch = 1.7f;
    [SerializeField] private Vector3 jawAxis = new Vector3(0f, 0f, 1f);
    [Header("② 덮침·쓰러짐")]
    [SerializeField] private float lungeSeconds = 0.13f;
    [Tooltip("덮친 뒤 입까지 거리 (주둥이가 길어 머리 뼈 대신 입 기준)")]
    [SerializeField] private float lungeDistance = 0.75f;
    [SerializeField] private float pinnedSeconds = 0.75f;
    [SerializeField, Range(0f, 1f)] private float attackPose = 0.3f;
    [SerializeField] private float fallDrop = 0.75f;
    [SerializeField] private float fallBack = 0.35f;
    [SerializeField] private float fallRoll = 18f;
    [SerializeField] private float fallPitchUp = -28f;
    [SerializeField] private float lungeFov = 50f;

    public override IEnumerator Play(JumpscareStage s)
    {
        Transform jaw = s.Bone(HumanBodyBones.Jaw, "Xenosaurid_ Jaw");
        Transform tongue1 = s.Bone(HumanBodyBones.LastBone, "Xenosaurid_ Tongue1");
        Transform tongue2 = s.Bone(HumanBodyBones.LastBone, "Xenosaurid_ Tongue2");
        Transform head = s.Face;
        float fov = s.OriginalFov;
        // 루트 정면과 주둥이 방향이 달라서 — 주둥이(머리→혀)가 카메라를 보게, 기준점은 입
        if (head != null && tongue1 != null)
        {
            s.FaceForward = () => tongue1.position - head.position;
            s.FaceAnchor = () => jaw != null ? Vector3.Lerp(jaw.position, tongue1.position, 0.55f) : tongue1.position;
        }
        Vector3 axis = jawAxis.sqrMagnitude > 0.0001f ? jawAxis.normalized : Vector3.forward;

        // ① 몸 낮췄다가 일어서며 아가리
        float stand = s.ClampDistance(standDistance);
        yield return Run(rearSeconds, (t, time) =>
        {
            s.Pose("Taunt", Mathf.Lerp(tauntPoseRange.x, tauntPoseRange.y, Smooth(t)));
            s.PlaceGrounded(stand);
            float open = Smooth((t - 0.25f) / 0.6f);
            s.SetBoneLocal(jaw, Quaternion.AngleAxis(jawOpen * open, axis));
            float stretch = Mathf.Lerp(1f, tongueStretch, Smooth((t - 0.45f) / 0.5f));
            s.SetBoneScale(tongue1, new Vector3(stretch, 1f, 1f));
            s.SetBoneScale(tongue2, new Vector3(stretch, 1f, 1f));
            s.Look(s.AnchorPoint, Smooth(t * 1.8f), JumpscareStage.Shake(time, 0.06f * open), Mathf.Lerp(fov, fov - 5f, t));
        });

        // ② 덮침
        s.Pose("Attack", attackPose);
        s.PlayScare(scareSound);
        float near = Mathf.Max(s.FaceDistance, lungeDistance);
        float startDistance = Vector3.Distance(s.AnchorPoint, s.Eye);
        yield return Run(lungeSeconds, (t, time) =>
        {
            s.PlaceFace(Mathf.Lerp(startDistance, near, EaseOut(t)));
            s.Look(s.CurrentEye + s.Forward, 0f, JumpscareStage.Shake(time, 0.55f), Mathf.Lerp(fov - 5f, lungeFov, t));
        });

        // 쓰러짐: 시점이 뒤로·아래로 넘어가고, 크리처는 위에서 따라 내려옴
        yield return Run(pinnedSeconds, (t, time) =>
        {
            float fall = EaseOut(t / 0.45f);
            s.SetCameraOffset(-s.FlatForward * fallBack * fall + Vector3.down * fallDrop * fall);
            s.PlaceFace(near, 0f, true); // 넘어지며 올려다보는 시선 정면에 입
            float shake = Mathf.Lerp(0.45f, 0f, Smooth(t / 0.6f)); // 떨림은 넘어지는 충격만 짧게
            Vector3 tilt = new Vector3(fallPitchUp * fall, 0f, fallRoll * fall);
            s.Look(s.CurrentEye + s.Forward, 0f, tilt + JumpscareStage.Shake(time, shake), lungeFov);
        });
    }
}
