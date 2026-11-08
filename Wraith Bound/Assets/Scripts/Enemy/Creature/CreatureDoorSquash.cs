using System;
using UnityEngine;

/// <summary>
/// 문보다 큰 크리처가 문(낮은 천장)을 지날 때 몸을 접어 비집고 지나감 — 크기는 그대로 유지.
/// · 감지: 크리처 위치 뒤쪽~진행 방향 앞쪽 여러 지점에서 위로 레이 → 가장 낮은 천장(문틀 위 벽) 높이.
///   본인·다른 적·플레이어·트리거는 무시. 문틀 위 벽에 충돌체가 있어야 함(Chapter1·HH 문 모듈 모두 있음).
/// · 접기: 허리를 숙이고(고개는 들어 앞을 봄) 무릎을 굽힘 — 다리가 접히면 CreatureFootGrounding이 골반을 내림.
///   접어도 모자라면 몸 전체를 골반 기준으로 살짝 줄임(minScale까지).
/// · CreatureChaseStretch(탈춤귀신)의 늘어남은 접는 만큼 거둠.
/// 각도는 크리처 기준 축(pitch: + = 앞으로 숙임). Animator가 꺼져 있으면(점프스케어) 쉼.
/// 실행 순서: 늘어남(−30)·관절 꺾기(−20) 뒤, 발 붙이기(0) 앞.
/// </summary>
[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureDoorSquash : MonoBehaviour
{
    [Serializable]
    public struct FoldBone
    {
        [Tooltip("휴머노이드 뼈 (LastBone이면 boneName으로 찾음)")]
        public HumanBodyBones human;
        public string boneName;
        [Tooltip("다 접었을 때 앞뒤 회전(°) — 상체 +는 숙임, 허벅지 −는 무릎이 앞으로, 정강이 +는 굽힘")]
        public float pitch;
    }

    [SerializeField] private FoldBone[] foldBones =
    {
        new FoldBone { human = HumanBodyBones.Spine, pitch = 22f },
        new FoldBone { human = HumanBodyBones.Chest, pitch = 25f },
        new FoldBone { human = HumanBodyBones.UpperChest, pitch = 25f },
        new FoldBone { human = HumanBodyBones.Neck, pitch = -25f },
        new FoldBone { human = HumanBodyBones.Head, pitch = -30f },
        new FoldBone { human = HumanBodyBones.LeftUpperLeg, pitch = -45f },
        new FoldBone { human = HumanBodyBones.RightUpperLeg, pitch = -45f },
        new FoldBone { human = HumanBodyBones.LeftLowerLeg, pitch = 85f },
        new FoldBone { human = HumanBodyBones.RightLowerLeg, pitch = 85f },
        new FoldBone { human = HumanBodyBones.LeftFoot, pitch = -40f },
        new FoldBone { human = HumanBodyBones.RightFoot, pitch = -40f },
    };

    [Tooltip("키를 잴 뼈 (머리·목·척추 — 팔은 제외)")]
    [SerializeField] private string[] topBoneNames = Array.Empty<string>();
    [Tooltip("발 뼈 — 다리를 굽히거나 몸을 줄여 발이 뜬 만큼 골반을 바로 내려 바닥에 붙임 (비우면 휴머노이드 발·발끝)")]
    [SerializeField] private string[] footBoneNames = Array.Empty<string>();
    [Tooltip("머리 뼈 위로 메쉬가 더 올라온 정도(m)")]
    [SerializeField] private float headExtra = 0.25f;
    [Tooltip("천장(문틀)과 머리 사이 여유(m)")]
    [SerializeField] private float margin = 0.12f;

    [Header("감지")]
    [Tooltip("크리처 기준 앞뒤로 천장을 살피는 범위(m) — x=뒤(몸이 아직 문 아래), y=앞")]
    [SerializeField] private Vector2 scanRange = new Vector2(-0.8f, 1.8f);
    [Tooltip("레이 간격 — 문틀 위 벽 두께(약 0.22m)보다 충분히 촘촘해야 함. 0.25면 걸을 때마다 문틀을 맞혔다 놓쳐 앉았다 일어났다 했음")]
    [SerializeField] private float scanStep = 0.1f;
    [Tooltip("최근 이 시간 동안 본 가장 낮은 천장을 기억 (한 번 놓쳐도 바로 펴지 않게)")]
    [SerializeField] private float ceilingMemory = 0.6f;
    [SerializeField] private float scanInterval = 0.08f;
    [SerializeField] private LayerMask ceilingMask = ~0;
    [Tooltip("이보다 낮게 맞은 건 천장이 아니라 가구(휠체어 좌석·침대 등)로 보고 무시 (바닥 기준 m)")]
    [SerializeField] private float minCeilingHeight = 1.6f;

    [Header("움직임")]
    [SerializeField] private float foldSpeed = 6f;
    [SerializeField] private float unfoldSpeed = 3f;
    [Tooltip("펴도 된다는 판단이 이 시간 동안 계속돼야 펴기 시작 — 문 아래에서 일어서려다 다시 앉는 엉거주춤 방지")]
    [SerializeField] private float unfoldDelay = 0.45f;
    [Tooltip("접어도 모자랄 때 몸을 줄이는 한계 (골반 기준 균등)")]
    [SerializeField, Range(0.5f, 1f)] private float minScale = 0.75f;

    private Animator animator;
    private Transform owner;
    private Transform hips;
    private Transform[] foldTransforms;
    private Transform[] topTransforms;
    private Vector3 hipsRestScale;
    private CreatureChaseStretch stretch;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private float allowedTop = float.PositiveInfinity; // 바닥 기준 머리가 닿아도 되는 높이
    private float nextScan;
    private readonly float[] recentCeiling = new float[16];
    private readonly float[] recentTime = new float[16];
    private int recentIndex;
    private readonly float[] recentTarget = new float[256];
    private readonly float[] recentTargetTime = new float[256];
    private int recentTargetIndex;
    private float amount;       // 접은 정도 0~1
    private float scale = 1f;   // 추가 축소
    private float foldReach = -1f; // 다 접었을 때 줄어드는 키(m) — 실행 중 측정해 갱신
    // 렌더 직전(발 붙이기가 골반까지 내린 뒤) 실제 결과 — 무릎을 굽히면 골반은 발 붙이기가 나중에 내리므로
    // LateUpdate 안에서 잰 높이로는 접기 효과를 과소평가해 너무 많이 접었음
    private float lastUnfolded;
    private float lastFinal;
    private float lastAmount;
    private float lastScale = 1f;
    private bool lastValid;

    /// <summary>지금 접은 정도 (0=펴짐, 1=다 접음).</summary>
    public float Amount => amount;

    public void Configure(FoldBone[] bones, string[] topNames, float extra, string[] feetNames = null)
    {
        foldBones = bones;
        topBoneNames = topNames;
        headExtra = extra;
        footBoneNames = feetNames ?? Array.Empty<string>();
    }

    private Transform[] footTransforms;

    private float LowestFoot()
    {
        float low = float.MaxValue;
        if (footTransforms != null)
        {
            foreach (Transform t in footTransforms)
            {
                if (t != null && t.position.y < low)
                {
                    low = t.position.y;
                }
            }
        }
        return low;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        EnemyBase enemy = GetComponentInParent<EnemyBase>();
        owner = enemy != null ? enemy.transform : transform;
        stretch = GetComponent<CreatureChaseStretch>();

        hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : FindNamed("Pelvis");
        if (hips != null)
        {
            hipsRestScale = hips.localScale;
        }

        foldTransforms = new Transform[foldBones.Length];
        for (int i = 0; i < foldBones.Length; i++)
        {
            FoldBone b = foldBones[i];
            foldTransforms[i] = b.human != HumanBodyBones.LastBone && animator.isHuman
                ? animator.GetBoneTransform(b.human)
                : FindNamed(b.boneName);
        }

        if (footBoneNames != null && footBoneNames.Length > 0)
        {
            footTransforms = new Transform[footBoneNames.Length];
            for (int i = 0; i < footBoneNames.Length; i++)
            {
                footTransforms[i] = FindNamed(footBoneNames[i]);
            }
        }
        else if (animator.isHuman)
        {
            footTransforms = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot),
                animator.GetBoneTransform(HumanBodyBones.LeftToes), animator.GetBoneTransform(HumanBodyBones.RightToes),
            };
        }

        if (topBoneNames != null && topBoneNames.Length > 0)
        {
            topTransforms = new Transform[topBoneNames.Length];
            for (int i = 0; i < topBoneNames.Length; i++)
            {
                topTransforms[i] = FindNamed(topBoneNames[i]);
            }
        }
        else if (animator.isHuman)
        {
            topTransforms = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.Head),
                animator.GetBoneTransform(HumanBodyBones.Neck),
                animator.GetBoneTransform(HumanBodyBones.UpperChest),
                animator.GetBoneTransform(HumanBodyBones.Chest),
            };
        }
    }

    private Transform FindNamed(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
        {
            return null;
        }

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName || t.name.EndsWith(" " + boneName))
            {
                return t;
            }
        }
        return null;
    }

    private void OnEnable()
    {
        Application.onBeforeRender += RecordFinal;
    }

    private void RecordFinal()
    {
        if (!isActiveAndEnabled || hips == null || topTransforms == null || !animator.enabled)
        {
            return;
        }
        lastFinal = MeasureTop() - transform.position.y;
        lastAmount = amount;
        lastScale = scale;
        lastValid = true;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= RecordFinal;
        lastValid = false;
        amount = 0f;
        scale = 1f;
        if (hips != null)
        {
            hips.localScale = hipsRestScale;
        }
        if (stretch != null)
        {
            stretch.Suppression = 0f;
        }
    }

    private void LateUpdate()
    {
        if (!animator.enabled || hips == null || topTransforms == null)
        {
            return;
        }

        float ground = transform.position.y;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + scanInterval;
            recentCeiling[recentIndex] = ScanCeiling(ground);
            recentTime[recentIndex] = Time.time;
            recentIndex = (recentIndex + 1) % recentCeiling.Length;
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < recentCeiling.Length; i++)
            {
                if (Time.time - recentTime[i] <= ceilingMemory && recentTime[i] > 0f)
                {
                    lowest = Mathf.Min(lowest, recentCeiling[i]);
                }
            }

            allowedTop = lowest - margin;
        }

        // 펴진 상태(이번 프레임 애니메이션·꺾기 결과)의 키
        hips.localScale = hipsRestScale;
        float top = MeasureTop() - ground;
        float need = top - allowedTop;

        if (foldReach <= 0f)
        {
            foldReach = Mathf.Max(0.3f, top * 0.3f);
        }

        // 지난 프레임 최종 결과로 '다 접었을 때 줄어드는 키'를 실측 갱신 (축소분은 되돌려 계산)
        if (lastValid && lastAmount > 0.2f)
        {
            float finalUnscaled = lastFinal / Mathf.Max(0.5f, lastScale);
            foldReach = Mathf.Lerp(foldReach, Mathf.Max(0.2f, (lastUnfolded - finalUnscaled) / lastAmount), 0.25f);
        }
        lastUnfolded = top;

        float target = need > 0f ? Mathf.Clamp01(need / foldReach) : 0f;
        // 펴는 쪽은 최근 unfoldDelay초 동안의 최대 목표를 따름 — 걸음마다 머리 높이가 출렁여 목표가 들쭉날쭉해도
        // 문 아래에선 접은 채 유지되고, 다 지나간 뒤에야 한 번에 펴짐 (예전엔 문 아래에서 일어서려다 다시 앉았음)
        recentTarget[recentTargetIndex] = target;
        recentTargetTime[recentTargetIndex] = Time.time;
        recentTargetIndex = (recentTargetIndex + 1) % recentTarget.Length;
        for (int i = 0; i < recentTarget.Length; i++)
        {
            if (recentTargetTime[i] > 0f && Time.time - recentTargetTime[i] <= unfoldDelay)
            {
                target = Mathf.Max(target, recentTarget[i]);
            }
        }

        float speed = target > amount ? foldSpeed : unfoldSpeed;
        amount = Mathf.MoveTowards(amount, target, speed * Time.deltaTime);
        if (stretch != null)
        {
            stretch.Suppression = amount;
        }

        float footBefore = LowestFoot();
        if (amount > 0.001f)
        {
            ApplyFold(amount);

            // 다 접고도 실제 최종 높이(지난 프레임)가 모자랄 때만 골반 기준으로 몸 전체를 살짝 줄임 — 조금씩 맞춤
            // (덜 접은 상태에서 줄이면 공처럼 쪼그라듦)
            float hipsY = hips.position.y - ground;
            float wantScale = 1f;
            if (amount >= 0.99f && lastValid && lastFinal - hipsY > 0.05f)
            {
                wantScale = Mathf.Clamp(lastScale * (allowedTop - hipsY) / (lastFinal - hipsY), minScale, 1f);
            }
            scale = Mathf.MoveTowards(scale, wantScale, foldSpeed * 0.5f * Time.deltaTime);
        }
        else
        {
            scale = Mathf.MoveTowards(scale, 1f, unfoldSpeed * 0.5f * Time.deltaTime);
        }

        hips.localScale = hipsRestScale * scale;

        // 다리를 굽히거나 몸을 줄여 발이 뜬 만큼 골반을 바로 내림 (발 붙이기가 늦게 따라와 너무 접히던 것도 해결)
        if (footBefore < float.MaxValue && (amount > 0.001f || scale < 0.999f))
        {
            float lift = LowestFoot() - footBefore;
            if (lift > 0f)
            {
                hips.position += Vector3.down * lift;
            }
        }
    }

    private void ApplyFold(float a)
    {
        Vector3 right = transform.right;
        for (int i = 0; i < foldTransforms.Length; i++)
        {
            Transform bone = foldTransforms[i];
            if (bone != null)
            {
                bone.rotation = Quaternion.AngleAxis(foldBones[i].pitch * a, right) * bone.rotation;
            }
        }
    }

    private float MeasureTop()
    {
        float top = float.MinValue;
        for (int i = 0; i < topTransforms.Length; i++)
        {
            if (topTransforms[i] != null)
            {
                top = Mathf.Max(top, topTransforms[i].position.y);
            }
        }
        return top + headExtra;
    }

    /// <summary>앞뒤 범위에서 가장 낮은 천장 높이(바닥 기준). 없으면 무한대.</summary>
    private float ScanCeiling(float ground)
    {
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            fwd = Vector3.forward;
        }
        fwd.Normalize();

        float lowest = float.PositiveInfinity;
        float rayLength = 8f;
        for (float d = scanRange.x; d <= scanRange.y + 0.001f; d += scanStep)
        {
            Vector3 origin = new Vector3(transform.position.x, ground + 0.5f, transform.position.z) + fwd * d;
            int n = Physics.RaycastNonAlloc(origin, Vector3.up, hits, rayLength, ceilingMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(owner) || c.GetComponentInParent<EnemyBase>() != null
                    || c.GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                float h = hits[i].point.y - ground;
                if (h >= minCeilingHeight)
                {
                    lowest = Mathf.Min(lowest, h);
                }
            }
        }
        return lowest;
    }
}
