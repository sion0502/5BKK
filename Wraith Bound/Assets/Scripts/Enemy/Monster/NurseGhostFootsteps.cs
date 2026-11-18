using UnityEngine;

/// <summary>
/// NurseGhost 맨발 발소리 (사운드 전용). 순찰·추격·연출 어디서든, 발이 바닥에 닿는 순간에만 소리를 냄.
///  · Animator의 양 발 뼈(휴머노이드 LeftFoot/RightFoot)의 높이를 매 프레임 읽어서,
///    발이 들렸다가 바닥 높이로 내려와 닿는 순간 한 번씩만 재생 (왼발·오른발 각각, 한 발에 한 번).
///  · 소리는 각 발 뼈에서 직접 나옴(3D) — 간호사 자신(발)에게서 나는 소리.
///  · 양발이 똑같은 소리가 되지 않게 왼발/오른발 음높이를 다르게 하고, 매번 살짝 무작위로 흔듦.
///  · 제자리(멈춤·포즈 연출)에서는 소리 안 남 — 실제로 이동 중일 때만.
///  · 거리 감쇠·벽 너머(Low Pass)·잔향·3D 방향성은 <see cref="SpatialSoundFx"/>가 처리.
/// 간호사 프리팹 루트(Animator가 있는 오브젝트)에 붙입니다.
/// </summary>
[DefaultExecutionOrder(1000)] // 애니메이션·발 붙이기(LateUpdate)가 끝난 뒤의 발 위치를 읽도록
[DisallowMultipleComponent]
public class NurseGhostFootsteps : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Header("음높이 (양발이 같은 소리가 되지 않게)")]
    [SerializeField] private float leftPitch = 0.93f;
    [SerializeField] private float rightPitch = 1.07f;
    [Tooltip("매 걸음마다 ± 이만큼 무작위로 흔듦")]
    [SerializeField] private float pitchJitter = 0.035f;
    [Header("발 닿음 판정")]
    [Tooltip("발이 이만큼(m) 이상 들려야 '든 발'로 침 — 질질 끄는 절뚝 발도 잡히게 작게")]
    [SerializeField] private float liftHeight = 0.06f;
    [Tooltip("들린 발이 가장 낮은 높이 + 이만큼 안으로 내려오면 닿은 것")]
    [SerializeField] private float plantBand = 0.03f;
    [Tooltip("발이 이 시간(초) 이상 떠 있었어야 '한 걸음'으로 침 — 뼈가 떨려서 생기는 '바바박'을 막음")]
    [SerializeField] private float minAirSeconds = 0.12f;
    [Tooltip("같은 발이 다시 소리 나기까지 최소 간격(초) — 한 발에 한 번")]
    [SerializeField] private float sameFootInterval = 0.45f;
    [Tooltip("어느 발이든 소리 사이 최소 간격(초)")]
    [SerializeField] private float anyFootInterval = 0.16f;
    [Tooltip("발높이를 부드럽게 읽는 정도(클수록 빠르게 따라감)")]
    [SerializeField] private float heightSmoothing = 25f;
    [Tooltip("이 속도(m/초) 미만이면 이동 중이 아닌 것으로 보고 소리 안 냄")]
    [SerializeField] private float minMoveSpeed = 0.25f;

    private class Foot
    {
        public Transform bone;
        public AudioSource source;
        public float basePitch;
        public float height;
        public float lowest;
        public bool lifted;
        public float airTime;
        public float lastStep = -10f;
    }

    private Foot left;
    private Foot right;
    private Vector3 lastPosition;
    private Foot lastSteppedFoot;
    private float lastAnyStep = -10f;

    private void Awake()
    {
        // 프리팹이 겹쳐서(상속) 같은 컴포넌트가 둘 붙었으면 하나만 남김 — 발소리 이중 재생 방지
        NurseGhostFootsteps[] all = GetComponents<NurseGhostFootsteps>();
        if (all.Length > 1 && all[0] != this)
        {
            Destroy(this);
            return;
        }

        Animator animator = GetComponent<Animator>();
        Transform l = null, r = null;
        if (animator != null && animator.isHuman)
        {
            l = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            r = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        l = l != null ? l : FindBone("left");
        r = r != null ? r : FindBone("right");
        left = Make(l, leftPitch, "Left");
        right = Make(r, rightPitch, "Right");
        lastPosition = transform.position;
    }

    private Transform FindBone(string side)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains(side) && n.Contains("foot") && !n.Contains("toe"))
            {
                return t;
            }
        }

        return null;
    }

    private Foot Make(Transform bone, float pitch, string label)
    {
        if (bone == null)
        {
            return null;
        }

        GameObject go = new GameObject("BarefootStep_" + label);
        go.transform.SetParent(bone, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = volume;
        source.clip = null;
        go.AddComponent<SpatialSoundFx>(); // 거리 감쇠 / 벽 너머 Low Pass / 잔향 / 3D 방향성

        return new Foot
        {
            bone = bone,
            source = source,
            basePitch = pitch,
            height = bone.position.y - transform.position.y,
            lowest = bone.position.y - transform.position.y
        };
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float speed = delta.magnitude / dt;
        lastPosition = transform.position;

        bool moving = speed >= minMoveSpeed;
        Tick(left, dt, moving);
        Tick(right, dt, moving);
    }

    private void Tick(Foot f, float dt, bool moving)
    {
        if (f == null || f.bone == null)
        {
            return;
        }

        // 뼈 떨림에 속지 않게 부드럽게 읽음
        float raw = f.bone.position.y - transform.position.y;
        float h = Mathf.Lerp(f.height, raw, 1f - Mathf.Exp(-heightSmoothing * dt));
        float velocity = (h - f.height) / dt;
        f.height = h;

        // 가장 낮은 높이(=디딘 발의 높이)는 즉시 따라 내려가고, 천천히만 올라감
        f.lowest = Mathf.Min(h, f.lowest + 0.08f * dt);

        if (!moving)
        {
            f.lifted = false;
            f.airTime = 0f;
            return;
        }

        if (h > f.lowest + liftHeight)
        {
            f.lifted = true;
            f.airTime += dt;
            return;
        }

        if (!f.lifted || h > f.lowest + plantBand)
        {
            return; // 아직 안 내려옴 (들렸다 내려오는 중이거나 덜 내려옴)
        }

        // 내려와 닿음 — 충분히 떠 있다가 내려온 '진짜 한 걸음'만
        bool realStep = f.airTime >= minAirSeconds && velocity <= 0.1f;
        bool sameFootTooSoon = Time.time - f.lastStep < sameFootInterval;
        bool anyTooSoon = Time.time - lastAnyStep < anyFootInterval;
        bool sameFootTwice = lastSteppedFoot == f && Time.time - lastAnyStep < 1.0f; // 한 발 다음엔 반대 발

        f.lifted = false;
        f.airTime = 0f;
        if (realStep && !sameFootTooSoon && !anyTooSoon && !sameFootTwice)
        {
            f.lastStep = Time.time;
            lastAnyStep = Time.time;
            lastSteppedFoot = f;
            Step(f);
        }
    }

    private void Step(Foot f)
    {
        if (clip == null || f.source == null)
        {
            return;
        }

        f.source.pitch = f.basePitch + Random.Range(-pitchJitter, pitchJitter);
        f.source.PlayOneShot(clip, 1f);
    }
}
