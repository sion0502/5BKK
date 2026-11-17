using System.Collections;
using UnityEngine;

/// <summary>
/// 간호사 귀신 점프스케어 — 기본형 "달려와 덮침".
/// 카메라가 간호사 쪽으로 돈 뒤(EnemyJumpscare 공통 0.14초), 몇 m 앞에서 달리기 동작을 빠르게 돌리며
/// 가속해 화면으로 돌진 → 얼굴이 눈앞에 닿는 순간 비명·흔들림.
/// </summary>
public class NurseJumpscare : CreatureJumpscare
{
    [Header("돌진")]
    [Tooltip("돌진 시작 거리 (벽이 가까우면 줄어듦)")]
    [SerializeField] private float startDistance = 5.5f;
    [SerializeField] private float dashSeconds = 0.36f;
    [Tooltip("달리기 클립 재생 배속 — 평소보다 훨씬 빠르게 (초당 사이클 수)")]
    [SerializeField] private float runCyclesPerSecond = 3.2f;
    [SerializeField] private string runState = "Chase";
    [Header("덮침")]
    [Tooltip("멈췄을 때 얼굴까지 거리 — 얼굴을 머리카락이 덮은 모델이라 너무 가까우면 머리카락만 보임")]
    [SerializeField] private float endDistance = 0.9f;
    [Tooltip("닿는 순간 고개를 들어 얼굴을 들이밂 (−면 듦)")]
    [SerializeField] private float impactHeadPitch = -36f;
    [SerializeField] private float impactSeconds = 0.45f;
    [SerializeField] private float impactShake = 0.9f;
    [SerializeField] private float impactFov = 48f;
    [Tooltip("닿는 순간 자세 — 달리기는 상체를 크게 비트는 동작이라 그대로 멈추면 옆모습이 됨")]
    [SerializeField] private string impactState = "Attack";
    [SerializeField, Range(0f, 1f)] private float impactPose = 0.1f; // 0.35 근처는 팔을 휘두르며 고개가 옆으로 돌아감

    public override IEnumerator Play(JumpscareStage s)
    {
        float fov = s.OriginalFov;
        float from = s.ClampDistance(startDistance);
        float to = Mathf.Max(s.FaceDistance, endDistance);

        s.Pose(runState, 0f);
        s.PlaceGrounded(from);
        s.PlayScare(scareSound);

        yield return Run(dashSeconds, (t, time) =>
        {
            s.Pose(runState, Mathf.Repeat(time * runCyclesPerSecond, 1f));

            // 가속(제곱) — 멀리선 달려오는 게 보이고 마지막에 확 붙음
            float k = t * t;
            float distance = Mathf.Lerp(from, to, k);
            // 멀리선 바닥에 서서 달리고, 가까워질수록 얼굴을 눈높이로 (키가 커서 그대로면 턱 밑만 보임)
            s.PlaceGrounded(distance);
            Vector3 grounded = s.Monster.position;
            s.PlaceFace(distance);
            s.Monster.position = Vector3.Lerp(grounded, s.Monster.position, Smooth((t - 0.45f) / 0.55f));

            s.Look(s.AnchorPoint, 1f, JumpscareStage.Shake(time, 0.15f * k), Mathf.Lerp(fov, fov - 6f, k));
        });

        // 닿는 순간: 팔 뻗은 공격 자세로, 짧고 강한 흔들림이 빠르게 잦아듦
        s.Pose(impactState, impactPose);
        Transform head = s.Bone(HumanBodyBones.Head);
        Transform neck = s.Bone(HumanBodyBones.Neck);
        s.SetBoneTilt(neck, 0f, impactHeadPitch * 0.4f);
        s.SetBoneTilt(head, 0f, impactHeadPitch * 0.6f);
        s.PlaceFace(to);
        Vector3 lookAt = s.AnchorPoint + Vector3.up * 0.15f; // 머리 뼈는 목 쪽이라 살짝 위가 얼굴 중앙
        yield return Run(impactSeconds, (t, time) =>
        {
            s.PlaceFace(to);
            float shake = impactShake * (1f - t) * (1f - t);
            s.Look(lookAt, 1f, JumpscareStage.Shake(time, shake), Mathf.Lerp(fov - 6f, impactFov, EaseOut(t / 0.15f)));
        });
    }
}
