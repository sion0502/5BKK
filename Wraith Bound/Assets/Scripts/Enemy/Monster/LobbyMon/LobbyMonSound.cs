using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// LobbyMon 전용 사운드. 프리팹에는 LobbyMonSound + MonsterEnemy 만 붙이면 됨.
/// 새 적은 Example02Sound 처럼 스크립트 복사해서 클립·간격만 따로 설정.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public class LobbyMonSound : MonoBehaviour
{
    public enum FootstepSyncMode
    {
        Interval,
        AnimationEvent
    }

    [Header("Audio Source (비워두면 자동 생성)")]
    [SerializeField] AudioSource footstepSource;
    [SerializeField] AudioSource vocalSource;

    [Header("발소리 클립")]
    [SerializeField] AudioClip[] walkFootsteps;
    [SerializeField] AudioClip[] runFootsteps;
    [Range(0f, 1f)] [SerializeField] float walkFootstepVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] float runFootstepVolume = 0.95f;
    [SerializeField] Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);

    [Header("발소리 ↔ 발 맞추기")]
    [Tooltip("Interval = Play 실행 후 아래 간격 숫자 조절 / AnimationEvent = 애니 이벤트로만")]
    [SerializeField] FootstepSyncMode footstepSync = FootstepSyncMode.Interval;

    [Tooltip("걸을 때 한 발 간격(초). Play 중 Inspector에서 슬라이더로 바로 조절")]
    [Range(0.1f, 1.5f)] [SerializeField] float walkStepInterval = 0.55f;

    [Tooltip("추격 달릴 때 한 발 간격(초). Play 중 Inspector에서 슬라이더로 바로 조절")]
    [Range(0.08f, 1f)] [SerializeField] float runStepInterval = 0.33f;

    [Tooltip("이 속도 미만이면 발소리 안 냄")]
    [Range(0f, 1f)] [SerializeField] float minMoveSpeed = 0.15f;

    [Header("울음/비명 — 추격(Chase) 중만")]
    [SerializeField] AudioClip[] chaseVocals;
    [Range(0f, 1f)] [SerializeField] float chaseVocalVolume = 0.9f;
    [SerializeField] Vector2 chaseVocalPitchRange = new Vector2(0.98f, 1.02f);
    [Range(0.5f, 10f)] [SerializeField] float chaseVocalInterval = 3f;
    [SerializeField] bool playVocalOnChaseEnter = true;

    [Header("순찰 신음 (선택)")]
    [SerializeField] AudioClip[] patrolMoans;
    [Range(0f, 1f)] [SerializeField] float patrolMoanVolume = 0.45f;
    [Range(3f, 60f)] [SerializeField] float patrolMoanInterval = 14f;

    EnemyBase _enemy;
    NavMeshAgent _agent;
    EnemyBase.State _lastState = (EnemyBase.State)(-1);
    float _nextChaseVocalTime;
    float _nextPatrolMoanTime;
    float _footstepTimer;

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
        else if (state == EnemyBase.State.Patrol)
            TickPatrolMoan();

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

    void TickPatrolMoan()
    {
        if (patrolMoans == null || patrolMoans.Length == 0 || Time.time < _nextPatrolMoanTime)
            return;

        _nextPatrolMoanTime = Time.time + patrolMoanInterval;
        PlayRandomOneShot(vocalSource, patrolMoans, patrolMoanVolume, new Vector2(0.97f, 1.03f));
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
        PlayFootstep(false);
    }

    public void OnRunFootstep()
    {
        PlayFootstep(true);
    }

    public void OnFootstep()
    {
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
                if (source != footstepSource)
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
    }
}
