using UnityEngine;
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

    [SerializeField, HideInInspector] private Quaternion closedLocalRotation = Quaternion.identity;
    [SerializeField, HideInInspector] private bool closedCaptured;

    private float lastClosed = -1f;

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
            PlaySounds(closed);
        }

        lastClosed = closed;
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

        if (slamClip != null && lastClosed < 0.99f && closed >= 0.99f)
        {
            audioSource.PlayOneShot(slamClip);
        }
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        driver.AddFromName<CutsceneDoorSwing>(gameObject, "blend");
        driver.AddFromName<Transform>(gameObject, "m_LocalRotation");
    }
}
