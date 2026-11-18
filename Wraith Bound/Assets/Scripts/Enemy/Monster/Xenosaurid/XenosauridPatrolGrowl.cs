using System.Collections;
using UnityEngine;

/// <summary>
/// 제노(Xenosaurid) 순찰 울음소리 (사운드 전용) — Dinosaur Patrol Growling.
///  · 순찰(Patrol)·수색(Investigate) 중에 계속 반복해서 울음. 같은 소리를 기계적으로 똑같이 되풀이하지 않고,
///    울음 → 무작위 간격 → 울음으로, 매번 음높이·크기를 조금씩 다르게 해 실제 짐승이 우는 것처럼 들리게 함.
///  · 순찰·수색을 벗어나면(추격 등) 서서히 줄이며 멈추고, 순찰로 돌아오면 처음부터 다시 시작.
///  · 거리 감쇠·벽 너머·잔향·3D 방향성은 같은 오브젝트의 <see cref="SpatialSoundFx"/>가 처리.
/// 제노 몸(머리 뼈 등)의 자식 오브젝트(AudioSource + SpatialSoundFx가 있는 곳)에 붙입니다. 게임 로직은 읽기만 합니다.
/// </summary>
[DisallowMultipleComponent]
public class XenosauridPatrolGrowl : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
    [Tooltip("울음 사이 쉬는 시간 (최소, 최대 초) — 무작위")]
    [SerializeField] private Vector2 gapSeconds = new Vector2(0.4f, 2.8f);
    [Tooltip("매번 음높이를 이 범위에서 무작위로 (낮을수록 크고 무거운 울음)")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.88f, 1.06f);
    [Tooltip("매번 크기를 이 배율 범위에서 무작위로")]
    [SerializeField] private Vector2 volumeScaleRange = new Vector2(0.8f, 1f);
    [Tooltip("순찰을 벗어날 때 소리가 줄어드는 시간(초)")]
    [SerializeField] private float fadeOutSeconds = 0.5f;

    private EnemyBase enemy;
    private AudioSource source;
    private float scale = 1f;
    private float level = 1f;
    private bool patrolling;
    private Coroutine routine;

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
        source.volume = 0f;
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }

        routine = null;
        patrolling = false;
    }

    private void Update()
    {
        if (enemy == null || source == null || clip == null)
        {
            return;
        }

        EnemyBase.State st = enemy.RuntimeState.CurrentState;
        bool want = st == EnemyBase.State.Patrol || st == EnemyBase.State.Investigate; // 순찰 + 수색
        if (want && !patrolling)
        {
            patrolling = true;
            level = 1f;
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            source.Stop();
            routine = StartCoroutine(GrowlLoop()); // 순찰에 들어올 때마다 처음부터
        }
        else if (!want && patrolling)
        {
            patrolling = false; // 코루틴이 새 울음을 멈추고, 아래 페이드로 지금 울음을 서서히 줄임
        }

        if (!patrolling)
        {
            level = fadeOutSeconds <= 0.01f ? 0f : Mathf.MoveTowards(level, 0f, Time.deltaTime / fadeOutSeconds);
            if (level <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }

        source.volume = volume * scale * level;
    }

    private IEnumerator GrowlLoop()
    {
        while (patrolling)
        {
            source.clip = clip;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            scale = Random.Range(volumeScaleRange.x, volumeScaleRange.y);
            source.volume = volume * scale * level;
            source.Play();

            // 울음이 끝날 때까지 (pitch가 낮으면 더 길어짐)
            float length = clip.length / Mathf.Max(0.1f, source.pitch);
            for (float t = 0f; t < length && patrolling; t += Time.deltaTime)
            {
                yield return null;
            }

            float gap = Random.Range(gapSeconds.x, gapSeconds.y);
            for (float t = 0f; t < gap && patrolling; t += Time.deltaTime)
            {
                yield return null;
            }
        }
    }
}
