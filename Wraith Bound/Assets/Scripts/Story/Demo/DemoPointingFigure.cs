using UnityEngine;

/// <summary>
/// 휴머노이드 모델을 '한쪽 팔로 어딘가를 가리킨 채 서 있는' 자세로 고정 (Animator 컨트롤러 없이 뼈를 직접 돌림).
/// 고개는 플레이어 쪽으로 천천히 따라 돌아감(최대 lookLimit도). 시연 맵 연출 ② 병동의 그림자 사람.
/// </summary>
[DefaultExecutionOrder(100)]
public class DemoPointingFigure : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("가리키는 곳 (월드)")]
    [SerializeField] private Transform pointTarget;
    [SerializeField] private bool useRightArm = true;
    [Tooltip("늘어뜨린 팔이 몸에서 벌어지는 정도")]
    [SerializeField] private float restArmSpread = 0.12f;
    [SerializeField] private float headTilt = 14f;
    [SerializeField] private float lookLimit = 80f;
    [SerializeField] private float lookSpeed = 40f;

    private Transform head;
    private Quaternion headBase;
    private Quaternion headOffset = Quaternion.identity;
    private Transform player;
    private bool posed;

    private void Start()
    {
        Pose();
    }

    public void Pose()
    {
        if (posed || animator == null || !animator.isHuman)
        {
            return;
        }

        posed = true;
        Vector3 down = Vector3.down;
        Vector3 side = transform.right;

        // 가리키는 팔
        HumanBodyBones upper = useRightArm ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
        HumanBodyBones lower = useRightArm ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
        HumanBodyBones hand = useRightArm ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
        Transform shoulder = animator.GetBoneTransform(upper);
        Vector3 dir = pointTarget != null && shoulder != null ? (pointTarget.position - shoulder.position).normalized : transform.forward;
        Aim(upper, lower, dir);
        Aim(lower, hand, dir);

        // 반대 팔은 축 늘어뜨림
        HumanBodyBones oUpper = useRightArm ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
        HumanBodyBones oLower = useRightArm ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
        HumanBodyBones oHand = useRightArm ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        Vector3 rest = (down + (useRightArm ? -side : side) * restArmSpread).normalized;
        Aim(oUpper, oLower, rest);
        Aim(oLower, oHand, rest);

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null)
        {
            head.rotation = Quaternion.AngleAxis(headTilt, transform.forward) * head.rotation;
            headBase = head.localRotation;
        }

        animator.enabled = false; // 고정 자세가 덮어쓰이지 않게
    }

    private void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 dir)
    {
        Transform b = animator.GetBoneTransform(bone);
        Transform c = animator.GetBoneTransform(child);
        if (b == null || c == null)
        {
            return;
        }

        Vector3 cur = c.position - b.position;
        b.rotation = Quaternion.FromToRotation(cur, dir) * b.rotation;
    }

    private void LateUpdate()
    {
        if (head == null)
        {
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            player = p != null ? p.transform : null;
            if (player == null)
            {
                return;
            }
        }

        // 몸 정면 기준 수평 각도만큼 고개를 돌림 (제한 있음)
        Vector3 to = player.position - head.position;
        to.y = 0f;
        float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, to, Vector3.up), -lookLimit, lookLimit);
        Quaternion want = Quaternion.AngleAxis(yaw, Vector3.up);
        headOffset = Quaternion.RotateTowards(headOffset, want, lookSpeed * Time.deltaTime);
        head.localRotation = headBase;
        head.rotation = headOffset * head.rotation;
    }
}
