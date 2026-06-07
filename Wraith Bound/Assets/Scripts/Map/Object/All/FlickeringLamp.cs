using UnityEngine;
using UnityEngine.Rendering;

public class FlickeringLamp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light targetLight;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private ParticleSystem sparkParticles;
    [SerializeField] private Transform sparkOrigin;

    [Header("Emission Flicker")]
    [Tooltip("발광부 MeshRenderer가 따로 지정된 경우에만 켜세요. 자동 수집은 램프 전체 메쉬를 검게 만들 수 있습니다.")]
    [SerializeField] private bool driveEmissionRenderers = false;

    [Header("Light Flicker")]
    [SerializeField] private float baseIntensity = -1f;
    [SerializeField] private float minIntensityMultiplier = 0.25f;
    [SerializeField] private float maxIntensityMultiplier = 1.15f;
    [SerializeField] private float flickerSpeed = 18f;
    [SerializeField] private float flickerSharpness = 2.4f;
    [SerializeField] private float smoothing = 18f;

    [Header("Local Flicker Light")]
    [SerializeField] private bool createLocalLightIfNeeded = true;
    [SerializeField] private bool ignoreLargeSceneLights = true;
    [SerializeField] private float maxFlickerLightRange = 8f;
    [SerializeField] private float generatedLightIntensity = 16f;
    [SerializeField] private float generatedLightRange = 7f;
    [SerializeField] private Color generatedLightColor = new Color(1f, 0.72f, 0.38f, 1f);
    [SerializeField] private bool useReferenceLightColor = false;
    [SerializeField] private Vector3 generatedLightLocalOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private bool generatedLightCastsShadows = false;
    [Tooltip("램프 housing이 벽에 직사각형 그림자를 남기지 않도록 MeshRenderer 그림자 cast를 끕니다.")]
    [SerializeField] private bool disableLampMeshShadowCasters = true;

    [Header("Blackout Jitter")]
    [SerializeField] private float blackoutChancePerSecond = 0.28f;
    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.03f, 0.1f);
    [SerializeField] private float blackoutIntensityMultiplier = 0.35f;

    [Header("Sparks")]
    [SerializeField] private bool useLineSparks = true;
    [SerializeField] private bool createSparkParticlesIfMissing = true;
    [SerializeField] private Vector3 sparkLocalOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField] private float sparkChancePerSecond = 0.7f;
    [SerializeField] private Vector2Int sparkBurstCount = new Vector2Int(2, 7);
    [SerializeField] private Color sparkColor = new Color(1f, 0.72f, 0.28f, 1f);
    [SerializeField] private Vector2 sparkLineLength = new Vector2(0.08f, 0.22f);
    [SerializeField] private Vector2 sparkLineLifetime = new Vector2(0.04f, 0.11f);

    [Header("Optional Crackle Audio")]
    [SerializeField] private AudioSource crackleAudio;
    [SerializeField] private Vector2 cracklePitch = new Vector2(0.85f, 1.25f);
    [SerializeField] private Vector2 crackleVolume = new Vector2(0.05f, 0.18f);

    private MaterialPropertyBlock propertyBlock;
    private float initialIntensity;
    private float noiseSeed;
    private float blackoutTimer;
    private Color emissionColor = Color.white;
    private Material generatedSparkMaterial;
    private Texture2D generatedSparkTexture;
    private Material generatedLineSparkMaterial;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        Light referenceLight = targetLight != null
            ? targetLight
            : GetComponentInChildren<Light>(true);

        targetLight = ResolveTargetLight();
        if (targetLight == null && createLocalLightIfNeeded)
        {
            targetLight = CreateLocalFlickerLight(referenceLight);
        }

        if (!useLineSparks && sparkParticles == null)
        {
            sparkParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (!useLineSparks && sparkParticles == null && createSparkParticlesIfMissing)
        {
            sparkParticles = CreateSparkParticles();
        }

        if (useLineSparks)
        {
            ConfigureLineSparkMaterial();
        }
        else
        {
            ConfigureSparkMaterial();
        }

        if (targetLight != null)
        {
            initialIntensity = SanitizeIntensity(
                baseIntensity > 0f ? baseIntensity : targetLight.intensity,
                generatedLightIntensity
            );
            emissionColor = targetLight.color;
            targetLight.enabled = true;
            targetLight.shadows = LightShadows.None;
            targetLight.intensity = initialIntensity;
        }
        else
        {
            initialIntensity = SanitizeIntensity(Mathf.Max(baseIntensity, 1f), generatedLightIntensity);
        }

        if (disableLampMeshShadowCasters)
        {
            DisableLampMeshShadowCasters();
        }

        noiseSeed = Random.value * 100f;
    }

    private void DisableLampMeshShadowCasters()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private Light ResolveTargetLight()
    {
        if (targetLight != null && CanUseAsFlickerLight(targetLight))
        {
            return targetLight;
        }

        Light[] lights = GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            if (CanUseAsFlickerLight(light))
            {
                return light;
            }
        }

        return null;
    }

    private bool CanUseAsFlickerLight(Light light)
    {
        if (light == null)
        {
            return false;
        }

        return !ignoreLargeSceneLights || light.range <= maxFlickerLightRange;
    }

    private Light CreateLocalFlickerLight(Light referenceLight)
    {
        GameObject lightObject = new GameObject("Lamp_FlickerLight_Runtime");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = generatedLightLocalOffset;
        lightObject.transform.localRotation = Quaternion.identity;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = useReferenceLightColor && referenceLight != null
            ? referenceLight.color
            : generatedLightColor;
        float fallbackIntensity = referenceLight != null
            ? SanitizeIntensity(referenceLight.intensity, generatedLightIntensity) * 0.45f
            : 12f;
        float fallbackRange = referenceLight != null
            ? Mathf.Clamp(referenceLight.range * 0.18f, 5f, maxFlickerLightRange)
            : 6f;

        light.intensity = SanitizeIntensity(
            Mathf.Max(generatedLightIntensity, fallbackIntensity),
            generatedLightIntensity
        );
        light.range = Mathf.Max(generatedLightRange, fallbackRange);
        light.shadows = generatedLightCastsShadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = 0.75f;
        light.bounceIntensity = 0.2f;

        return light;
    }

    private void OnDestroy()
    {
        ClearEmissionBlocks();

        if (generatedSparkMaterial != null)
        {
            Destroy(generatedSparkMaterial);
        }

        if (generatedSparkTexture != null)
        {
            Destroy(generatedSparkTexture);
        }

        if (generatedLineSparkMaterial != null)
        {
            Destroy(generatedLineSparkMaterial);
        }
    }

    private void OnDisable()
    {
        ClearEmissionBlocks();
    }

    private void Update()
    {
        float multiplier = GetFlickerMultiplier();
        ApplyLight(multiplier);
        ApplyEmission(multiplier);
        TryEmitSparks(multiplier);
    }

    private float GetFlickerMultiplier()
    {
        if (blackoutTimer > 0f)
        {
            blackoutTimer -= Time.deltaTime;
            return blackoutIntensityMultiplier;
        }

        if (Random.value < blackoutChancePerSecond * Time.deltaTime)
        {
            blackoutTimer = Random.Range(blackoutDuration.x, blackoutDuration.y);
            EmitSparks(Random.Range(sparkBurstCount.x, sparkBurstCount.y + 1));
            PlayCrackle();
            return blackoutIntensityMultiplier;
        }

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        float shaped = Mathf.Pow(noise, flickerSharpness);
        return Mathf.Lerp(minIntensityMultiplier, maxIntensityMultiplier, shaped);
    }

    private void ApplyLight(float multiplier)
    {
        if (targetLight == null)
        {
            return;
        }

        if (!IsValidIntensity(initialIntensity))
        {
            initialIntensity = SanitizeIntensity(generatedLightIntensity, 1f);
        }

        float safeMultiplier = Mathf.Clamp(
            SanitizeIntensity(multiplier, 1f),
            0f,
            Mathf.Max(maxIntensityMultiplier, blackoutIntensityMultiplier)
        );
        float targetIntensity = initialIntensity * safeMultiplier;
        float currentIntensity = SanitizeIntensity(targetLight.intensity, targetIntensity);

        float blend = Mathf.Clamp01(Time.deltaTime * smoothing);
        targetLight.intensity = SanitizeIntensity(
            Mathf.Lerp(currentIntensity, targetIntensity, blend),
            targetIntensity
        );
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

    private void ApplyEmission(float multiplier)
    {
        if (!driveEmissionRenderers || emissiveRenderers == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Color targetEmission = emissionColor * (initialIntensity * multiplier);
        foreach (Renderer renderer in emissiveRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", targetEmission);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ClearEmissionBlocks()
    {
        if (emissiveRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in emissiveRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.SetPropertyBlock(null);
        }
    }

    private void TryEmitSparks(float multiplier)
    {
        if (!useLineSparks && sparkParticles == null)
        {
            return;
        }

        float stress = 1f - Mathf.InverseLerp(
            minIntensityMultiplier,
            maxIntensityMultiplier,
            multiplier
        );

        if (Random.value < sparkChancePerSecond * stress * Time.deltaTime)
        {
            EmitSparks(Random.Range(sparkBurstCount.x, sparkBurstCount.y + 1));
            PlayCrackle();
        }
    }

    private void EmitSparks(int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (useLineSparks)
        {
            EmitLineSparks(count);
            return;
        }

        if (sparkParticles == null)
        {
            return;
        }

        sparkParticles.Emit(count);
    }

    private void PlayCrackle()
    {
        if (crackleAudio == null)
        {
            return;
        }

        crackleAudio.pitch = Random.Range(cracklePitch.x, cracklePitch.y);
        crackleAudio.volume = Random.Range(crackleVolume.x, crackleVolume.y);
        crackleAudio.Play();
    }

    private ParticleSystem CreateSparkParticles()
    {
        GameObject sparkObject = new GameObject("Lamp_Sparks_Runtime");
        Transform parent = sparkOrigin != null ? sparkOrigin : transform;
        sparkObject.transform.SetParent(parent, false);
        sparkObject.transform.localPosition = sparkLocalOffset;
        sparkObject.transform.localRotation = Quaternion.identity;

        ParticleSystem particles = sparkObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.startColor = sparkColor;
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(sparkColor, 0f),
                new GradientColorKey(Color.white, 0.25f),
                new GradientColorKey(sparkColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.65f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.025f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.15f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 10;
        particleRenderer.receiveShadows = false;
        particleRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        return particles;
    }

    private void ConfigureSparkMaterial()
    {
        if (sparkParticles == null)
        {
            return;
        }

        ParticleSystemRenderer particleRenderer =
            sparkParticles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return;
        }

        generatedSparkMaterial = new Material(shader)
        {
            name = "Lamp Spark Material (Runtime)"
        };

        generatedSparkTexture = CreateSoftSparkTexture();
        if (generatedSparkMaterial.HasProperty("_BaseMap"))
        {
            generatedSparkMaterial.SetTexture("_BaseMap", generatedSparkTexture);
        }

        if (generatedSparkMaterial.HasProperty("_MainTex"))
        {
            generatedSparkMaterial.SetTexture("_MainTex", generatedSparkTexture);
        }

        if (generatedSparkMaterial.HasProperty("_BaseColor"))
        {
            generatedSparkMaterial.SetColor("_BaseColor", sparkColor);
        }

        if (generatedSparkMaterial.HasProperty("_Color"))
        {
            generatedSparkMaterial.SetColor("_Color", sparkColor);
        }

        ConfigureTransparentSparkBlend(generatedSparkMaterial);
        particleRenderer.material = generatedSparkMaterial;
    }

    private void ConfigureLineSparkMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return;
        }

        generatedLineSparkMaterial = new Material(shader)
        {
            name = "Lamp Line Spark Material (Runtime)"
        };

        if (generatedLineSparkMaterial.HasProperty("_BaseColor"))
        {
            generatedLineSparkMaterial.SetColor("_BaseColor", sparkColor);
        }

        if (generatedLineSparkMaterial.HasProperty("_Color"))
        {
            generatedLineSparkMaterial.SetColor("_Color", sparkColor);
        }

        ConfigureTransparentSparkBlend(generatedLineSparkMaterial);
    }

    private void EmitLineSparks(int count)
    {
        Transform parent = sparkOrigin != null ? sparkOrigin : transform;
        Vector3 origin = parent.TransformPoint(sparkLocalOffset);

        for (int i = 0; i < count; i++)
        {
            GameObject sparkObject = new GameObject("Lamp_LineSpark_Runtime");
            sparkObject.transform.position = origin;

            LineRenderer line = sparkObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.material = generatedLineSparkMaterial;
            line.startWidth = Random.Range(0.008f, 0.018f);
            line.endWidth = 0f;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Color startColor = sparkColor;
            Color endColor = sparkColor;
            startColor.a = 1f;
            endColor.a = 0f;
            line.startColor = startColor;
            line.endColor = endColor;

            Vector3 direction = new Vector3(
                Random.Range(-0.8f, 0.8f),
                Random.Range(-1.2f, -0.2f),
                Random.Range(-0.8f, 0.8f)
            ).normalized;
            float length = Random.Range(sparkLineLength.x, sparkLineLength.y);
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + direction * length);

            Destroy(sparkObject, Random.Range(sparkLineLifetime.x, sparkLineLifetime.y));
        }
    }

    private Texture2D CreateSoftSparkTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(
            textureSize,
            textureSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "Lamp Spark Soft Dot (Runtime)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[textureSize * textureSize];
        float center = (textureSize - 1) * 0.5f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * alpha;
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void ConfigureTransparentSparkBlend(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 2f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
