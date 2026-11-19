using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 소리 공통 규칙 (사운드 전용) — 괴물·귀신·연출 소리를 이 컴포넌트가 붙은 오브젝트의 AudioSource들에서 내게 함.
///  1) Audio Listener 기반 거리 감쇠: 가까우면 크게, 멀면 서서히 작게 (부드러운 곡선)
///  2) Audio Low Pass Filter: 멀수록 먹먹, 벽/문 너머면 더 먹먹하고 작게 (리스너↔소리 사이를 Raycast로 판정)
///  3) Reverb: 멀수록 잔향이 많아져 공간이 울리는 느낌
///  4) Spatial Blend / 방향성: 완전한 3D(1), 한 점에서 나는 소리(spread 0), 도플러 끔
/// 같은 오브젝트에 있는 모든 AudioSource(런타임에 다른 스크립트가 만든 것 포함)에 적용합니다.
/// 주의: 소리 방향(좌우)이 나려면 오디오 클립이 모노여야 합니다(스테레오는 방향이 안 남).
/// </summary>
public class SpatialSoundFx : MonoBehaviour
{
    [Header("거리 감쇠 (Audio Listener 기준)")]
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 30f;
    [Tooltip("멀리서도 작게 들리게 남길 볼륨(0~0.3). 0이면 maxDistance에서 0(기본). 0보다 크면 maxDistance까지 이 값 아래로 안 줄고, " +
             "maxDistance를 넘으면 farFadeDistance 동안 0으로 — 탈춤귀신이 다가오는지 멀리서부터 알 수 있게 (2026-11-20)")]
    [SerializeField, Range(0f, 0.3f)] private float farVolume;
    [SerializeField] private float farFadeDistance = 10f;

    [Header("벽 너머 소리 (Low Pass + 볼륨)")]
    [SerializeField] private float farCutoff = 1500f;
    [SerializeField] private float occludedCutoff = 900f;
    [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.55f;
    [Tooltip("플레이어 주변 이 거리 안의 물체(숨은 캐비닛 등)는 벽으로 치지 않음")]
    [SerializeField] private float ignoreNearListener = 1.5f;
    [Tooltip("벽 판정 때 소리 위치에 더하는 높이(m) — 발밑에서 나는 소리가 바닥에 가리지 않게")]
    [SerializeField] private float occlusionHeight = 0f;

    [Header("공간 잔향")]
    [SerializeField] private float reverbDecaySeconds = 1.8f;
    [SerializeField, Range(0f, 1.1f)] private float reverbNear = 0.25f;
    [SerializeField, Range(0f, 1.1f)] private float reverbFar = 0.9f;

    private class Voice
    {
        public AudioSource source;
        public float baseVolume;
        public float lastWritten;
    }

    private readonly List<Voice> voices = new List<Voice>();
    private AudioLowPassFilter lowPass;
    private AudioReverbFilter reverb;
    private Transform listener;
    private Transform owner; // 이 소리를 내는 생명체의 루트 — 자기 몸의 콜라이더는 '벽'이 아님
    private float occlusion;
    private float volumeFactor = 1f;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        Animator anim = GetComponentInParent<Animator>();
        owner = anim != null ? anim.transform : transform;

        lowPass = gameObject.GetComponent<AudioLowPassFilter>();
        if (lowPass == null)
        {
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        lowPass.cutoffFrequency = 22000f;

        reverb = gameObject.GetComponent<AudioReverbFilter>();
        if (reverb == null)
        {
            reverb = gameObject.AddComponent<AudioReverbFilter>();
        }

        reverb.reverbPreset = AudioReverbPreset.User;
        reverb.decayTime = reverbDecaySeconds;
        reverb.density = 100f;
        reverb.diffusion = 100f;
        reverb.dryLevel = 0f;

        Collect();
    }

    // 다른 스크립트가 Awake에서 소리의 3D 설정을 덮어쓰거나 AudioSource를 늦게 만들어도 Start에서 다시 맞춤
    private void Start()
    {
        Collect();
    }

    private void Collect()
    {
        foreach (AudioSource s in GetComponents<AudioSource>())
        {
            Voice v = voices.Find(x => x.source == s);
            if (v == null)
            {
                v = new Voice { source = s, baseVolume = s.volume, lastWritten = s.volume };
                voices.Add(v);
            }

            Apply(s);
        }
    }

    private void Apply(AudioSource source)
    {
        source.spatialBlend = 1f;
        source.spread = 0f;
        source.panStereo = 0f;
        source.dopplerLevel = 0f;
        source.reverbZoneMix = 1f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        // farVolume만큼 바닥을 올림 (0이면 예전 곡선 그대로)
        float f = farVolume;
        float r = 1f - f;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -1.2f * r),
            new Keyframe(0.25f, f + 0.62f * r, -1.4f * r, -1.4f * r),
            new Keyframe(0.55f, f + 0.2f * r, -0.8f * r, -0.8f * r),
            new Keyframe(1f, f, -0.2f * r, 0f)));
    }

    private void Update()
    {
        bool anyPlaying = false;
        foreach (Voice v in voices)
        {
            if (v.source != null && v.source.isPlaying)
            {
                anyPlaying = true;
                break;
            }
        }

        if (!anyPlaying)
        {
            return;
        }

        if (listener == null)
        {
            AudioListener al = FindFirstObjectByType<AudioListener>();
            if (al == null)
            {
                return;
            }

            listener = al.transform;
        }

        Vector3 pos = transform.position;
        float d = Vector3.Distance(listener.position, pos);
        float k = Mathf.Clamp01((d - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));

        occlusion = Mathf.Lerp(occlusion, IsOccluded(d) ? 1f : 0f, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float distCutoff = Mathf.Lerp(22000f, farCutoff, k);
        float cutoff = Mathf.Lerp(distCutoff, Mathf.Min(distCutoff, occludedCutoff), occlusion);
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, cutoff, 1f - Mathf.Exp(-8f * Time.deltaTime));
        volumeFactor = Mathf.Lerp(1f, occludedVolume, occlusion);
        if (farVolume > 0f && d > maxDistance)
        {
            // 곡선은 maxDistance 밖에서 끝값(farVolume)을 계속 쓰므로, 맵 전체에서 들리지 않게 여기서 줄여 끔
            volumeFactor *= Mathf.Clamp01(1f - (d - maxDistance) / Mathf.Max(0.01f, farFadeDistance));
        }

        float wet = Mathf.Lerp(reverbNear, reverbFar, k);
        reverb.room = Mathf.Lerp(-3000f, -300f, wet);
        reverb.reverbLevel = Mathf.Lerp(-3000f, 0f, wet);
    }

    // 볼륨은 LateUpdate에서 마지막에 곱함 — 다른 스크립트(페이드 등)가 Update에서 정한 볼륨 위에 벽 너머 감쇠만 얹음
    private void LateUpdate()
    {
        foreach (Voice v in voices)
        {
            if (v.source == null)
            {
                continue;
            }

            float cur = v.source.volume;
            if (!Mathf.Approximately(cur, v.lastWritten))
            {
                v.baseVolume = cur; // 다른 스크립트가 바꾼 값을 새 기준으로
            }

            float w = v.baseVolume * volumeFactor;
            v.source.volume = w;
            v.lastWritten = w;
        }
    }

    private bool IsOccluded(float distance)
    {
        if (distance < ignoreNearListener + 0.1f)
        {
            return false;
        }

        Vector3 from = listener.position;
        Vector3 to = transform.position + Vector3.up * occlusionHeight;
        Vector3 diff = to - from;
        float len = diff.magnitude;
        if (len < 0.01f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(from, diff / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
        for (int n = 0; n < count; n++)
        {
            RaycastHit hit = hits[n];
            Transform t = hit.collider.transform;
            if (t.IsChildOf(owner) || t.IsChildOf(transform) || t.IsChildOf(listener.root) || hit.distance < ignoreNearListener)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
