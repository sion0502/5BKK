using UnityEngine;

/// <summary>
/// 프롤로그 병원 BGM (EnterTotheHospital).
///  · 캠코더로 형체를 본 뒤 "…방금, 뭐였지?" 대사가 나오는 순간부터 재생 시작 (병원에 들어서자마자 틀지 않음).
///  · 곡 앞부분을 한 번 들려준 뒤에는 처음으로 되돌아가지 않고, 가운데 '비슷한 멜로디' 구간(loopStart~loopEnd)만
///    끝과 처음을 서로 겹쳐(crossfade) 자연스럽게 계속 반복.
///  · 숨었다 나와 "…지나갔어. …지금이야, 나가자." 대사가 나오는 순간(비상구 문이 열리게 되는 때) 서서히 꺼짐.
///    (비상구 컷씬이 시작돼도 꺼짐)
/// PrologueDirector와 같은 오브젝트에 붙입니다(컷씬이 끝나고 조작을 넘길 때 켜짐).
/// </summary>
public class HospitalMusic : MonoBehaviour
{
    [SerializeField] private PrologueFinaleCutscene finale;
    [Tooltip("캠코더 형체 — Seen 후 \"방금, 뭐였지?\" 대사 타이밍에 시작")]
    [SerializeField] private CamcorderOnlyApparition apparition;
    [Tooltip("비상구 문 — 열 수 있게 되는 순간(= \"지금이야, 나가자\" 대사)에 꺼짐")]
    [SerializeField] private PrologueFinaleDoor finaleDoor;
    [Tooltip("Seen 이후 대사까지 기다리는 시간 (PrologueDirector의 0.8초와 같게)")]
    [SerializeField] private float startDelayAfterSeen = 0.8f;
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.3f;
    [SerializeField] private float fadeInSeconds = 1.5f;

    [Header("Loop (곡 안의 초 단위)")]
    [Tooltip("반복 구간 시작 — 곡이 충분히 올라온 뒤 멜로디가 일정한 지점")]
    [SerializeField] private float loopStart = 16f;
    [Tooltip("반복 구간 끝 — 곡이 잦아들기 시작하기 전 지점 (0이면 곡 끝)")]
    [SerializeField] private float loopEnd = 56f;
    [Tooltip("끝과 처음을 겹치는 시간")]
    [SerializeField] private float crossfadeSeconds = 5f;

    [Header("Stop")]
    [SerializeField] private float stopFadeSeconds = 0.8f;

    private AudioSource voiceA;
    private AudioSource voiceB;
    private float entryA;
    private float entryB;
    private float master;
    private bool stopping;
    private bool started;
    private bool seen;
    private float seenAt;
    private System.Reflection.FieldInfo armedField;

    private void Start()
    {
        if (clip == null)
        {
            enabled = false;
            return;
        }

        voiceA = CreateVoice("HospitalMusic_A");
        voiceB = CreateVoice("HospitalMusic_B");
        armedField = typeof(PrologueFinaleDoor).GetField("armed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (apparition != null)
        {
            apparition.Seen += OnApparitionSeen;
        }
        else
        {
            StartPlaying(); // 형체 참조가 없으면 예전처럼 바로 시작
        }
    }

    private void OnDestroy()
    {
        if (apparition != null)
        {
            apparition.Seen -= OnApparitionSeen;
        }
    }

    private void OnApparitionSeen()
    {
        seen = true;
        seenAt = Time.time;
    }

    private void StartPlaying()
    {
        started = true;
        Begin(voiceA, 0f);
    }

    private bool FinaleDoorArmed()
    {
        return finaleDoor != null && armedField != null && (bool)armedField.GetValue(finaleDoor);
    }

    private AudioSource CreateVoice(string voiceName)
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

    private void Begin(AudioSource voice, float entry)
    {
        voice.time = Mathf.Clamp(entry, 0f, Mathf.Max(0f, clip.length - 0.1f));
        voice.volume = 0f;
        voice.Play();
        if (voice == voiceA)
        {
            entryA = entry;
        }
        else
        {
            entryB = entry;
        }
    }

    private void Update()
    {
        if (!started)
        {
            // "…방금, 뭐였지?" 대사 순간부터 시작
            if (seen && Time.time - seenAt >= startDelayAfterSeen)
            {
                StartPlaying();
            }

            return;
        }

        if (!stopping && ((finale != null && finale.IsRunning) || FinaleDoorArmed()))
        {
            stopping = true; // "지금이야, 나가자" 대사(또는 마지막 문 컷씬) — 병원 BGM 종료
        }

        float rate = stopping ? 1f / Mathf.Max(0.01f, stopFadeSeconds) : 1f / Mathf.Max(0.01f, fadeInSeconds);
        master = Mathf.MoveTowards(master, stopping ? 0f : 1f, Time.unscaledDeltaTime * rate);

        UpdateVoice(voiceA, voiceB, entryA);
        UpdateVoice(voiceB, voiceA, entryB);

        if (stopping && master <= 0f)
        {
            voiceA.Stop();
            voiceB.Stop();
            enabled = false;
        }
    }

    private void UpdateVoice(AudioSource voice, AudioSource other, float entry)
    {
        if (!voice.isPlaying)
        {
            return;
        }

        float end = loopEnd > loopStart + 1f ? Mathf.Min(loopEnd, clip.length) : clip.length;
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
            Begin(other, loopStart);
        }

        float fadeIn = entry > 0f ? Mathf.Sin(Mathf.Clamp01((t - entry) / fade) * Mathf.PI * 0.5f) : 1f;
        float fadeOut = t >= end - fade ? Mathf.Cos(Mathf.Clamp01((t - (end - fade)) / fade) * Mathf.PI * 0.5f) : 1f;
        voice.volume = volume * master * fadeIn * fadeOut;
    }
}
