using System.Collections;
using UnityEngine;

/// <summary>
/// 탈춤귀신 점프스케어 — "발광하며 돌진".
/// 카메라가 돈 뒤(EnemyJumpscare 공통) 몇 m 앞에서 발작 상태로 관절을 미친 듯이 꺾으며(CreatureJointSnap 강제 발작,
/// 겹친 몸까지) 가속해 화면으로 돌진 → 코앞에서 모든 움직임이 뚝 멈추고 고개가 꺾인 채 웃는 탈·붉은 눈이 화면을 채움 + 비명
/// → 고개만 잘게 경련. 몸 늘어남·안광·겹친 몸은 각 컴포넌트가 점프스케어 중에도 유지.
/// 기준점은 두 눈 사이(CreatureEyeGlow) — 키가 커서(추격 중 약 3.4m) 멀리선 바닥에 세우고 가까워질수록 얼굴을 눈높이로.
/// </summary>
public class MaskGhostJumpscare : CreatureJumpscare
{
    [Header("발광 돌진")]
    [SerializeField] private float startDistance = 6f;
    [SerializeField] private float dashSeconds = 0.5f;
    [Tooltip("달리기 클립 재생 배속 (초당 사이클)")]
    [SerializeField] private float runCyclesPerSecond = 3.6f;
    [SerializeField] private string runState = "Chase";
    [SerializeField] private float dashConvulsion = 1.2f;
    [Header("뚝 멈춤")]
    [Tooltip("탈(두 눈 사이)까지 거리")]
    [SerializeField] private float endDistance = 0.55f;
    [Tooltip("멈춘 순간 고개 꺾임 (좌우, 숙임)")]
    [SerializeField] private Vector2 stopHeadTilt = new Vector2(58f, -10f);
    [SerializeField] private float stillSeconds = 0.14f;
    [Header("경련")]
    [SerializeField] private float twitchSeconds = 0.6f;
    [SerializeField] private float twitchAngle = 9f;
    [SerializeField] private float impactShake = 1.1f;
    [SerializeField] private float impactFov = 46f;

    public override IEnumerator Play(JumpscareStage s)
    {
        float fov = s.OriginalFov;
        CreatureEyeGlow eyes = s.Monster.GetComponent<CreatureEyeGlow>();
        if (eyes != null)
        {
            s.FaceAnchor = () => eyes.TryGetEyeMidpoint(out Vector3 p) ? p : (s.Face != null ? s.Face.position : s.Monster.position + Vector3.up * 3f);
        }

        // 원본·겹친 몸의 관절 꺾기를 실제 시간 발작으로
        CreatureJointSnap[] snaps = s.Monster.GetComponentsInChildren<CreatureJointSnap>(true);
        foreach (CreatureJointSnap snap in snaps)
        {
            snap.SetForced(true, dashConvulsion);
        }

        Transform head = s.Bone(HumanBodyBones.Head);
        CreatureDoubleBody doubleBody = s.Monster.GetComponent<CreatureDoubleBody>();
        float from = s.ClampDistance(startDistance);
        float to = Mathf.Max(s.FaceDistance, endDistance);
        s.Pose(runState, 0f);
        s.PlaceGrounded(from);

        try
        {
            // ① 발광 돌진 — 가속, 멀리선 바닥에 서서·가까워질수록 얼굴 눈높이로
            yield return Run(dashSeconds, (t, time) =>
            {
                s.Pose(runState, Mathf.Repeat(time * runCyclesPerSecond, 1f));
                float k = Mathf.Pow(t, 1.7f);
                float distance = Mathf.Lerp(from, to, k);
                s.PlaceGrounded(distance);
                Vector3 grounded = s.Monster.position;
                s.PlaceFace(distance);
                s.Monster.position = Vector3.Lerp(grounded, s.Monster.position, Smooth((t - 0.4f) / 0.6f));
                s.Look(s.AnchorPoint, 1f, JumpscareStage.Shake(time, 0.2f * k), Mathf.Lerp(fov, fov - 6f, k));
            });

            // ② 뚝 멈춤 — 몸은 꺾인 자세 그대로 정지(고개·목만 풀어 탈이 정면), 겹친 몸은 하나로 합쳐지듯 사라짐, 비명
            foreach (CreatureJointSnap snap in snaps)
            {
                snap.SetForced(true, 0f);
                snap.ResetBones(HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.UpperChest);
            }
            if (doubleBody != null)
            {
                doubleBody.SetSuppressed(true);
            }
            // 몸통이 꺾인 채 굳어 있어 기울임 값만으론 방향을 못 맞춤 → 모든 꺾임이 적용된 뒤
            // 탈의 실제 정면(머리→headfront)이 카메라를 보게 돌리고, 그 위에 꺾임·경련을 얹음
            Transform headFront = FindChild(s.Monster, "headfront");
            float roll = stopHeadTilt.x, pitch = stopHeadTilt.y;
            s.LateHook = () => AimFace(s, head, headFront, roll, pitch);
            s.PlaceFace(to);
            s.PlayScare(scareSound);
            Vector3 lookAt = s.AnchorPoint;
            yield return Run(stillSeconds, (t, time) =>
            {
                s.PlaceFace(to);
                s.Look(lookAt, 1f, JumpscareStage.Shake(time, impactShake * (1f - t * 0.5f)), Mathf.Lerp(fov - 6f, impactFov, EaseOut(t / 0.5f)));
            });

            // ③ 고개만 잘게 경련
            yield return Run(twitchSeconds, (t, time) =>
            {
                float n1 = Mathf.PerlinNoise(time * 31f, 0.2f) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(0.7f, time * 27f) * 2f - 1f;
                roll = stopHeadTilt.x + n1 * twitchAngle;
                pitch = stopHeadTilt.y + n2 * twitchAngle * 0.6f;
                s.PlaceFace(to);
                float shake = impactShake * 0.5f * (1f - t) * (1f - t);
                s.Look(lookAt, 1f, JumpscareStage.Shake(time, shake), impactFov);
            });
        }
        finally
        {
            foreach (CreatureJointSnap snap in snaps)
            {
                if (snap != null)
                {
                    snap.SetForced(false);
                }
            }
            if (doubleBody != null)
            {
                doubleBody.SetSuppressed(false);
            }
        }
    }

    /// <summary>탈 정면이 카메라를 똑바로 보게 머리 뼈를 돌린 뒤 꺾임(roll)·숙임(pitch)을 더함. LateApply 뒤라 누적 없음.</summary>
    private static void AimFace(JumpscareStage s, Transform head, Transform headFront, float roll, float pitch)
    {
        if (head == null || s.View == null)
        {
            return;
        }

        Vector3 face = headFront != null ? headFront.position - head.position : s.Monster.forward;
        Vector3 toCam = s.View.transform.position - head.position;
        if (face.sqrMagnitude < 1e-6f || toCam.sqrMagnitude < 1e-6f)
        {
            return;
        }

        toCam.Normalize();
        Vector3 camRight = s.View.transform.right;
        head.rotation =
            Quaternion.AngleAxis(roll, toCam) *
            Quaternion.AngleAxis(pitch, camRight) *
            Quaternion.FromToRotation(face.normalized, toCam) * head.rotation;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != childName)
            {
                continue;
            }
            bool inTwin = false;
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                if (p.name.EndsWith("_Double"))
                {
                    inTwin = true;
                    break;
                }
            }
            if (!inTwin)
            {
                return t;
            }
        }
        return null;
    }
}
