using UnityEngine;

/// <summary>
/// NurseGhost 순찰 목소리 (사운드 전용). 프롤로그 연출(NurseGhostHideVoice)과 같은 방식으로,
/// 실제 게임의 순찰(Patrol)·수색(Investigate) 중에 NurseGhostVoice_1~4 중 무작위로 하나씩 재생한다.
///  · 한 소리가 끝나면 무작위 간격을 두고 다음 소리를 무작위로 재생
///  · 추격(Chase) 등 그 외 상태에서는 부드럽게 줄이며 멈춘다
///  · 거리 감쇠·벽 너머(Low Pass)·잔향·3D 방향성은 SpatialSoundFx가 처리
/// 프리팹: NurseGhostSound + MonsterEnemy
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public class NurseGhostSound : MonoBehaviour
{
    [SerializeField] AudioClip[] clips = { };
    [Range(0f, 1f)] [SerializeField] float volume = 1f;
    [Tooltip("순찰 시작 후 첫 소리까지 (최소, 최대 초)")]
    [SerializeField] Vector2 firstDelay = new Vector2(0.3f, 1.5f);
    [Tooltip("소리가 끝난 뒤 다음 소리까지 쉬는 시간 (최소, 최대 초)")]
    [SerializeField] Vector2 gapSeconds = new Vector2(0.4f, 3.5f);
    [Tooltip("목소리 한 번이 시작될 때 서서히 커지는 시간(초) — 갑자기 확 들리지 않게")]
    [SerializeField] float voiceFadeIn = 0.6f;
    [Tooltip("순찰이 끝날 때 소리가 줄어드는 시간(초)")]
    [SerializeField] float fadeOutSeconds = 0.5f;
    [SerializeField] float minDistance = 1.2f;
    [SerializeField] float maxDistance = 18f;
    [Tooltip("소리가 나는 높이(m) — 간호사 입 쪽")]
    [SerializeField] float mouthHeight = 1.5f;

    [Header("추격(Chase) 웃음 — 루프 (끝이 확 올라가며 울린 뒤 처음으로 자연스럽게 이어짐)")]
    [SerializeField] AudioClip chaseClip;
    [Range(0f, 1f)] [SerializeField] float chaseVolume = 1f;
    [SerializeField] float chaseFadeIn = 0.15f;
    [SerializeField] float chaseFadeOut = 0.35f;
    [Tooltip("루프가 한 바퀴 돌 때마다 음높이를 이 범위에서 새로 고름 (같은 소리가 반복되는 느낌 방지)")]
    [SerializeField] Vector2 chasePitchRange = new Vector2(0.88f, 1.16f);
    [Tooltip("새 음높이로 부드럽게 넘어가는 시간(초)")]
    [SerializeField] float chasePitchGlide = 1.2f;
    [Tooltip("음높이가 계속 살짝 흔들리는 정도(±)와 속도")]
    [SerializeField] float chasePitchWobble = 0.025f;
    [SerializeField] float chaseWobbleSpeed = 0.9f;

    EnemyBase _enemy;
    AudioSource _source;
    AudioSource _chaseSource;
    float _chaseLevel;
    float _chasePitchTarget = 1f;
    float _chasePitchBase = 1f;
    float _chasePitchVel;
    float _chaseLastTime;
    float _wobblePhase;
    float _nextAt;
    float _level = 1f;
    float _voiceStart;
    bool _wasWanting;

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();

        // 프롤로그 연출: 간호사(PrologueNurse)는 NurseGhostHideVoice가 따로 소리를 내고,
        // 캠코더로만 보이는 형체(CamcorderOnlyApparition)는 서 있기만 하므로 순찰 소리가 나면 안 됨
        if (GetComponent<PrologueNurse>() != null || GetComponentInParent<CamcorderOnlyApparition>() != null)
        {
            enabled = false;
            return;
        }

        GameObject go = new GameObject("PatrolVoice");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, mouthHeight, 0f);

        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 1f;
        _source.spread = 0f;
        _source.dopplerLevel = 0f;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;
        _source.volume = volume;

        go.AddComponent<SpatialSoundFx>(); // 거리 감쇠 / 벽 너머 Low Pass / 잔향 / 3D 방향성

        GameObject cgo = new GameObject("ChaseLaugh");
        cgo.transform.SetParent(transform, false);
        cgo.transform.localPosition = new Vector3(0f, mouthHeight, 0f);
        _chaseSource = cgo.AddComponent<AudioSource>();
        _chaseSource.playOnAwake = false;
        _chaseSource.loop = true;
        _chaseSource.spatialBlend = 1f;
        _chaseSource.spread = 0f;
        _chaseSource.dopplerLevel = 0f;
        _chaseSource.rolloffMode = AudioRolloffMode.Linear;
        _chaseSource.minDistance = minDistance;
        _chaseSource.maxDistance = maxDistance + 7f;
        _chaseSource.clip = chaseClip;
        _chaseSource.volume = 0f;
        cgo.AddComponent<SpatialSoundFx>();
    }

    void PickChasePitch()
    {
        float lo = Mathf.Min(chasePitchRange.x, chasePitchRange.y);
        float hi = Mathf.Max(chasePitchRange.x, chasePitchRange.y);
        float p = Random.Range(lo, hi);
        // 직전 음높이와 너무 비슷하면 한 번 더 뽑음
        if (Mathf.Abs(p - _chasePitchTarget) < 0.06f)
            p = Random.Range(lo, hi);
        _chasePitchTarget = p;
    }

    void UpdateChase(bool chasing)
    {
        if (_chaseSource == null || chaseClip == null)
            return;

        float target = chasing ? 1f : 0f;
        float fade = chasing ? chaseFadeIn : chaseFadeOut;
        _chaseLevel = fade <= 0.01f ? target : Mathf.MoveTowards(_chaseLevel, target, Time.deltaTime / fade);

        if (_chaseLevel > 0f)
        {
            if (!_chaseSource.isPlaying)
            {
                _chaseSource.Play(); // 추격 시작마다 처음부터
                _chaseLastTime = 0f;
                _wobblePhase = Random.value * 10f;
                PickChasePitch();
                _chasePitchBase = _chasePitchTarget;
            }

            // 루프가 한 바퀴 돌아 처음으로 돌아오면 새 음높이를 고름
            float tNow = _chaseSource.time;
            if (tNow < _chaseLastTime - 0.5f)
                PickChasePitch();
            _chaseLastTime = tNow;

            _chasePitchBase = Mathf.SmoothDamp(_chasePitchBase, _chasePitchTarget, ref _chasePitchVel,
                Mathf.Max(0.05f, chasePitchGlide));
            float wobble = Mathf.Sin((Time.time + _wobblePhase) * chaseWobbleSpeed * 6.2832f) * chasePitchWobble;
            _chaseSource.pitch = Mathf.Clamp(_chasePitchBase + wobble, 0.5f, 2f);
            _chaseSource.volume = chaseVolume * _chaseLevel;
        }
        else if (_chaseSource.isPlaying)
        {
            _chaseSource.Stop();
        }
    }

    void Update()
    {
        if (_enemy == null || _source == null)
            return;

        EnemyBase.State state = _enemy.RuntimeState.CurrentState;
        UpdateChase(state == EnemyBase.State.Chase);

        if (clips == null || clips.Length == 0)
            return;

        bool want = state == EnemyBase.State.Patrol || state == EnemyBase.State.Investigate;

        if (want && !_wasWanting)
            _nextAt = Time.time + Random.Range(firstDelay.x, firstDelay.y);
        _wasWanting = want;

        if (want)
        {
            _level = 1f;
            if (!_source.isPlaying && Time.time >= _nextAt)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    _source.clip = clip;
                    _source.volume = 0f;
                    _source.Play();
                    _voiceStart = Time.time;
                }

                _nextAt = Time.time + (clip != null ? clip.length : 0f)
                          + Random.Range(gapSeconds.x, gapSeconds.y);
            }

            if (_source.isPlaying)
                _source.volume = volume * VoiceRamp();
        }
        else if (_source.isPlaying)
        {
            _level = fadeOutSeconds <= 0.01f ? 0f
                : Mathf.MoveTowards(_level, 0f, Time.deltaTime / fadeOutSeconds);
            _source.volume = volume * _level * VoiceRamp();
            if (_level <= 0f)
                _source.Stop();
        }
    }

    // 목소리 시작 후 voiceFadeIn 초 동안 0 → 1 로 부드럽게 커짐
    float VoiceRamp()
    {
        if (voiceFadeIn <= 0.01f)
            return 1f;
        float k = Mathf.Clamp01((Time.time - _voiceStart) / voiceFadeIn);
        return k * k * (3f - 2f * k);
    }

    void OnDisable()
    {
        if (_source != null)
            _source.Stop();
        if (_chaseSource != null)
            _chaseSource.Stop();
    }
}
