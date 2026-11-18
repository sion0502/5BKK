using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 컷씬용 여닫이 문 (Cutscene Blend 트랙 대상). 문짝의 경첩 위치가 피벗인 오브젝트에 붙입니다.
/// Blend 0 = 열림(openAngle만큼 Y 회전), 1 = 닫힘(배치된 원래 회전). 중간은 closeCurve로 매핑 —
/// 기본 곡선은 앞 3/4 동안 천천히 끼익 밀리다가 마지막에 쾅 닫힘(클립 하나에 Ease In만 주면 됨).
/// 플레이 중에만 사운드: 움직이기 시작할 때 creakClip, 다 닫힐 때 slamClip (비어 있으면 무음 — 사운드는 팀이 나중에 넣음).
/// </summary>
public class CutsceneDoorSwing : CutsceneBlendTarget
{
    [SerializeField] private float openAngle = 95f;
    [SerializeField, Range(0f, 1f)] private float blend;
    [Tooltip("x = Blend, y = 닫힌 정도(0 열림 ~ 1 닫힘)")]
    [SerializeField] private AnimationCurve closeCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.25f),
        new Keyframe(0.75f, 0.22f, 0.35f, 0.35f),
        new Keyframe(1f, 1f, 7f, 0f));

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip creakClip;
    [SerializeField] private AudioClip slamClip;
    [Tooltip("쾅 소리 볼륨 배율 (살짝 키움)")]
    [SerializeField] private float slamVolume = 1.2f;
    [Tooltip("문이 이만큼 닫혔을 때(0 열림 ~ 1 닫힘) 쾅 소리 시작 — 작을수록 소리가 빨라짐. 문이 닫히려는 순간에 맞춤")]
    [SerializeField, Range(0.05f, 0.95f)] private float slamTriggerClosed = 0.6f;

    [Header("Wind (문이 닫히기 전까지 바깥 바람 — 비워 두면 없음)")]
    [Tooltip("건물에 막 들어온 상태라 처음엔 아주 작게 들리다가, 문이 닫히기 직전까지 서서히 세지고, 문이 쾅 닫히는 순간 끊깁니다")]
    [SerializeField] private AudioClip windClip;
    [SerializeField, Range(0f, 1f)] private float windQuietVolume = 0.04f;
    [SerializeField, Range(0f, 1f)] private float windPeakVolume = 0.3f;
    [Tooltip("컷씬(Timeline) 시간 기준: 이 시각부터 바람이 세지기 시작")]
    [SerializeField] private float windRampStartTime = 5f;
    [Tooltip("이 시각에 가장 세짐 (문이 닫히려는 시각 근처)")]
    [SerializeField] private float windRampEndTime = 14.4f;
    [SerializeField] private float windStopFadeSeconds = 0.25f;

    [SerializeField, HideInInspector] private Quaternion closedLocalRotation = Quaternion.identity;
    [SerializeField, HideInInspector] private bool closedCaptured;

    private float lastClosed = -1f;
    private AudioSource windSource;
    private PlayableDirector windDirector;
    private bool windStopping;
    private float windFadeFrom;

    public override float Blend
    {
        get => blend;
        set
        {
            blend = Mathf.Clamp01(value);
            Apply();
        }
    }

    /// <summary>지금 회전을 "닫힘"으로 기록 (빌더가 배치 직후 호출).</summary>
    public void CaptureClosedRotation()
    {
        closedLocalRotation = transform.localRotation;
        closedCaptured = true;
    }

    private void Awake()
    {
        if (!closedCaptured)
        {
            CaptureClosedRotation();
        }

        Apply();
        StartWind();
    }

    private void Update()
    {
        UpdateWind();
    }

    private void StartWind()
    {
        if (!Application.isPlaying || windClip == null)
        {
            return;
        }

        windSource = gameObject.AddComponent<AudioSource>();
        windSource.clip = windClip;
        windSource.loop = true;
        windSource.playOnAwake = false;
        windSource.spatialBlend = 0f;
        windSource.volume = windQuietVolume;
        windSource.Play();
    }

    private void UpdateWind()
    {
        if (windSource == null)
        {
            return;
        }

        if (windStopping)
        {
            windSource.volume = Mathf.MoveTowards(windSource.volume, 0f, windFadeFrom * Time.unscaledDeltaTime / Mathf.Max(0.01f, windStopFadeSeconds));
            if (windSource.volume <= 0f)
            {
                windSource.Stop();
                Destroy(windSource);
                windSource = null;
            }

            return;
        }

        if (windDirector == null)
        {
            windDirector = FindFirstObjectByType<PlayableDirector>();
        }

        float now = windDirector != null ? (float)windDirector.time : Time.timeSinceLevelLoad;
        float t = Mathf.InverseLerp(windRampStartTime, Mathf.Max(windRampStartTime + 0.01f, windRampEndTime), now);
        windSource.volume = Mathf.Lerp(windQuietVolume, windPeakVolume, Mathf.SmoothStep(0f, 1f, t));
    }

    private void Apply()
    {
        if (!closedCaptured)
        {
            return;
        }

        float closed = Mathf.Clamp01(closeCurve.Evaluate(blend));
        transform.localRotation = closedLocalRotation * Quaternion.Euler(0f, openAngle * (1f - closed), 0f);

        if (Application.isPlaying)
        {
            StopWindIfClosing(closed);
            PlaySounds(closed);
        }

        lastClosed = closed;
    }

    // 문이 쾅 닫히려는 순간 바람 끊김
    private void StopWindIfClosing(float closed)
    {
        if (windSource != null && !windStopping && lastClosed < slamTriggerClosed && closed >= slamTriggerClosed)
        {
            windStopping = true;
            windFadeFrom = Mathf.Max(0.01f, windSource.volume);
        }
    }

    private void PlaySounds(float closed)
    {
        if (audioSource == null || lastClosed < 0f)
        {
            return;
        }

        if (creakClip != null && lastClosed <= 0.01f && closed > 0.01f)
        {
            audioSource.PlayOneShot(creakClip);
        }

        // 문이 서서히 밀리다가 쾅 닫히려는 순간에 소리
        if (slamClip != null && lastClosed < slamTriggerClosed && closed >= slamTriggerClosed)
        {
            audioSource.PlayOneShot(slamClip, slamVolume);
        }
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        driver.AddFromName<CutsceneDoorSwing>(gameObject, "blend");
        driver.AddFromName<Transform>(gameObject, "m_LocalRotation");
    }
}
