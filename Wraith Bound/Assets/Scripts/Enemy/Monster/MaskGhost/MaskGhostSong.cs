using UnityEngine;

/// <summary>
/// 탈춤귀신 노래 (사운드 전용) — 태평소 능게.
///  · 순찰 / 수색 / 추격 모든 상태에서 계속 울림.
///  · 상태가 바뀌는 순간(순찰→추격, 추격→순찰, 수색→순찰 등)마다 노래를 처음(0초)부터 다시 시작.
///  · 노래 파일 자체가 끝↔처음이 겹쳐 이어지는 루프 파일이라, 반복돼도 뚝 끊기지 않음.
///  · 거리 감쇠·벽 너머·잔향·3D 방향성은 같은 오브젝트의 <see cref="SpatialSoundFx"/>가 처리.
/// 귀신 몸의 자식 오브젝트(AudioSource + SpatialSoundFx가 있는 곳)에 붙입니다.
/// 게임 로직은 읽기만 합니다(EnemyBase의 현재 상태).
/// </summary>
[DisallowMultipleComponent]
public class MaskGhostSong : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [Header("추격(Chase) 전용 — 휘모리장단")]
    [SerializeField] private AudioClip chaseClip;
    [SerializeField, Range(0f, 1f)] private float chaseVolume = 1f;
    [Tooltip("추격 때 살짝 빠르게 해서 더 긴박하게")]
    [SerializeField, Range(0.8f, 1.3f)] private float chasePitch = 1.05f;

    [Tooltip("처음부터 다시 시작할 때 소리가 커지는 시간(초) — 뚝 끊기는 소리 방지")]
    [SerializeField] private float fadeInSeconds = 0.2f;

    private EnemyBase enemy;
    private AudioSource source;
    private EnemyBase.State lastState = (EnemyBase.State)(-1);
    private float level;
    private bool chasing;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = true;
        source.clip = clip;
        source.volume = 0f;
        source.pitch = 1f;
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }

        lastState = (EnemyBase.State)(-1);
        level = 0f;
    }

    private void Update()
    {
        if (enemy == null || source == null || clip == null)
        {
            return;
        }

        EnemyBase.State state = enemy.RuntimeState.CurrentState;
        if (state != lastState)
        {
            lastState = state;
            chasing = state == EnemyBase.State.Chase && chaseClip != null;
            source.Stop();
            source.clip = chasing ? chaseClip : clip;
            source.pitch = chasing ? chasePitch : 1f;
            source.time = 0f;
            level = 0f;
            source.Play(); // 상태가 바뀔 때마다 처음부터
        }

        level = fadeInSeconds <= 0.01f ? 1f : Mathf.MoveTowards(level, 1f, Time.deltaTime / fadeInSeconds);
        source.volume = (chasing ? chaseVolume : volume) * level;
    }
}
