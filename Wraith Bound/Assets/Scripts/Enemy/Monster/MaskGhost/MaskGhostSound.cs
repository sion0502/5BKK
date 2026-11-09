using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// MaskGhost 전용 사운드.
/// 프리팹: MaskGhostSound + MonsterEnemy(또는 GhostEnemy)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public class MaskGhostSound : MonoBehaviour
{
    public enum FootstepSyncMode
    {
        Interval,
        AnimationEvent
    }

    [Header("Audio Source (비워두면 자동 생성)")]
    [SerializeField] AudioSource footstepSource;
    [SerializeField] AudioSource vocalSource;
    [SerializeField] AudioSource patrolSource;

    [Header("발소리 클립")]
    [SerializeField] AudioClip[] walkFootsteps;
    [SerializeField] AudioClip[] runFootsteps;
    [Range(0f, 1f)] [SerializeField] float walkFootstepVolume = 0.55f;
    [Range(0f, 1f)] [SerializeField] float runFootstepVolume = 0.75f;
    [SerializeField] Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);

    [Header("발소리 ↔ 발 맞추기 (Play 실행 후 슬라이더 조절)")]
    [Tooltip("Interval = 간격으로 맞춤 / AnimationEvent = 애니 발 프레임에 OnWalkFootstep·OnRunFootstep")]
    [SerializeField] FootstepSyncMode footstepSync = FootstepSyncMode.Interval;

    [Tooltip("걸을 때 한 발 간격(초) — 빠르면 줄이고, 늦으면 늘리기")]
    [Range(0.1f, 1.5f)] [SerializeField] float walkStepInterval = 0.55f;

    [Tooltip("추격 달릴 때 한 발 간격(초)")]
    [Range(0.08f, 1f)] [SerializeField] float runStepInterval = 0.34f;

    [Tooltip("이 속도 미만이면 발소리 안 냄")]
    [Range(0f, 1f)] [SerializeField] float minMoveSpeed = 0.12f;

    [Header("울음/비명 — 추격(Chase) 중만 3초 간격")]
    [SerializeField] AudioClip[] chaseVocals;
    [Range(0f, 1f)] [SerializeField] float chaseVocalVolume = 0.9f;
    [SerializeField] Vector2 chaseVocalPitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] float chaseVocalInterval = 3f;
    [SerializeField] bool playVocalOnChaseEnter = true;

    [Header("순찰(Patrol)·수색(Investigate) 노래 — 추격(Chase) 때는 멈춤")]
    [SerializeField] AudioClip patrolLoop;
    [Range(0f, 1f)] [SerializeField] float patrolLoopVolume = 0.8f;
    [Tooltip("Patrol/수색 시작 시 소리가 커지는 시간(초)")]
    [SerializeField] float patrolFadeIn = 0.5f;
    [Tooltip("Patrol/수색을 벗어날 때 소리가 줄어드는 시간(초)")]
    [SerializeField] float patrolFadeOut = 0.8f;
    [Tooltip("이 거리 안에서는 최대 볼륨")]
    [SerializeField] float patrolMinDistance = 3f;
    [Tooltip("이 거리 밖에서는 안 들림")]
    [SerializeField] float patrolMaxDistance = 25f;

    EnemyBase _enemy;
    NavMeshAgent _agent;
    EnemyBase.State _lastState = (EnemyBase.State)(-1);
    float _nextChaseVocalTime;
    float _footstepTimer;
    float _patrolLevel;
    bool _patrolPaused;

    public float PlaySpeed => _agent != null ? _agent.velocity.magnitude : 0f;
    public float NextFootstepIn => Mathf.Max(0f, _footstepTimer);
    public float NextChaseVocalIn => Mathf.Max(0f, _nextChaseVocalTime - Time.time);
    public bool IsRunFootstep =>
        _enemy != null && _enemy.RuntimeState.CurrentState == EnemyBase.State.Chase;

    void Reset()
    {
        EnsureAudioSources();
    }

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _agent = GetComponent<NavMeshAgent>();
        EnsureAudioSources();
    }

    void Update()
    {
        if (_enemy == null)
            return;

        EnemyBase.State state = _enemy.RuntimeState.CurrentState;

        if (state != _lastState)
        {
            OnEnemyStateChanged(state);
            _lastState = state;
        }

        if (state == EnemyBase.State.Chase)
            TickChaseVocal();

        TickPatrolLoop(state);

        if (footstepSync == FootstepSyncMode.Interval)
            TickIntervalFootsteps(state);
    }

    void OnEnemyStateChanged(EnemyBase.State next)
    {
        if (next != EnemyBase.State.Chase || !playVocalOnChaseEnter)
            return;

        PlayChaseVocal();
        _nextChaseVocalTime = Time.time + chaseVocalInterval;
    }

    void TickChaseVocal()
    {
        if (chaseVocals == null || chaseVocals.Length == 0 || Time.time < _nextChaseVocalTime)
            return;

        PlayChaseVocal();
        _nextChaseVocalTime = Time.time + chaseVocalInterval;
    }

    void TickPatrolLoop(EnemyBase.State state)
    {
        if (patrolSource == null || patrolLoop == null)
            return;

        bool wantPlay = state == EnemyBase.State.Patrol || state == EnemyBase.State.Investigate;
        float target = wantPlay ? 1f : 0f;
        float fadeTime = wantPlay ? patrolFadeIn : patrolFadeOut;
        _patrolLevel = fadeTime <= 0.01f
            ? target
            : Mathf.MoveTowards(_patrolLevel, target, Time.deltaTime / fadeTime);

        if (_patrolLevel > 0f)
        {
            if (!patrolSource.isPlaying)
            {
                // 멈췄던 위치에서 이어서 재생 (처음부터 다시 시작하지 않음)
                if (_patrolPaused)
                    patrolSource.UnPause();
                else
                    patrolSource.Play();

                _patrolPaused = false;
            }

            patrolSource.volume = patrolLoopVolume * _patrolLevel;
        }
        else if (patrolSource.isPlaying)
        {
            patrolSource.Pause();
            _patrolPaused = true;
        }
    }

    void TickIntervalFootsteps(EnemyBase.State state)
    {
        if (_agent == null)
            return;

        if (_agent.velocity.magnitude < minMoveSpeed)
        {
            _footstepTimer = 0f;
            return;
        }

        bool running = state == EnemyBase.State.Chase;
        AudioClip[] clips = running ? runFootsteps : walkFootsteps;
        if (clips == null || clips.Length == 0)
            return;

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer > 0f)
            return;

        _footstepTimer = running ? runStepInterval : walkStepInterval;
        PlayFootstep(running);
    }

    public void OnWalkFootstep()
    {
        if (footstepSync == FootstepSyncMode.AnimationEvent)
            PlayFootstep(false);
    }

    public void OnRunFootstep()
    {
        if (footstepSync == FootstepSyncMode.AnimationEvent)
            PlayFootstep(true);
    }

    public void OnFootstep()
    {
        if (footstepSync != FootstepSyncMode.AnimationEvent)
            return;

        bool running = _enemy != null && _enemy.RuntimeState.CurrentState == EnemyBase.State.Chase;
        PlayFootstep(running);
    }

    void PlayChaseVocal()
    {
        PlayRandomOneShot(vocalSource, chaseVocals, chaseVocalVolume, chaseVocalPitchRange);
    }

    void PlayFootstep(bool running)
    {
        AudioClip[] clips = running ? runFootsteps : walkFootsteps;
        float volume = running ? runFootstepVolume : walkFootstepVolume;
        PlayRandomOneShot(footstepSource, clips, volume, footstepPitchRange);
    }

    void PlayRandomOneShot(AudioSource source, AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        if (source == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        source.pitch = Mathf.Clamp(Random.Range(pitchRange.x, pitchRange.y), 0.5f, 2f);
        source.PlayOneShot(clip, volume);
    }

    void EnsureAudioSources()
    {
        if (footstepSource == null)
            footstepSource = GetComponent<AudioSource>();

        if (footstepSource == null)
            footstepSource = gameObject.AddComponent<AudioSource>();

        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 1f;

        if (vocalSource == null)
        {
            foreach (AudioSource source in GetComponents<AudioSource>())
            {
                if (source != footstepSource && source != patrolSource)
                {
                    vocalSource = source;
                    break;
                }
            }
        }

        if (vocalSource == null)
        {
            vocalSource = gameObject.AddComponent<AudioSource>();
            vocalSource.playOnAwake = false;
            vocalSource.spatialBlend = 1f;
        }

        if (patrolSource == null)
            patrolSource = gameObject.AddComponent<AudioSource>();

        patrolSource.playOnAwake = false;
        patrolSource.loop = true;
        patrolSource.spatialBlend = 1f;
        patrolSource.rolloffMode = AudioRolloffMode.Linear;
        patrolSource.minDistance = patrolMinDistance;
        patrolSource.maxDistance = patrolMaxDistance;
        patrolSource.clip = patrolLoop;
        patrolSource.volume = 0f;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (chaseVocalInterval < 0.5f)
            chaseVocalInterval = 0.5f;
    }
#endif
}
