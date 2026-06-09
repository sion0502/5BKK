using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 괴물 발각·추적 화면 연출.
/// Impact: 붉음 + 줌 + 흔들림 / Burst: 노이즈 / Sustain: 노이즈 + 점멸 (붉은 틴트 없음).
/// </summary>
public class PlayerChaseEffectController : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private float volumePriority = 20f;

    [Header("Impact (발각 — 붉음 + 줌 + 흔들림)")]
    [SerializeField] private float impactDuration = 0.55f;
    [SerializeField] private Color impactRedColor = new Color(1f, 0.22f, 0.22f, 1f);
    [SerializeField] private float impactRedVignette = 0.85f;
    [SerializeField] private Color impactVignetteColor = new Color(0.9f, 0f, 0f, 1f);
    [SerializeField] private float impactFovPunch = -12f;
    [SerializeField] private float impactShakeDuration = 0.65f;
    [SerializeField] private float impactShakePos = 0.09f;
    [SerializeField] private float impactShakeRot = 4.5f;
    [SerializeField] private AnimationCurve impactFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Burst (발각 — 노이즈)")]
    [SerializeField] private float burstDuration = 1.5f;
    [SerializeField] private float burstGrainPeak = 1f;
    [SerializeField] private float burstChromaticPeak = 1f;
    [SerializeField] private float burstPostExposurePeak = 1.2f;
    [SerializeField] private float burstVolumeWeight = 1f;
    [SerializeField] private AnimationCurve burstFalloff = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.12f, 1f, 0f, 0f),
        new Keyframe(0.35f, 0.75f, -2f, -2f),
        new Keyframe(0.7f, 0.2f, -1.5f, -1.5f),
        new Keyframe(1f, 0f, 0f, 0f));

    [Header("Sustain (추적 — 노이즈 + 점멸)")]
    [SerializeField] private float sustainFadeIn = 0.25f;
    [SerializeField] private float sustainFadeOut = 0.7f;
    [SerializeField] private float sustainVolumeWeight = 0.75f;
    [SerializeField] private float sustainGrain = 0.52f;
    [SerializeField] private float sustainChromatic = 0.08f;
    [SerializeField] private float flickerMin = 0.15f;
    [SerializeField] private float flickerMax = 1f;
    [Tooltip("추적 점멸 시 exposure 하향만 (평상시보다 밝아지지 않음)")]
    [SerializeField] private float flickerExposureAmp = 0.4f;
    [SerializeField] private float flickerBlackoutChance = 0.45f;
    [SerializeField] private Vector2 flickerBlackoutDuration = new Vector2(0.1f, 0.18f);
    [SerializeField] private float flickerBlackoutMultiplier = 0f;
    [Tooltip("이 값 이하 배율이면 ChaseSceneLightFlicker가 Light를 완전히 끕니다.")]
    [SerializeField] private float hardBlackoutThreshold = 0.1f;

    [Header("Flicker Waveform (Hybrid Blend)")]
    [Tooltip("완만(사인)과 계단(strobe)을 느린 노이즈로 섞습니다.")]
    [SerializeField] private bool useHybridFlicker = true;
    [SerializeField] private float flickerStrobeHz = 3.5f;
    [SerializeField] private float flickerDutyCycle = 0.45f;
    [Tooltip("0=항상 완만, 1=노이즈 최대 시 완전 계단형")]
    [SerializeField] private float flickerBlendWeight = 0.65f;
    [SerializeField] private float flickerBlendSpeed = 0.4f;
    [SerializeField] private float flickerBlendSharpness = 1.4f;
    [SerializeField] private float flickerSpeed = 12f;
    [SerializeField] private float flickerSharpness = 2.2f;

    [Header("Scene Lights")]
    [SerializeField] private ChaseSceneLightFlicker sceneLightFlicker;

    [Header("Debug (Chapter1 테스트)")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private KeyCode debugBurstKey = KeyCode.F6;
    [SerializeField] private KeyCode debugChaseToggleKey = KeyCode.F7;

    private Volume chaseVolume;
    private VolumeProfile chaseProfile;
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private FilmGrain filmGrain;
    private ChromaticAberration chromaticAberration;

    private PlayerHidingController hidingController;
    private CamcorderEnergyController camcorderEnergy;
    private PlayerController playerController;

    private Camera mainCamera;
    private float baseFieldOfView = 60f;
    private Transform shakeTransform;
    private Vector3 lastShakePosOffset;
    private Quaternion lastShakeRotOffset = Quaternion.identity;

    private bool isBeingChased;
    private float sustainWeight;
    private float burstIntensity;
    private float impactIntensity;
    private float shakeTimeRemaining;
    private float flickerNoiseSeed;
    private float flickerBlendSeed;
    private float flickerBlackoutTimer;

    private Coroutine burstRoutine;
    private Coroutine impactRoutine;

    public bool IsBeingChased => isBeingChased;
    public float HardBlackoutThreshold => hardBlackoutThreshold;
    public float FlickerStrobeBlend => GetFlickerStrobeBlend();

    void Awake()
    {
        hidingController = GetComponent<PlayerHidingController>();
        camcorderEnergy = GetComponent<CamcorderEnergyController>();
        playerController = GetComponent<PlayerController>();
        if (sceneLightFlicker == null)
        {
            sceneLightFlicker = GetComponent<ChaseSceneLightFlicker>();
        }

        flickerNoiseSeed = Random.value * 100f;
        flickerBlendSeed = Random.value * 100f + 37f;
        ResolveCameraReferences();
        EnsureMainCameraPostProcessing();
        BuildChaseVolume();
    }

    void Update()
    {
        if (enableDebugKeys)
        {
            HandleDebugInput();
        }

        UpdateSustainWeight();
        UpdateShake();
        SyncSceneLightFlicker();
        ApplyVisuals();
    }

    void LateUpdate()
    {
        ApplyCameraImpact();
    }

    /// <summary>추적 상태. false→true 시 Impact + Burst 자동 재생.</summary>
    public void SetBeingChased(bool chased)
    {
        if (chased && !isBeingChased)
        {
            sustainWeight = 1f;
            PlayDetectionBurst();
        }

        isBeingChased = chased;
        SyncSceneLightFlicker();
    }

    public void PlayDetectionBurst()
    {
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
        }

        if (impactRoutine != null)
        {
            StopCoroutine(impactRoutine);
        }

        shakeTimeRemaining = impactShakeDuration;
        burstRoutine = StartCoroutine(BurstRoutine());
        impactRoutine = StartCoroutine(ImpactRoutine());
    }

    /// <summary>씬 라이트 점멸 배율 (1=기본). phaseOffset으로 라이트별 위상을 분리합니다.</summary>
    public float GetFlickerMultiplier(float phaseOffset = 0f)
    {
        if (!isBeingChased || ShouldSuppressEffects())
        {
            return 1f;
        }

        if (flickerBlackoutTimer > 0f)
        {
            float blackoutBlend = GetFlickerStrobeBlend();
            return Mathf.Lerp(flickerMin, flickerBlackoutMultiplier, blackoutBlend);
        }

        float smooth = SampleSmoothMultiplier(phaseOffset);
        if (!useHybridFlicker)
        {
            return smooth;
        }

        float strobe = SampleStrobeMultiplier(phaseOffset);
        float strobeBlend = GetFlickerStrobeBlend();
        return Mathf.Lerp(smooth, strobe, strobeBlend);
    }

    private float GetFlicker01()
    {
        return Mathf.InverseLerp(flickerMin, flickerMax, GetFlickerMultiplier());
    }

    private float GetFlickerStrobeBlend()
    {
        if (!isBeingChased || ShouldSuppressEffects() || !useHybridFlicker)
        {
            return 0f;
        }

        float noise = Mathf.PerlinNoise(flickerBlendSeed, Time.time * flickerBlendSpeed);
        float shaped = Mathf.Pow(noise, flickerBlendSharpness);
        return Mathf.Clamp01(shaped * flickerBlendWeight);
    }

    private float SampleSmoothMultiplier(float phaseOffset)
    {
        float hz = Mathf.Max(0.1f, flickerStrobeHz);
        float phase = Time.time * hz + phaseOffset;
        float sine = (Mathf.Sin(phase * Mathf.PI * 2f) + 1f) * 0.5f;

        float noise = Mathf.PerlinNoise(flickerNoiseSeed + phaseOffset, Time.time * flickerSpeed);
        float shapedNoise = Mathf.Pow(noise, flickerSharpness);
        float organic = Mathf.Lerp(sine, shapedNoise, 0.35f);

        return Mathf.Lerp(flickerMin, flickerMax, organic);
    }

    private float SampleStrobeMultiplier(float phaseOffset)
    {
        float hz = Mathf.Max(0.1f, flickerStrobeHz);
        float duty = Mathf.Clamp01(flickerDutyCycle);
        float phase = Time.time * hz + phaseOffset;
        float cycle = phase - Mathf.Floor(phase);
        return cycle < duty ? flickerMax : flickerMin;
    }

    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(debugBurstKey))
        {
            SetBeingChased(true);
            Debug.Log("[ChaseEffect] Debug: 발각 + 추적 ON (F6)");
        }

        if (Input.GetKeyDown(debugChaseToggleKey))
        {
            SetBeingChased(!isBeingChased);
            Debug.Log($"[ChaseEffect] Debug: 추적 {(isBeingChased ? "ON" : "OFF")} (F7)");
        }
    }

    private bool ShouldSuppressEffects()
    {
        return hidingController != null && hidingController.isHiding;
    }

    private bool ShouldSuppressCameraImpact()
    {
        if (hidingController != null && hidingController.isHiding)
        {
            return true;
        }

        return camcorderEnergy != null && camcorderEnergy.IsViewfinderActive;
    }

    private void SyncSceneLightFlicker()
    {
        if (sceneLightFlicker == null)
        {
            return;
        }

        bool active = isBeingChased && !ShouldSuppressEffects();
        sceneLightFlicker.SetChaseActive(active);
    }

    private void UpdateSustainWeight()
    {
        float target = (!ShouldSuppressEffects() && isBeingChased) ? 1f : 0f;
        float fadeSpeed = target > sustainWeight
            ? Mathf.Max(0.01f, sustainFadeIn)
            : Mathf.Max(0.01f, sustainFadeOut);

        sustainWeight = Mathf.MoveTowards(sustainWeight, target, Time.deltaTime / fadeSpeed);
    }

    private void ApplyVisuals()
    {
        if (chaseVolume == null)
        {
            return;
        }

        if (ShouldSuppressEffects())
        {
            chaseVolume.weight = 0f;
            return;
        }

        float sustain = sustainWeight;
        float burst = burstIntensity;
        float impact = impactIntensity;
        float flicker01 = GetFlicker01();

        chaseVolume.weight = Mathf.Clamp01(
            Mathf.Max(sustain * sustainVolumeWeight, burst * burstVolumeWeight, impact));

        colorAdjustments.colorFilter.value = Color.Lerp(Color.white, impactRedColor, impact);
        colorAdjustments.saturation.value = 0f;

        float sustainExposure = 0f;
        if (burst < 0.01f && sustain > 0.01f)
        {
            sustainExposure = -flicker01 * flickerExposureAmp;
        }

        colorAdjustments.postExposure.value = burst * burstPostExposurePeak + sustainExposure;

        vignette.intensity.value = impact * impactRedVignette;
        vignette.color.value = impactVignetteColor;

        float baseGrain = sustain * sustainGrain;
        float flickerGrain = baseGrain * Mathf.Lerp(0.65f, 1f, flicker01);
        filmGrain.intensity.value = burst > 0.001f
            ? Mathf.Lerp(Mathf.Max(baseGrain, flickerGrain), burstGrainPeak, burst)
            : flickerGrain;

        float sustainChromaticValue = sustain * sustainChromatic * Mathf.Lerp(0.5f, 1f, flicker01);
        chromaticAberration.intensity.value = burst > 0.001f
            ? Mathf.Max(sustainChromaticValue, burst * burstChromaticPeak)
            : sustainChromaticValue;
    }

    private void ApplyCameraImpact()
    {
        if (mainCamera == null)
        {
            return;
        }

        ClearShakeOffset();

        if (ShouldSuppressCameraImpact())
        {
            mainCamera.fieldOfView = baseFieldOfView;
            return;
        }

        float impact = impactIntensity;
        mainCamera.fieldOfView = impact > 0.001f
            ? baseFieldOfView + impact * impactFovPunch
            : baseFieldOfView;

        if (shakeTransform == null || shakeTimeRemaining <= 0f)
        {
            return;
        }

        float t = Time.time * 42f;
        Vector3 posOffset = new Vector3(
            (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * impactShakePos,
            (Mathf.PerlinNoise(0.2f, t) - 0.5f) * 2f * impactShakePos,
            0f);
        Vector3 rotOffset = new Vector3(
            (Mathf.PerlinNoise(t, 0.5f) - 0.5f) * 2f * impactShakeRot,
            (Mathf.PerlinNoise(0.6f, t) - 0.5f) * 2f * impactShakeRot,
            (Mathf.PerlinNoise(t, 0.9f) - 0.5f) * 2f * impactShakeRot * 0.5f);

        lastShakePosOffset = posOffset;
        lastShakeRotOffset = Quaternion.Euler(rotOffset);
        ApplyShakeOffset();
    }

    private void UpdateShake()
    {
        UpdateFlickerBlackout();

        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.deltaTime;
        }
    }

    private void UpdateFlickerBlackout()
    {
        if (flickerBlackoutTimer > 0f)
        {
            flickerBlackoutTimer -= Time.deltaTime;
            return;
        }

        if (!isBeingChased || ShouldSuppressEffects())
        {
            return;
        }

        if (Random.value < flickerBlackoutChance * Time.deltaTime)
        {
            flickerBlackoutTimer = Random.Range(flickerBlackoutDuration.x, flickerBlackoutDuration.y);
        }
    }

    private void ClearShakeOffset()
    {
        if (shakeTransform != null && lastShakePosOffset.sqrMagnitude > 0.000001f)
        {
            shakeTransform.localPosition -= lastShakePosOffset;
        }

        lastShakePosOffset = Vector3.zero;
        lastShakeRotOffset = Quaternion.identity;
    }

    private void ApplyShakeOffset()
    {
        if (shakeTransform == null)
        {
            return;
        }

        shakeTransform.localPosition += lastShakePosOffset;
        shakeTransform.localRotation *= lastShakeRotOffset;
    }

    private IEnumerator BurstRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, burstDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            burstIntensity = burstFalloff.Evaluate(t);
            yield return null;
        }

        burstIntensity = 0f;
        burstRoutine = null;
    }

    private IEnumerator ImpactRoutine()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, impactDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            impactIntensity = impactFalloff.Evaluate(t);
            yield return null;
        }

        impactIntensity = 0f;
        impactRoutine = null;
    }

    private void ResolveCameraReferences()
    {
        if (playerController != null && playerController.cameraTransform != null)
        {
            shakeTransform = playerController.cameraTransform;
        }
        else
        {
            shakeTransform = Camera.main != null ? Camera.main.transform : null;
        }

        mainCamera = shakeTransform != null ? shakeTransform.GetComponent<Camera>() : Camera.main;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            baseFieldOfView = mainCamera.fieldOfView;
        }

    }

    private void EnsureMainCameraPostProcessing()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
        }
    }

    private void BuildChaseVolume()
    {
        GameObject volumeObject = new GameObject("ChaseEffectVolume");
        volumeObject.transform.SetParent(transform, false);

        chaseVolume = volumeObject.AddComponent<Volume>();
        chaseVolume.isGlobal = true;
        chaseVolume.priority = volumePriority;
        chaseVolume.weight = 0f;

        chaseProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        chaseVolume.sharedProfile = chaseProfile;

        colorAdjustments = chaseProfile.Add<ColorAdjustments>(true);
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.colorFilter.value = Color.white;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = 0f;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = 0f;

        vignette = chaseProfile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.35f;
        vignette.color.overrideState = true;
        vignette.color.value = impactVignetteColor;

        filmGrain = chaseProfile.Add<FilmGrain>(true);
        filmGrain.type.overrideState = true;
        filmGrain.type.value = FilmGrainLookup.Large02;
        filmGrain.intensity.overrideState = true;
        filmGrain.intensity.value = 0f;
        filmGrain.response.overrideState = true;
        filmGrain.response.value = 1f;

        chromaticAberration = chaseProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.overrideState = true;
        chromaticAberration.intensity.value = 0f;
    }

    void OnDestroy()
    {
        if (chaseVolume != null)
        {
            Destroy(chaseVolume.gameObject);
        }

        if (chaseProfile != null)
        {
            Destroy(chaseProfile);
        }

        ClearShakeOffset();

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = baseFieldOfView;
        }
    }
}
