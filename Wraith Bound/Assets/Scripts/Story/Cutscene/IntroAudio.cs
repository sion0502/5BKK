using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 인트로 컷씬 사운드.
///  · 컷씬이 시작되면 겨울 바람(Winter Wind) + 귀뚜라미(Cricket sound)를 끝없이 loop — 병원에 들어가기 전까지.
///  · 기획서(쪽지)를 들어 올릴 때 / 다시 내려놓을 때 Paper flutter.
///  · 병원에 들어가는 순간(slamTime) Violent door slam → 바람·귀뚜라미 페이드 아웃 → "바람인가?" 자막.
/// 타임라인(PlayableDirector)의 재생 상태만 읽어서 동작하므로 메인 메뉴 배경으로 Additive 로드돼 있을 땐 조용합니다.
/// </summary>
public class IntroAudio : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [Tooltip("기획서를 들어 올리는 오브젝트 — Blend 값이 올라가면 '꺼냄', 내려가면 '집어넣음'")]
    [SerializeField] private CutsceneHeldDocument heldDocument;

    [Header("Clips")]
    [SerializeField] private AudioClip windClip;
    [SerializeField] private AudioClip cricketClip;
    [SerializeField] private AudioClip paperClip;
    [SerializeField] private AudioClip doorSlamClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float cricketVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float paperVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float doorSlamVolume = 1f;
    [SerializeField] private float loopFadeInSeconds = 1.5f;
    [Tooltip("귀뚜라미가 반복될 때 끝·처음이 서로 겹치며 자연스럽게 이어지는 시간(초)")]
    [SerializeField] private float cricketCrossfadeSeconds = 2f;

    [Header("Paper flutter (원본이 54초라 소리가 또렷한 구간만 잘라 씀)")]
    [SerializeField] private float paperStartOffset = 8.4f;
    [SerializeField] private float paperPlaySeconds = 1.8f;
    [SerializeField] private float paperFadeOutSeconds = 0.5f;

    [Header("병원 진입 (타임라인 시간, 초)")]
    [Tooltip("암전이 완전히 검게 차는 시점 — 이때 문 쾅")]
    [SerializeField] private float slamTime = 61.6f;
    [SerializeField] private float loopFadeOutSeconds = 1.2f;
    [SerializeField] private float lineDelay = 0.8f;
    [SerializeField] private string lineSpeaker = "나";
    [SerializeField] private string line = "바람인가?";
    [SerializeField] private float lineDuration = 1.8f;

    private AudioSource windSource;
    private AudioSource cricketSource;
    private AudioSource cricketSourceB;
    private AudioSource paperSource;
    private AudioSource sfxSource;

    private bool started;
    private bool slammed;
    private float loopLevel;
    private float lastBlend;
    private Coroutine paperRoutine;

    private void Awake()
    {
        windSource = CreateSource("Wind", windClip, true);
        // 귀뚜라미는 loop 대신 두 개가 번갈아 겹치며(크로스페이드) 이어 재생 — 끝나자마자 처음이 툭 시작되는 어색함 방지
        cricketSource = CreateSource("Cricket", cricketClip, false);
        cricketSourceB = CreateSource("CricketB", cricketClip, false);
        paperSource = CreateSource("Paper", paperClip, false);
        sfxSource = CreateSource("Sfx", null, false);
    }

    private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop)
    {
        GameObject go = new GameObject("IntroAudio_" + sourceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop; // 노래가 끝나도 계속 반복
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        source.volume = 0f;
        return source;
    }

    private void Update()
    {
        if (director == null)
        {
            return;
        }

        bool playing = director.state == PlayState.Playing;

        if (!started)
        {
            if (!playing)
            {
                return;
            }

            Begin();
        }

        // ── 바람 + 귀뚜라미: 시작하면 켜 두고, 문이 쾅 닫히면 끔. 건너뛰기(일시정지·정지)에도 끔
        float fadeSeconds = slammed ? loopFadeOutSeconds : (playing ? loopFadeInSeconds : 0.5f);
        float target = playing && !slammed ? 1f : 0f;
        loopLevel = Mathf.MoveTowards(loopLevel, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        windSource.volume = windVolume * loopLevel;
        UpdateCricket();

        if (target <= 0f && loopLevel <= 0f && (windSource.isPlaying || cricketSource.isPlaying || cricketSourceB.isPlaying))
        {
            windSource.Stop();
            cricketSource.Stop();
            cricketSourceB.Stop();
        }

        if (!playing)
        {
            return;
        }

        // ── 쪽지: 들어 올릴 때 / 내려놓을 때
        if (heldDocument != null)
        {
            float blend = heldDocument.Blend;
            bool raising = lastBlend <= 0.001f && blend > 0.001f;
            bool lowering = lastBlend >= 0.99f && blend < 0.99f;
            if (raising || lowering)
            {
                PlayPaper();
            }

            lastBlend = blend;
        }

        // ── 병원 문 쾅
        if (!slammed && director.time >= slamTime)
        {
            slammed = true;
            if (doorSlamClip != null)
            {
                sfxSource.PlayOneShot(doorSlamClip, doorSlamVolume);
            }

            StartCoroutine(WindLine());
        }
    }

    private void UpdateCricket()
    {
        if (cricketClip == null)
        {
            return;
        }

        float length = cricketClip.length;
        float fade = Mathf.Clamp(cricketCrossfadeSeconds, 0.05f, length * 0.5f);
        UpdateCricketVoice(cricketSource, cricketSourceB, length, fade);
        UpdateCricketVoice(cricketSourceB, cricketSource, length, fade);
    }

    private void UpdateCricketVoice(AudioSource voice, AudioSource other, float length, float fade)
    {
        if (!voice.isPlaying)
        {
            return;
        }

        float t = voice.time;

        // 끝나기 fade초 전에 다른 쪽을 처음부터 시작 → 서로 겹치며 이어짐
        if (t >= length - fade && !other.isPlaying)
        {
            other.time = 0f;
            other.Play();
        }

        // 시작·끝 모두 서서히
        float envelope = Mathf.Clamp01(Mathf.Min(t / fade, (length - t) / fade));
        voice.volume = cricketVolume * loopLevel * envelope;
    }

    private void Begin()
    {
        started = true;
        loopLevel = 0f;
        lastBlend = heldDocument != null ? heldDocument.Blend : 0f;

        if (windClip != null)
        {
            windSource.Play();
        }

        if (cricketClip != null)
        {
            cricketSource.time = 0f;
            cricketSource.Play();
        }
    }

    private void PlayPaper()
    {
        if (paperClip == null)
        {
            return;
        }

        if (paperRoutine != null)
        {
            StopCoroutine(paperRoutine);
        }

        paperRoutine = StartCoroutine(PaperRoutine());
    }

    private IEnumerator PaperRoutine()
    {
        paperSource.Stop();
        paperSource.volume = paperVolume;
        paperSource.time = Mathf.Clamp(paperStartOffset, 0f, Mathf.Max(0f, paperClip.length - 0.1f));
        paperSource.Play();

        float solid = Mathf.Max(0f, paperPlaySeconds - paperFadeOutSeconds);
        yield return new WaitForSecondsRealtime(solid);

        for (float t = 0f; t < paperFadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            paperSource.volume = paperVolume * (1f - t / paperFadeOutSeconds);
            yield return null;
        }

        paperSource.Stop();
        paperRoutine = null;
    }

    private IEnumerator WindLine()
    {
        yield return new WaitForSecondsRealtime(lineDelay);

        // 그 사이 건너뛰기로 컷씬이 끝났다면 자막을 띄우지 않음
        if (director != null && director.state == PlayState.Playing)
        {
            SubtitleUI.ShowForDuration(lineSpeaker, line, lineDuration);
        }
    }
}
