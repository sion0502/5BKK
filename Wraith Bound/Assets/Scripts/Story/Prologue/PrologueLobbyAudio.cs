using UnityEngine;

/// <summary>
/// 프롤로그 로비 형광등 소리 (사운드 전용).
///  · 병원에 들어서 조작이 시작되는 순간(이 오브젝트가 켜질 때)부터 light buzzling을 계속 틈.
///    곡 앞부분 한 번 후에는 처음으로 돌아가지 않고, 일정한 가운데 구간(loopStart~loopEnd)만 끝/처음을 겹쳐 반복.
///  · 손전등을 줍는 순간(월드 손전등이 사라질 때) 바로
///    buzzling을 바로 끄고 Flourescent를 같은 순간에 재생.
///  · 마지막 문 컷씬이 시작돼도 buzzling은 꺼짐.
/// PrologueDirector와 같은 오브젝트에 붙입니다(HospitalMusic과 같은 시점에 시작).
/// </summary>
public class PrologueLobbyAudio : MonoBehaviour
{
    [SerializeField] private FlickeringLightGroup lobbyLights;
    [SerializeField] private PrologueFinaleCutscene finale;
    [Tooltip("접수대 위 월드 손전등 — 이게 사라지는(주운) 순간 바로 Flourescent 재생 (PrologueLobbyPowerFail과 같은 오브젝트)")]
    [SerializeField] private GameObject flashlightPickup;

    [Header("Buzz (light buzzling)")]
    [SerializeField] private AudioClip buzzClip;
    [SerializeField, Range(0f, 1f)] private float buzzVolume = 1f;
    [SerializeField] private float buzzFadeInSeconds = 1f;
    [Tooltip("처음 재생 위치 (곡 맨 앞의 튀는 소리를 건너뜀)")]
    [SerializeField] private float buzzStartTime = 1f;
    [Tooltip("반복 구간 (곡 안의 초) — 소리가 일정한 가운데 부분")]
    [SerializeField] private float loopStart = 12f;
    [SerializeField] private float loopEnd = 100f;
    [SerializeField] private float crossfadeSeconds = 4f;
    [Tooltip("불이 꺼지기 시작할 때 buzzling이 꺼지는 시간")]
    [SerializeField] private float cutFadeSeconds = 0.05f;

    [Header("Power fail (Flourescent)")]
    [SerializeField] private AudioClip failClip;
    [SerializeField, Range(0f, 1f)] private float failVolume = 1f;
    [Tooltip("재생 시작 위치 — 클립 앞의 짧은 무음을 건너뛰어 '퍽' 소리가 바로 나게")]
    [SerializeField] private float failStartTime = 0.08f;

    private AudioSource voiceA;
    private AudioSource voiceB;
    private AudioSource failSource;
    private float entryA;
    private float entryB;
    private float master;
    private bool stopping;
    private float stopSeconds;
    private bool started;
    private bool wasOn;
    private bool failPlayed;
    private bool flashlightAssigned;

    private void Start()
    {
        wasOn = lobbyLights == null || lobbyLights.IsOn;
        flashlightAssigned = flashlightPickup != null;
        if (buzzClip != null)
        {
            voiceA = CreateVoice("LobbyBuzz_A", buzzClip);
            voiceB = CreateVoice("LobbyBuzz_B", buzzClip);
            Begin(voiceA, buzzStartTime, false);
            started = true;
        }

        if (failClip != null)
        {
            failSource = CreateVoice("LobbyPowerFail", failClip);
            failSource.volume = 1f;
        }
    }

    private AudioSource CreateVoice(string voiceName, AudioClip clip)
    {
        GameObject go = new GameObject(voiceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private void Begin(AudioSource voice, float entry, bool crossfaded)
    {
        voice.time = Mathf.Clamp(entry, 0f, Mathf.Max(0f, buzzClip.length - 0.1f));
        voice.volume = 0f;
        voice.Play();
        float marker = crossfaded ? entry : 0f; // 0이면 크로스페이드 페이드인 없이 master로만 올라감
        if (voice == voiceA)
        {
            entryA = marker;
        }
        else
        {
            entryB = marker;
        }
    }

    private void Update()
    {
        // 손전등을 줍는 순간(월드 손전등이 사라짐) 바로 — buzzling 컷 + Flourescent. (불 깜빡임은 0.5초 뒤 시작하지만 소리는 줍자마자)
        if (!failPlayed)
        {
            bool pickedUp = false;
            if (flashlightPickup == null && flashlightAssigned)
            {
                pickedUp = true;
            }
            else if (flashlightPickup != null && !flashlightPickup.activeInHierarchy)
            {
                pickedUp = true;
            }

            bool lightsFailing = lobbyLights != null && wasOn && !lobbyLights.IsOn; // 예비: 불이 꺼지기 시작하면
            if (lobbyLights != null)
            {
                wasOn = lobbyLights.IsOn;
            }

            if (pickedUp || lightsFailing)
            {
                failPlayed = true;
                BeginStop(cutFadeSeconds);
                PlayFail();
            }
        }

        if (!started)
        {
            return;
        }

        if (!stopping && finale != null && finale.IsRunning)
        {
            BeginStop(0.8f);
        }

        float rate = stopping ? 1f / Mathf.Max(0.01f, stopSeconds) : 1f / Mathf.Max(0.01f, buzzFadeInSeconds);
        master = Mathf.MoveTowards(master, stopping ? 0f : 1f, Time.unscaledDeltaTime * rate);

        UpdateVoice(voiceA, voiceB, entryA);
        UpdateVoice(voiceB, voiceA, entryB);

        if (stopping && master <= 0f)
        {
            voiceA.Stop();
            voiceB.Stop();
            started = false;
        }
    }

    private void BeginStop(float seconds)
    {
        if (stopping)
        {
            return;
        }

        stopping = true;
        stopSeconds = seconds;
    }

    private void PlayFail()
    {
        if (failSource == null)
        {
            return;
        }

        failSource.time = Mathf.Clamp(failStartTime, 0f, Mathf.Max(0f, failClip.length - 0.1f));
        failSource.volume = failVolume;
        failSource.Play();
    }

    private void UpdateVoice(AudioSource voice, AudioSource other, float entry)
    {
        if (voice == null || !voice.isPlaying)
        {
            return;
        }

        float end = loopEnd > loopStart + 1f ? Mathf.Min(loopEnd, buzzClip.length) : buzzClip.length;
        float fade = Mathf.Clamp(crossfadeSeconds, 0.05f, (end - loopStart) * 0.5f);
        float t = voice.time;

        if (t >= end)
        {
            voice.Stop();
            return;
        }

        // 반복 구간 끝에 다가가면 반대쪽이 구간 처음부터 들어옴 (정지 중이면 새로 시작하지 않음)
        if (!stopping && t >= end - fade && !other.isPlaying)
        {
            Begin(other, loopStart, true);
        }

        float fadeIn = entry > 0f ? Mathf.Sin(Mathf.Clamp01((t - entry) / fade) * Mathf.PI * 0.5f) : 1f;
        float fadeOut = t >= end - fade ? Mathf.Cos(Mathf.Clamp01((t - (end - fade)) / fade) * Mathf.PI * 0.5f) : 1f;
        voice.volume = buzzVolume * master * fadeIn * fadeOut;
    }
}
