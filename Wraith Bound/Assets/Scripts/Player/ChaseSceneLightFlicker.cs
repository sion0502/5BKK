using UnityEngine;

/// <summary>
/// 추적 중 씬의 Point/Spot 라이트를 중앙에서 점멸합니다.
/// 플레이어 장비(손전등·캠코더) 하위 Light와 FlickeringLamp 로컬 제어는 제외/일시 정지합니다.
/// </summary>
public class ChaseSceneLightFlicker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerChaseEffectController chaseEffects;
    [SerializeField] private Transform playerRoot;

    [Header("Light Collection")]
    [SerializeField] private bool autoCollectOnChaseStart = true;
    [SerializeField] private Light[] manualLights;
    [SerializeField] private float maxLightRange = 50f;
    [Tooltip("0이면 range만으로 필터. Directional 등 제외용")]
    [SerializeField] private float maxDirectionalIntensity = 2.5f;

    [Header("Per-Light Flicker")]
    [SerializeField] private float lightPhaseSpread = 2.5f;
    [SerializeField] private float lightIntensitySmoothing = 10f;
    [Tooltip("strobe 블렌드가 이 값 이상일 때만 완전 소등")]
    [SerializeField] private float hardBlackoutBlendMin = 0.45f;
    [SerializeField] private float perLightBlackoutChance = 0.22f;
    [SerializeField] private Vector2 perLightBlackoutDuration = new Vector2(0.06f, 0.14f);

    private Light[] cachedLights;
    private float[] originalIntensities;
    private bool[] originalEnabled;
    private float[] lightPhaseOffsets;
    private float[] perLightBlackoutTimers;
    private FlickeringLamp[] pausedFlickeringLamps;
    private bool[] flickeringLampWasEnabled;

    private bool chaseActive;

    public bool IsChaseActive => chaseActive;

    void Awake()
    {
        if (chaseEffects == null)
        {
            chaseEffects = GetComponent<PlayerChaseEffectController>();
        }

        if (playerRoot == null)
        {
            playerRoot = transform;
        }
    }

    public void SetChaseActive(bool active)
    {
        if (chaseActive == active)
        {
            return;
        }

        chaseActive = active;

        if (chaseActive)
        {
            BeginChase();
        }
        else
        {
            EndChase();
        }
    }

    void Update()
    {
        if (!chaseActive || chaseEffects == null || cachedLights == null)
        {
            return;
        }

        ApplyPerLightFlicker();
    }

    void OnDisable()
    {
        EndChase();
    }

    private void BeginChase()
    {
        if (autoCollectOnChaseStart)
        {
            CollectSceneLights();
        }
        else
        {
            UseManualLights();
        }

        PauseFlickeringLamps();
        CacheOriginalLightState();
    }

    private void EndChase()
    {
        RestoreLights();
        ResumeFlickeringLamps();
        cachedLights = null;
        originalIntensities = null;
        originalEnabled = null;
        lightPhaseOffsets = null;
        perLightBlackoutTimers = null;
        chaseActive = false;
    }

    private void CollectSceneLights()
    {
        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<Light>(allLights.Length);

        for (int i = 0; i < allLights.Length; i++)
        {
            Light light = allLights[i];
            if (!IsValidChaseLight(light))
            {
                continue;
            }

            list.Add(light);
        }

        cachedLights = list.ToArray();
    }

    private void UseManualLights()
    {
        if (manualLights == null || manualLights.Length == 0)
        {
            cachedLights = System.Array.Empty<Light>();
            return;
        }

        var list = new System.Collections.Generic.List<Light>();
        for (int i = 0; i < manualLights.Length; i++)
        {
            Light light = manualLights[i];
            if (light != null && IsValidChaseLight(light))
            {
                list.Add(light);
            }
        }

        cachedLights = list.ToArray();
    }

    private bool IsValidChaseLight(Light light)
    {
        if (light == null || !light.enabled)
        {
            return false;
        }

        if (playerRoot != null && light.transform.IsChildOf(playerRoot))
        {
            return false;
        }

        if (light.type == LightType.Directional && light.intensity > maxDirectionalIntensity)
        {
            return false;
        }

        if (light.type != LightType.Directional && light.range > maxLightRange)
        {
            return false;
        }

        return true;
    }

    private void CacheOriginalLightState()
    {
        if (cachedLights == null)
        {
            return;
        }

        int count = cachedLights.Length;
        originalIntensities = new float[count];
        originalEnabled = new bool[count];
        lightPhaseOffsets = new float[count];
        perLightBlackoutTimers = new float[count];

        for (int i = 0; i < count; i++)
        {
            Light light = cachedLights[i];
            if (light == null)
            {
                continue;
            }

            // FlickeringLamp과 같이 NaN/Infinity intensity는 캐시 단계에서 걸러둠
            originalIntensities[i] = SanitizeIntensity(light.intensity, 1f);
            originalEnabled[i] = light.enabled;
            if (!IsValidIntensity(light.intensity))
            {
                light.intensity = originalIntensities[i];
            }
            lightPhaseOffsets[i] = SampleLightPhaseOffset(light, i);
            perLightBlackoutTimers[i] = 0f;
        }
    }

    private float SampleLightPhaseOffset(Light light, int index)
    {
        float spread = Mathf.Max(0.01f, lightPhaseSpread);
        int instanceHash = light != null ? light.GetInstanceID() : index;
        float deterministic = (instanceHash & 0xFFFF) / 65535f * spread;
        float random = Random.Range(0f, spread);
        return (deterministic + random) * 0.5f;
    }

    private void ApplyPerLightFlicker()
    {
        if (cachedLights == null || originalIntensities == null || lightPhaseOffsets == null)
        {
            return;
        }

        float hardThreshold = chaseEffects.HardBlackoutThreshold;
        float strobeBlend = chaseEffects.FlickerStrobeBlend;
        bool allowHardBlackout = strobeBlend >= hardBlackoutBlendMin;

        for (int i = 0; i < cachedLights.Length; i++)
        {
            Light light = cachedLights[i];
            if (light == null || !originalEnabled[i])
            {
                continue;
            }

            UpdatePerLightBlackout(i, strobeBlend);

            if (allowHardBlackout && perLightBlackoutTimers[i] > 0f)
            {
                light.enabled = false;
                continue;
            }

            float multiplier = SanitizeMultiplier(
                chaseEffects.GetFlickerMultiplier(lightPhaseOffsets[i]),
                1f
            );
            if (allowHardBlackout && multiplier <= hardThreshold)
            {
                light.enabled = false;
                continue;
            }

            float baseIntensity = SanitizeIntensity(originalIntensities[i], 1f);
            if (!IsValidIntensity(originalIntensities[i]))
            {
                originalIntensities[i] = baseIntensity;
            }

            float targetIntensity = baseIntensity * multiplier;
            light.enabled = true;
            light.intensity = SmoothLightIntensity(light.intensity, targetIntensity);
        }
    }

    private float SmoothLightIntensity(float current, float target)
    {
        float smoothing = Mathf.Max(0.01f, lightIntensitySmoothing);
        float blend = Mathf.Clamp01(Time.deltaTime * smoothing);
        float safeCurrent = SanitizeIntensity(current, target);
        float safeTarget = SanitizeIntensity(target, safeCurrent);
        return SanitizeIntensity(Mathf.Lerp(safeCurrent, safeTarget, blend), safeTarget);
    }

    private static bool IsValidIntensity(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    private static float SanitizeIntensity(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return Mathf.Max(fallback, 0f);
        }

        return value;
    }

    private static float SanitizeMultiplier(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return fallback;
        }

        return Mathf.Max(value, 0f);
    }

    private void UpdatePerLightBlackout(int index, float strobeBlend)
    {
        if (perLightBlackoutTimers == null)
        {
            return;
        }

        if (perLightBlackoutTimers[index] > 0f)
        {
            perLightBlackoutTimers[index] -= Time.deltaTime;
            return;
        }

        if (strobeBlend < hardBlackoutBlendMin)
        {
            return;
        }

        float chance = perLightBlackoutChance * Mathf.Lerp(0.35f, 1f, strobeBlend);
        if (Random.value < chance * Time.deltaTime)
        {
            perLightBlackoutTimers[index] = Random.Range(
                perLightBlackoutDuration.x,
                perLightBlackoutDuration.y);
        }
    }

    private void RestoreLights()
    {
        if (cachedLights == null || originalIntensities == null || originalEnabled == null)
        {
            return;
        }

        for (int i = 0; i < cachedLights.Length; i++)
        {
            Light light = cachedLights[i];
            if (light == null)
            {
                continue;
            }

            light.intensity = SanitizeIntensity(originalIntensities[i], 1f);
            light.enabled = originalEnabled[i];
        }
    }

    private void PauseFlickeringLamps()
    {
        FlickeringLamp[] lamps = FindObjectsByType<FlickeringLamp>(FindObjectsSortMode.None);
        pausedFlickeringLamps = lamps;
        flickeringLampWasEnabled = new bool[lamps.Length];

        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null)
            {
                continue;
            }

            flickeringLampWasEnabled[i] = lamps[i].enabled;
            lamps[i].enabled = false;
        }
    }

    private void ResumeFlickeringLamps()
    {
        if (pausedFlickeringLamps == null || flickeringLampWasEnabled == null)
        {
            return;
        }

        for (int i = 0; i < pausedFlickeringLamps.Length; i++)
        {
            FlickeringLamp lamp = pausedFlickeringLamps[i];
            if (lamp == null)
            {
                continue;
            }

            lamp.enabled = flickeringLampWasEnabled[i];
        }

        pausedFlickeringLamps = null;
        flickeringLampWasEnabled = null;
    }
}
