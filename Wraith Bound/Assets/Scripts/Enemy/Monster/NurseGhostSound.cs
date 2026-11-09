using UnityEngine;

/// <summary>
/// NurseGhost 전용 사운드.
/// 순찰(Patrol)·수색(Investigate) 중에는 노래를 반복 재생하고, 추격(Chase) 중에는 멈춥니다.
/// 프리팹: NurseGhostSound + MonsterEnemy
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public class NurseGhostSound : MonoBehaviour
{
    [Header("Audio Source (비워두면 자동 생성)")]
    [SerializeField] AudioSource patrolSource;

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
    float _patrolLevel;
    bool _patrolPaused;

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        EnsureAudioSource();
    }

    void Update()
    {
        if (_enemy == null || patrolSource == null || patrolLoop == null)
            return;

        EnemyBase.State state = _enemy.RuntimeState.CurrentState;
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

    void EnsureAudioSource()
    {
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
}
