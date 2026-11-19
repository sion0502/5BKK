using UnityEngine;

/// <summary>
/// 얼굴없는귀신(Non-Face Ghost) 추격 비명 (사운드 전용).
///  · 추격(Chase) 중에만: 비명 → 1~2초 랜덤 텀 → 다시 비명 … 반복.
///  · 비명마다 피치를 다르게(비명 안에서도 살짝 솟구침) 해서 진짜 쫓아오는 느낌.
///  · 추격이 끝나면 부드럽게 줄어들며 멈춤.
///  · 거리 감쇠·벽 너머·잔향·3D 방향성은 같은 오브젝트의 <see cref="SpatialSoundFx"/>가 처리.
/// 귀신 몸의 자식 오브젝트(AudioSource + SpatialSoundFx가 있는 곳)에 붙입니다. 게임 로직은 읽기만 합니다.
/// </summary>
[DisallowMultipleComponent]
public class NonFaceGhostChaseScream : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] private float volumeVariation = 0.08f;
    [Tooltip("비명마다 랜덤 피치 범위")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.88f, 1.22f);
    [Tooltip("비명 하나 안에서 끝으로 갈수록 피치가 오르는 비율 (0.06 = 6%)")]
    [SerializeField, Range(0f, 0.2f)] private float pitchRise = 0.06f;
    [Tooltip("비명이 끝난 뒤 다음 비명까지 랜덤 텀(초)")]
    [SerializeField] private Vector2 gapRange = new Vector2(1f, 2f);
    [SerializeField] private float fadeInSeconds = 0.04f;
    [Tooltip("추격이 끝날 때 줄어드는 시간(초)")]
    [SerializeField] private float fadeOutSeconds = 0.35f;
    [Tooltip("비명이 자연스럽게 끝나도록 마지막 구간을 줄이는 시간(초)")]
    [SerializeField] private float tailFadeSeconds = 0.25f;

    /// <summary>추격 비명 클립 (시연 연출의 예고 소리가 같은 비명을 씀).</summary>
    public AudioClip Clip => clip;

    private enum Phase { Idle, Playing, Gap }

    private EnemyBase enemy;
    private AudioSource source;
    private Phase phase = Phase.Idle;
    private float gapTimer;
    private float level;
    private float basePitch = 1f;
    private float baseVolume = 1f;
    private float lastPitch = 1f;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = 0f;
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }

        phase = Phase.Idle;
        level = 0f;
    }

    private void Update()
    {
        if (enemy == null || source == null || clip == null)
        {
            return;
        }

        bool chasing = enemy.RuntimeState.CurrentState == EnemyBase.State.Chase;

        if (!chasing)
        {
            if (phase != Phase.Idle)
            {
                level = Mathf.MoveTowards(level, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeOutSeconds));
                source.volume = baseVolume * level;
                if (level <= 0f)
                {
                    source.Stop();
                    phase = Phase.Idle;
                }
            }

            return;
        }

        switch (phase)
        {
            case Phase.Idle:
                StartScream();
                break;
            case Phase.Gap:
                gapTimer -= Time.deltaTime;
                if (gapTimer <= 0f)
                {
                    StartScream();
                }

                break;
            case Phase.Playing:
                if (!source.isPlaying)
                {
                    phase = Phase.Gap;
                    gapTimer = Random.Range(gapRange.x, Mathf.Max(gapRange.x, gapRange.y));
                    break;
                }

                float t = Mathf.Clamp01(source.time / Mathf.Max(0.01f, clip.length));
                source.pitch = basePitch * (1f + pitchRise * t); // 끝으로 갈수록 솟구침
                float remainReal = (clip.length - source.time) / Mathf.Max(0.1f, source.pitch);
                float tail = Mathf.Clamp01(remainReal / Mathf.Max(0.01f, tailFadeSeconds));
                level = Mathf.MoveTowards(level, 1f, Time.deltaTime / Mathf.Max(0.01f, fadeInSeconds));
                source.volume = baseVolume * level * tail;
                break;
        }
    }

    private void StartScream()
    {
        // 직전 비명과 너무 비슷한 피치는 피함
        float p = Random.Range(pitchRange.x, pitchRange.y);
        for (int i = 0; i < 4 && Mathf.Abs(p - lastPitch) < 0.08f; i++)
        {
            p = Random.Range(pitchRange.x, pitchRange.y);
        }

        lastPitch = p;
        basePitch = p;
        baseVolume = Mathf.Clamp01(volume * (1f + Random.Range(-volumeVariation, volumeVariation)));
        level = 0f;
        source.Stop();
        source.clip = clip;
        source.pitch = basePitch;
        source.volume = 0f;
        source.time = 0f;
        source.Play();
        phase = Phase.Playing;
    }
}
