using System.Collections;
using UnityEngine;

/// <summary>
/// 정전된 병원에 희미하게 남아 있는 불빛 한 묶음 (2026-11-17, "처음 시작이 너무 어둡다"는 피드백을 연출로 해결).
/// 켜져 있는 동안 가끔 지직거리고(idleFlicker), <see cref="FailOut"/>로 한동안 깜빡이다 꺼짐.
/// 인트로 = 병원 입구 유리 너머 로비 불빛(켜진 채 유지), 프롤로그 = 같은 로비 불빛(손전등을 주우면 꺼짐, <see cref="PrologueLobbyPowerFail"/>).
/// 램프 모델은 재질 슬롯 <see cref="materialSlot"/>만 켜짐/꺼짐 재질로 바꿈. 소리 슬롯은 비워 둠(사운드는 팀이 나중에 결정).
/// </summary>
public class FlickeringLightGroup : MonoBehaviour
{
    [SerializeField] private Light[] lights = { };
    [SerializeField] private Renderer[] renderers = { };
    [SerializeField] private int materialSlot;
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private bool startOn = true;

    [Header("Idle Flicker")]
    [SerializeField] private bool idleFlicker = true;
    [SerializeField] private Vector2 idleInterval = new Vector2(2.5f, 6f);
    [Tooltip("평소 밝기 흔들림 (0 = 일정)")]
    [SerializeField, Range(0f, 0.5f)] private float hum = 0.08f;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip flickerClip;
    [SerializeField] private AudioClip powerDownClip;

    private float[] baseIntensity;
    private bool lit;
    private bool failing;
    private float nextIdle;
    private float dim = 1f;

    public bool IsOn => lit && !failing;

    private void Awake()
    {
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        SetLit(startOn);
        ScheduleIdle();
    }

    private void Update()
    {
        if (!lit || failing)
        {
            return;
        }

        if (idleFlicker && Time.time >= nextIdle)
        {
            StartCoroutine(IdleBlink());
            ScheduleIdle();
        }

        float wobble = hum > 0f ? 1f - hum * Mathf.PerlinNoise(Time.time * 3.1f, 0.37f) : 1f;
        ApplyIntensity(wobble * dim);
    }

    /// <summary>지직거리며 몇 번 깜빡이다 완전히 꺼짐 (약 1.6초).</summary>
    public IEnumerator FailOut()
    {
        if (!lit || failing)
        {
            yield break;
        }

        failing = true;
        Play(flickerClip);
        float[] pattern = { 0.08f, 0.06f, 0.22f, 0.05f, 0.05f, 0.04f, 0.35f, 0.07f, 0.12f, 0.05f, 0.04f };
        for (int i = 0; i < pattern.Length; i++)
        {
            bool on = i % 2 == 1;
            SetLit(on);
            if (on)
            {
                ApplyIntensity(Random.Range(0.35f, 1f));
            }

            yield return new WaitForSeconds(pattern[i]);
        }

        Play(powerDownClip);
        SetLit(false);
        failing = false;
    }

    public void SetLit(bool on)
    {
        lit = on;
        foreach (Light l in lights)
        {
            if (l != null)
            {
                l.enabled = on;
            }
        }

        Material material = on ? onMaterial : offMaterial;
        if (material == null)
        {
            return;
        }

        foreach (Renderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            Material[] mats = r.sharedMaterials;
            if (materialSlot < mats.Length)
            {
                mats[materialSlot] = material;
                r.sharedMaterials = mats;
            }
        }
    }

    private IEnumerator IdleBlink()
    {
        Play(flickerClip);
        int blinks = Random.Range(1, 4);
        for (int i = 0; i < blinks && lit && !failing; i++)
        {
            dim = Random.Range(0.05f, 0.4f);
            yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            dim = 1f;
            yield return new WaitForSeconds(Random.Range(0.04f, 0.15f));
        }

        dim = 1f;
    }

    private void ApplyIntensity(float factor)
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].intensity = baseIntensity[i] * factor;
            }
        }
    }

    private void ScheduleIdle()
    {
        nextIdle = Time.time + Random.Range(idleInterval.x, idleInterval.y);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
