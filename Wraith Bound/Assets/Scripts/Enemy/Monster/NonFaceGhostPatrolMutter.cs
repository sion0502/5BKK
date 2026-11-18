using UnityEngine;

/// <summary>
/// NonFaceGhost 순찰·수색 중얼거림 (사운드 전용). CreepyWhiper_C 목소리를 잘게 쪼개 만든 30초 루프를
/// 두 겹으로 겹쳐 재생하되, 두 겹의 음높이·세기·재생 위치가 계속 제각각 바뀌어서
/// 같은 소리가 똑같이 반복되지 않고 알 수 없는 말을 중얼거리는 것처럼 들린다.
///  · 순찰(Patrol)·수색(Investigate)에서만 재생, 추격(Chase) 등에서는 부드럽게 줄이며 멈춤
///  · 거리 감쇠·벽 너머(Low Pass)·잔향·3D 방향성은 <see cref="SpatialSoundFx"/>가 처리
/// 프리팹: NonFaceGhost 루트(EnemyBase 와 같은 오브젝트)에 붙임
/// </summary>
[DisallowMultipleComponent]
public class NonFaceGhostPatrolMutter : MonoBehaviour
{
    [SerializeField] AudioClip clip;
    [Range(0f, 1f)] [SerializeField] float volume = 1f;
    [Range(0f, 1f)] [SerializeField] float secondVoiceVolume = 0.6f;
    [SerializeField] float fadeIn = 1.0f;
    [SerializeField] float fadeOut = 0.6f;
    [Tooltip("첫 번째 목소리 음높이 범위")]
    [SerializeField] Vector2 pitchRangeA = new Vector2(0.82f, 1.22f);
    [Tooltip("두 번째(낮은) 목소리 음높이 범위")]
    [SerializeField] Vector2 pitchRangeB = new Vector2(0.6f, 0.9f);
    [Tooltip("음높이를 새로 고르는 간격 (최소, 최대 초)")]
    [SerializeField] Vector2 pitchChangeEvery = new Vector2(1.2f, 3.5f);
    [SerializeField] float pitchGlide = 0.9f;
    [SerializeField] float minDistance = 2f;
    [SerializeField] float maxDistance = 22f;
    [SerializeField] float mouthHeight = 1.3f;
    [Header("잔향 (Reverb) — 중얼거림이라 울림을 아주 약하게")]
    [SerializeField] float reverbDecaySeconds = 0.6f;
    [Range(0f, 1.1f)] [SerializeField] float reverbNear = 0.02f;
    [Range(0f, 1.1f)] [SerializeField] float reverbFar = 0.3f;

    class Voice
    {
        public AudioSource source;
        public Vector2 range;
        public float baseVolume;
        public float target = 1f;
        public float current = 1f;
        public float vel;
        public float nextChange;
        public float noiseSeed;
    }

    EnemyBase _enemy;
    Voice _a, _b;
    float _level;

    void Awake()
    {
        _enemy = GetComponentInParent<EnemyBase>();
        if (clip == null)
        {
            enabled = false;
            return;
        }

        _a = Make("PatrolMutter_A", pitchRangeA, 1f);
        _b = Make("PatrolMutter_B", pitchRangeB, secondVoiceVolume);
    }

    Voice Make(string objName, Vector2 range, float vol)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, mouthHeight, 0f);

        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.clip = clip;
        s.spatialBlend = 1f;
        s.spread = 0f;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.volume = 0f;
        // SpatialSoundFx 는 Awake 에서 잔향 값을 읽으므로, 오브젝트를 잠시 꺼 둔 채 값을 넣고 켠다
        go.SetActive(false);
        SpatialSoundFx fx = go.AddComponent<SpatialSoundFx>(); // 거리 감쇠 / 벽 너머 Low Pass / 잔향 / 3D 방향성
        SetPrivate(fx, "reverbDecaySeconds", reverbDecaySeconds);
        SetPrivate(fx, "reverbNear", reverbNear);
        SetPrivate(fx, "reverbFar", reverbFar);
        go.SetActive(true);

        return new Voice
        {
            source = s,
            range = range,
            baseVolume = vol,
            noiseSeed = Random.value * 100f,
            nextChange = 0f
        };
    }

    static void SetPrivate(object target, string field, float value)
    {
        System.Reflection.FieldInfo f = target.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (f != null)
            f.SetValue(target, value);
    }

    void Update()
    {
        if (_enemy == null || _a == null)
            return;

        EnemyBase.State st = _enemy.RuntimeState.CurrentState;
        bool want = st == EnemyBase.State.Patrol || st == EnemyBase.State.Investigate;

        float target = want ? 1f : 0f;
        float fade = want ? fadeIn : fadeOut;
        _level = fade <= 0.01f ? target : Mathf.MoveTowards(_level, target, Time.deltaTime / fade);

        Tick(_a);
        Tick(_b);
    }

    void Tick(Voice v)
    {
        if (v.source == null)
            return;

        if (_level <= 0f)
        {
            if (v.source.isPlaying)
                v.source.Stop();
            return;
        }

        if (!v.source.isPlaying)
        {
            // 매번 다른 위치에서 시작 → 두 겹이 서로 어긋나 같은 조합이 반복되지 않음
            v.source.time = Random.value * Mathf.Max(0.1f, clip.length - 0.1f);
            v.source.Play();
            v.current = v.target = Random.Range(v.range.x, v.range.y);
            v.nextChange = Time.time + Random.Range(pitchChangeEvery.x, pitchChangeEvery.y);
        }

        // 일정 시간마다 새 음높이 → 부드럽게 이동 (중얼거리는 억양)
        if (Time.time >= v.nextChange)
        {
            float p = Random.Range(v.range.x, v.range.y);
            if (Mathf.Abs(p - v.target) < 0.07f)
                p = Random.Range(v.range.x, v.range.y);
            v.target = p;
            v.nextChange = Time.time + Random.Range(pitchChangeEvery.x, pitchChangeEvery.y);
        }

        v.current = Mathf.SmoothDamp(v.current, v.target, ref v.vel, Mathf.Max(0.05f, pitchGlide));
        float wobble = (Mathf.PerlinNoise(v.noiseSeed, Time.time * 0.7f) - 0.5f) * 0.08f;
        v.source.pitch = Mathf.Clamp(v.current + wobble, 0.4f, 2f);

        // 숨 쉬듯 세기가 일렁임
        float breath = Mathf.Lerp(0.45f, 1f, Mathf.PerlinNoise(v.noiseSeed + 7f, Time.time * 0.45f));
        v.source.volume = volume * v.baseVolume * breath * _level;
    }

    void OnDisable()
    {
        if (_a != null && _a.source != null) _a.source.Stop();
        if (_b != null && _b.source != null) _b.source.Stop();
    }
}
