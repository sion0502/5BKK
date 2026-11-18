using UnityEngine;

/// <summary>
/// 3D 소리 공통 규칙 (사운드 전용) — 괴물·귀신·연출 소리를 이 컴포넌트가 붙은 오브젝트(AudioSource)에서 내게 함.
///  1) Audio Listener 기반 거리 감쇠: 가까우면 크게, 멀면 서서히 작게 (부드러운 곡선)
///  2) Audio Low Pass Filter: 멀수록 먹먹, 벽/문 너머면 더 먹먹하고 작게 (리스너↔소리 사이를 Raycast로 판정)
///  3) Reverb: 멀수록 잔향이 많아져 공간이 울리는 느낌
///  4) Spatial Blend / 방향성: 완전한 3D(1), 한 점에서 나는 소리(spread 0), 도플러 끔
/// AudioSource와 같은 오브젝트에 붙이면 Awake에서 자동으로 설정합니다.
/// 주의: 소리 방향(좌우)이 나려면 오디오 클립이 모노여야 합니다(스테레오는 방향이 안 남).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SpatialSoundFx : MonoBehaviour
{
    [Header("거리 감쇠 (Audio Listener 기준)")]
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 30f;

    [Header("벽 너머 소리 (Low Pass + 볼륨)")]
    [SerializeField] private float farCutoff = 1500f;
    [SerializeField] private float occludedCutoff = 900f;
    [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.55f;
    [Tooltip("플레이어 주변 이 거리 안의 물체(숨은 캐비닛 등)는 벽으로 치지 않음")]
    [SerializeField] private float ignoreNearListener = 1.5f;

    [Header("공간 잔향")]
    [SerializeField] private float reverbDecaySeconds = 1.8f;
    [SerializeField, Range(0f, 1.1f)] private float reverbNear = 0.25f;
    [SerializeField, Range(0f, 1.1f)] private float reverbFar = 0.9f;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private AudioReverbFilter reverb;
    private Transform listener;
    private float occlusion;
    private float baseVolume;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        source.spatialBlend = 1f;
        source.spread = 0f;
        source.panStereo = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -1.2f),
            new Keyframe(0.25f, 0.62f, -1.4f, -1.4f),
            new Keyframe(0.55f, 0.2f, -0.8f, -0.8f),
            new Keyframe(1f, 0f, -0.2f, 0f)));

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
        source.reverbZoneMix = 1f;
    }

    private void Update()
    {
        if (!source.isPlaying)
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

        float d = Vector3.Distance(listener.position, transform.position);
        float k = Mathf.Clamp01((d - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));

        occlusion = Mathf.Lerp(occlusion, IsOccluded(d) ? 1f : 0f, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float distCutoff = Mathf.Lerp(22000f, farCutoff, k);
        float cutoff = Mathf.Lerp(distCutoff, Mathf.Min(distCutoff, occludedCutoff), occlusion);
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, cutoff, 1f - Mathf.Exp(-8f * Time.deltaTime));
        source.volume = baseVolume * Mathf.Lerp(1f, occludedVolume, occlusion);

        float wet = Mathf.Lerp(reverbNear, reverbFar, k);
        reverb.room = Mathf.Lerp(-3000f, -300f, wet);
        reverb.reverbLevel = Mathf.Lerp(-3000f, 0f, wet);
    }

    private bool IsOccluded(float distance)
    {
        if (distance < ignoreNearListener + 0.1f)
        {
            return false;
        }

        Vector3 from = listener.position;
        Vector3 dir = (transform.position - from) / distance;
        int count = Physics.RaycastNonAlloc(from, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int n = 0; n < count; n++)
        {
            RaycastHit hit = hits[n];
            Transform t = hit.collider.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(listener.root) || hit.distance < ignoreNearListener)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
