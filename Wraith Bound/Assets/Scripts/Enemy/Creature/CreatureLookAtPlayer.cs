using UnityEngine;

/// <summary>
/// 추격 중 시선(탈 얼굴)이 항상 플레이어 눈을 바라봄 (탈춤귀신, 2026-11-18 사용자 요청).
/// 탈춤·'뚝' 꺾임·문 접기가 모두 적용된 뒤 마지막에 목(neckShare)과 머리를 돌려,
/// 탈의 실제 정면(머리 뼈 → headfront 뼈, 없으면 머리 forward)이 플레이어 카메라를 정확히 겨누게 함. 갸웃(roll)은 춤 그대로.
/// 추격 중에만(페이드), 점프스케어 중엔 끔(점프스케어가 직접 얼굴 방향을 정함).
/// </summary>
[DefaultExecutionOrder(30)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureLookAtPlayer : MonoBehaviour
{
    [Tooltip("돌리는 각도 중 목이 맡는 비율 (나머지는 머리)")]
    [SerializeField, Range(0f, 1f)] private float neckShare = 0.35f;
    [Tooltip("한 번에 돌릴 수 있는 최대 각도 — 넘으면 이만큼만 (뒤에 있을 때 목이 완전히 뒤로 꺾이는 걸 막음)")]
    [SerializeField] private float maxAngle = 150f;
    [SerializeField] private float fadeSeconds = 0.25f;

    private Animator animator;
    private Transform head;
    private Transform neck;
    private Transform front;
    private Transform target;
    private float weight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            enabled = false;
            return;
        }

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (head != null)
        {
            foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "headfront")
                {
                    front = t;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (head == null || !animator.enabled)
        {
            return;
        }

        bool on = CreatureChaseState.IsChasing(animator) && !CreatureChaseState.IsJumpscare;
        weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (weight <= 0f || !ResolveTarget())
        {
            return;
        }

        if (neck != null && neckShare > 0f)
        {
            TurnToward(neck, neckShare * weight);
        }

        TurnToward(head, weight);
    }

    // 탈 정면을 목표 쪽으로 share만큼 돌림 (뼈 피벗 기준)
    private void TurnToward(Transform bone, float share)
    {
        Vector3 face = Face();
        Vector3 toTarget = target.position - head.position;
        if (face.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-4f)
        {
            return;
        }

        Quaternion full = Quaternion.FromToRotation(face, toTarget);
        full.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
        {
            angle -= 360f;
        }

        angle = Mathf.Clamp(angle, -maxAngle, maxAngle) * share;
        bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }

    private Vector3 Face()
    {
        return front != null ? (front.position - head.position).normalized : head.forward;
    }

    private bool ResolveTarget()
    {
        if (target != null && target.gameObject.activeInHierarchy)
        {
            return true;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            return false;
        }

        // 눈 카메라(MainCamera 태그) — 플레이어 아래엔 손 아이템용 카메라도 있음
        Camera eye = null;
        foreach (Camera cam in player.GetComponentsInChildren<Camera>(true))
        {
            if (cam.CompareTag("MainCamera") || eye == null)
            {
                eye = cam;
                if (cam.CompareTag("MainCamera"))
                {
                    break;
                }
            }
        }

        target = eye != null ? eye.transform : player.transform;
        return true;
    }
}
