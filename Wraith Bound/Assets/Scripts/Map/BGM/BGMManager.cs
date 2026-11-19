using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource audioSource;
    private string ownerSceneName;
    private float defaultVolume;
    private Coroutine fadeRoutine;

    [Header("Crossfade Loop (0 = 기존처럼 AudioSource loop)")]
    [Tooltip("0보다 크면 곡의 끝과 처음을 이만큼 겹쳐서, 확 끝났다 다시 시작하지 않고 자연스럽게 무한 반복합니다")]
    [SerializeField] private float loopCrossfadeSeconds = 0f;

    [Tooltip("반복 구간 시작/끝(곡 안의 초). 박자·멜로디가 정확히 한 바퀴 도는 길이로 잡으면 속도 변화 없이 이어짐. 끝이 시작보다 크지 않으면 곡 전체를 겹쳐서 반복")]
    [SerializeField] private float loopStartSeconds = 0f;
    [SerializeField] private float loopEndSeconds = 0f;

    private AudioSource voiceB;
    private float entryA;
    private float entryB;
    private bool crossfade;
    private bool waitingForMenu;
    private float master = 1f;

    // 설정의 '배경음' 음량을 곱한 기본 볼륨 (환경음은 AudioListener라 BGM 소스는 ignoreListenerVolume으로 분리)
    private float Level => defaultVolume * GameSettings.BgmVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        ownerSceneName = gameObject.scene.name;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.ignoreListenerVolume = true;
            defaultVolume = audioSource.volume;
            crossfade = loopCrossfadeSeconds > 0.01f && audioSource.clip != null;
            if (crossfade)
            {
                SetupCrossfade();
            }

            if (HubArrival.MenuBackdrop)
            {
                // 메인 메뉴 뒤에 허브가 깔려 있는 동안엔 메뉴 BGM만 — 이어서 하기로 들어갈 때 시작
                audioSource.Stop();
                waitingForMenu = true;
            }
            else if (audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    private void SetupCrossfade()
    {
        audioSource.loop = false;
        audioSource.volume = 0f;

        voiceB = gameObject.AddComponent<AudioSource>();
        voiceB.clip = audioSource.clip;
        voiceB.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        voiceB.spatialBlend = audioSource.spatialBlend;
        voiceB.priority = audioSource.priority;
        voiceB.playOnAwake = false;
        voiceB.loop = false;
        voiceB.ignoreListenerVolume = true;
        voiceB.volume = 0f;
    }

    private void Update()
    {
        if (waitingForMenu)
        {
            if (HubArrival.MenuBackdrop)
            {
                // PlayOnAwake가 Awake 뒤에 켜질 수 있어 메뉴가 떠 있는 동안 계속 꺼 둠
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }

                return;
            }

            waitingForMenu = false;
            PlayBGM();
        }

        if (!crossfade && audioSource != null && fadeRoutine == null && audioSource.isPlaying)
        {
            audioSource.volume = Level; // 설정에서 배경음 슬라이더를 움직이면 바로 반영
        }

        if (!crossfade || audioSource == null || voiceB == null)
        {
            return;
        }

        UpdateCrossfadeVoice(audioSource, voiceB, true);
        UpdateCrossfadeVoice(voiceB, audioSource, false);
    }

    // 끝나기 fade초 전에 반대쪽을 이어서 시작 → 서로 겹치며 이어짐.
    // 반복 구간(loopStart~loopEnd)이 지정돼 있으면 같은 박자 위치에서 들어와 속도·박자가 흔들리지 않음(선형 겹침).
    private void UpdateCrossfadeVoice(AudioSource voice, AudioSource other, bool isA)
    {
        if (!voice.isPlaying)
        {
            return;
        }

        float length = voice.clip.length;
        bool region = loopEndSeconds > loopStartSeconds + 0.5f;
        float end = region ? Mathf.Min(loopEndSeconds, length) : length;
        float span = end - (region ? loopStartSeconds : 0f);
        float fade = Mathf.Clamp(loopCrossfadeSeconds, 0.05f, span * 0.5f);
        float entry = isA ? entryA : entryB;
        float t = voice.time;

        if (region && t >= end)
        {
            voice.Stop();
            return;
        }

        if (t >= end - fade && !other.isPlaying)
        {
            // 반복 구간이면 (구간 시작 - fade)에서 들어와, 지금 끝나가는 소리와 같은 박자 위치끼리 겹침
            float enter = region ? Mathf.Max(0f, loopStartSeconds - fade) : 0f;
            other.time = enter;
            other.Play();
            if (isA)
            {
                entryB = enter;
            }
            else
            {
                entryA = enter;
            }
        }

        float fadeIn;
        float fadeOut;
        if (region)
        {
            fadeIn = Mathf.Clamp01((t - entry) / fade);
            fadeOut = t >= end - fade ? Mathf.Clamp01((end - t) / fade) : 1f;
        }
        else
        {
            fadeIn = Mathf.Sin(Mathf.Clamp01(t / fade) * Mathf.PI * 0.5f);
            fadeOut = Mathf.Cos(Mathf.Clamp01((t - (length - fade)) / fade) * Mathf.PI * 0.5f);
        }

        voice.volume = Level * master * fadeIn * fadeOut;
    }

    private bool AnyPlaying()
    {
        return audioSource != null && (audioSource.isPlaying || (voiceB != null && voiceB.isPlaying));
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == ownerSceneName)
        {
            return;
        }

        StopBGM();

        if (Instance == this)
        {
            Instance = null;
        }

        Destroy(gameObject);
    }

    public void StopBGM()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (crossfade)
        {
            master = 1f;
            audioSource.Stop();
            voiceB.Stop();
            return;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = Level;
        }
    }

    public void PlayBGM()
    {
        if (audioSource == null)
        {
            return;
        }

        if (crossfade)
        {
            master = 1f;
            entryA = 0f;
            voiceB.Stop();
            audioSource.time = 0f;
            audioSource.Play();
            return;
        }

        audioSource.volume = Level;
        audioSource.Play();
    }

    public void FadeOut(float duration, AnimationCurve curve = null)
    {
        if (audioSource == null || !AnyPlaying())
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(crossfade ? FadeOutCrossfadeRoutine(duration, curve) : FadeOutRoutine(duration, curve));
    }

    private IEnumerator FadeOutCrossfadeRoutine(float duration, AnimationCurve curve)
    {
        float start = master;
        if (duration > 0f)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float curveProgress = curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
                master = Mathf.Lerp(start, 0f, curveProgress);
                yield return null;
            }
        }

        master = 0f;
        audioSource.Stop();
        voiceB.Stop();
        fadeRoutine = null;
    }

    private IEnumerator FadeOutRoutine(float duration, AnimationCurve curve)
    {
        float startVolume = audioSource.volume;

        if (duration <= 0f)
        {
            audioSource.volume = 0f;
            audioSource.Stop();
            fadeRoutine = null;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float curveProgress = curve != null && curve.length > 0
                ? curve.Evaluate(progress)
                : progress;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, curveProgress);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        fadeRoutine = null;
    }
}
