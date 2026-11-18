using System.Collections;
using UnityEngine;

/// <summary>
/// 프롤로그에서 플레이어가 숨은 동안 간호사가 지나갈 때, 간호사 자신에게서 나는 목소리 (사운드 전용).
///  · NurseGhost_1~4 중 매번 무작위로 하나를 골라 재생 (순서 없음, 같은 것이 연속으로 나올 수도 있음)
///  · 한 소리가 끝나면 무작위 간격을 두고 다음 소리를 무작위로 재생
///  · 간호사 오브젝트에 붙은 3D AudioSource에서 나므로 플레이어에게는 간호사 위치에서 들림
///  · 간호사가 사라지거나(비활성) 플레이어가 숨은 상태가 아니면 멈춤. 연출/AI 로직은 읽기만 함.
/// 간호사(PrologueNurse)와 같은 오브젝트에 붙입니다.
/// </summary>
[RequireComponent(typeof(PrologueNurse))]
public class NurseGhostHideVoice : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips = { };
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("간호사가 나타난 뒤 첫 소리까지 (최소, 최대 초)")]
    [SerializeField] private Vector2 firstDelay = new Vector2(0.3f, 1.5f);
    [Tooltip("소리가 끝난 뒤 다음 소리까지 쉬는 시간 (최소, 최대 초)")]
    [SerializeField] private Vector2 gapSeconds = new Vector2(0.4f, 3.5f);
    [Tooltip("이 거리 안에서는 최대 볼륨")]
    [SerializeField] private float minDistance = 1.2f;
    [Tooltip("이 거리 밖에서는 안 들림")]
    [SerializeField] private float maxDistance = 18f;

    [Header("벽 너머 소리 (Low Pass + 볼륨)")]
    [Tooltip("간호사와 플레이어 사이에 벽/문 같은 것이 있을 때 고음이 깎이는 정도(Hz) — 낮을수록 먹먹")]
    [SerializeField] private float occludedCutoff = 900f;
    [Tooltip("가려졌을 때 볼륨 배율")]
    [SerializeField, Range(0f, 1f)] private float occludedVolume = 0.55f;
    [Tooltip("플레이어 주변 이 거리 안의 물체(내가 숨은 캐비닛 등)는 '벽'으로 치지 않음")]
    [SerializeField] private float ignoreNearListener = 1.5f;

    [Header("공간 잔향 (Reverb)")]
    [SerializeField] private AudioReverbPreset reverbPreset = AudioReverbPreset.Hallway;
    [Tooltip("가까울 때 / 멀 때 잔향 양 (멀수록 잔향이 많아져 공간이 울리는 느낌)")]
    [SerializeField, Range(0f, 1.1f)] private float reverbNear = 0.25f;
    [SerializeField, Range(0f, 1.1f)] private float reverbFar = 0.9f;

    private AudioSource source;
    private AudioReverbFilter reverb;
    private float occlusion; // 0 = 그대로, 1 = 완전히 가려짐 (부드럽게 변함)
    private AudioLowPassFilter lowPass;
    private Transform listener;
    private PrologueNurse nurse;
    private PlayerHidingController hiding;

    private void Awake()
    {
        nurse = GetComponent<PrologueNurse>();
        // 간호사 머리 위치(눈높이)에 붙여 — 간호사가 움직이면 소리 위치도 함께 움직이고, 발 밑이 아니라 입 쪽에서 나는 소리가 됨
        GameObject voice = new GameObject("NurseGhostHideVoice_Source");
        voice.transform.SetParent(nurse.EyePoint, false);
        voice.transform.localPosition = Vector3.zero;
        source = voice.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;   // 완전한 3D — 간호사 위치에서 나는 소리
        source.spread = 0f;         // 한 점에서 나는 소리 (좌우로 퍼지지 않음)
        source.panStereo = 0f;
        source.dopplerLevel = 0f;
        // 가까워질수록 부드럽게 커지고, 멀어질수록 부드럽게 작아지는 거리 곡선 (0 = 소리 나는 곳, 1 = maxDistance)
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -1.2f),
            new Keyframe(0.25f, 0.62f, -1.4f, -1.4f),
            new Keyframe(0.55f, 0.2f, -0.8f, -0.8f),
            new Keyframe(1f, 0f, -0.2f, 0f)));

        // 멀리 있을 땐 먹먹하게(고음이 깎임), 가까이 올수록 또렷하게
        lowPass = voice.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;

        // 공간 잔향 — 이 소리에만 적용되는 잔향 (복도/병실 울림). 가까우면 건조하고 멀수록 울림이 커짐
        reverb = voice.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = reverbPreset;
        source.reverbZoneMix = 1f;
    }

    private void Update()
    {
        if (lowPass == null || source == null || !source.isPlaying)
        {
            return;
        }

        if (listener == null)
        {
            AudioListener al = FindFirstObjectByType<AudioListener>();
            listener = al != null ? al.transform : null;
            if (listener == null)
            {
                return;
            }
        }

        float d = Vector3.Distance(listener.position, source.transform.position);
        float k = Mathf.Clamp01((d - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));

        // 벽 너머 판정: 리스너 → 소리 위치 사이에 막는 물체가 있으면 가려진 것으로 봄
        float blocked = IsOccluded(d) ? 1f : 0f;
        occlusion = Mathf.Lerp(occlusion, blocked, 1f - Mathf.Exp(-6f * Time.deltaTime));

        float distCutoff = Mathf.Lerp(22000f, 1500f, k);
        float cutoff = Mathf.Lerp(distCutoff, Mathf.Min(distCutoff, occludedCutoff), occlusion);
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, cutoff, 1f - Mathf.Exp(-8f * Time.deltaTime));
        source.volume = volume * Mathf.Lerp(1f, occludedVolume, occlusion);

        // 멀수록 잔향이 많아져 소리가 공간에 울리며 멀리서 오는 느낌
        if (reverb != null)
        {
            float wet = Mathf.Lerp(reverbNear, reverbFar, k);
            reverb.dryLevel = 0f;
            reverb.room = Mathf.Lerp(-3000f, -300f, wet);
            reverb.reverbLevel = Mathf.Lerp(-3000f, 0f, wet);
        }
    }

    private readonly RaycastHit[] hits = new RaycastHit[16];

    private bool IsOccluded(float distance)
    {
        Vector3 from = listener.position;
        Vector3 to = source.transform.position;
        Vector3 dir = to - from;
        if (distance < ignoreNearListener + 0.1f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(from, dir / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
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

    private void OnEnable()
    {
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }
    }

    private bool PlayerHidden
    {
        get
        {
            if (hiding == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                hiding = player != null ? player.GetComponent<PlayerHidingController>() : null;
            }

            return hiding != null && hiding.isHiding;
        }
    }

    private bool ShouldVoice => PlayerHidden && !nurse.IsChasing;

    private IEnumerator Run()
    {
        if (clips == null || clips.Length == 0)
        {
            yield break;
        }

        // 숨은 상태가 될 때까지 대기 (간호사가 나타나는 건 숨은 뒤라 보통 바로 통과)
        while (!ShouldVoice)
        {
            yield return null;
        }

        yield return WaitSeconds(Random.Range(firstDelay.x, firstDelay.y));

        while (ShouldVoice)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                source.clip = clip;
                source.volume = volume;
                source.Play();
                while (source.isPlaying && ShouldVoice)
                {
                    yield return null;
                }

                if (!ShouldVoice)
                {
                    source.Stop();
                    yield break;
                }
            }

            yield return WaitSeconds(Random.Range(gapSeconds.x, gapSeconds.y));
        }

        source.Stop();
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            yield return null;
        }
    }
}
