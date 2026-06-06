using UnityEngine;

public class FlickeringLamp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light targetLight;
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private ParticleSystem sparkParticles;
    [SerializeField] private Transform sparkOrigin;

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

    [Header("Blackout Jitter")]
    [SerializeField] private float blackoutChancePerSecond = 0.45f;
    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.03f, 0.12f);
    [SerializeField] private float blackoutIntensityMultiplier = 0.02f;

    [Header("Sparks")]
    [SerializeField] private bool createSparkParticlesIfMissing = true;
    [SerializeField] private Vector3 sparkLocalOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField] private float sparkChancePerSecond = 0.7f;
    [SerializeField] private Vector2Int sparkBurstCount = new Vector2Int(2, 7);
    [SerializeField] private Color sparkColor = new Color(1f, 0.72f, 0.28f, 1f);

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

        if (emissiveRenderers == null || emissiveRenderers.Length == 0)
        {
            emissiveRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (sparkParticles == null)
        {
            sparkParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (sparkParticles == null && createSparkParticlesIfMissing)
        {
            sparkParticles = CreateSparkParticles();
        }

        ConfigureSparkMaterial();

        if (targetLight != null)
        {
            initialIntensity = baseIntensity > 0f ? baseIntensity : targetLight.intensity;
            emissionColor = targetLight.color;
            targetLight.enabled = true;
        }
        else
        {
            initialIntensity = Mathf.Max(baseIntensity, 1f);
        }

        noiseSeed = Random.value * 100f;
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
            ? referenceLight.intensity * 0.45f
            : 12f;
        float fallbackRange = referenceLight != null
            ? Mathf.Clamp(referenceLight.range * 0.18f, 5f, maxFlickerLightRange)
            : 6f;

        light.intensity = Mathf.Max(generatedLightIntensity, fallbackIntensity);
        light.range = Mathf.Max(generatedLightRange, fallbackRange);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.75f;
        light.bounceIntensity = 0.2f;

        return light;
    }

    private void OnDestroy()
    {
        if (generatedSparkMaterial != null)
        {
            Destroy(generatedSparkMaterial);
        }
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

        float targetIntensity = initialIntensity * multiplier;
        targetLight.intensity = Mathf.Lerp(
            targetLight.intensity,
            targetIntensity,
            Time.deltaTime * smoothing
        );
    }

    private void ApplyEmission(float multiplier)
    {
        if (emissiveRenderers == null)
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

    private void TryEmitSparks(float multiplier)
    {
        if (sparkParticles == null)
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
        if (sparkParticles == null || count <= 0)
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

        if (generatedSparkMaterial.HasProperty("_BaseColor"))
        {
            generatedSparkMaterial.SetColor("_BaseColor", sparkColor);
        }

        if (generatedSparkMaterial.HasProperty("_Color"))
        {
            generatedSparkMaterial.SetColor("_Color", sparkColor);
        }

        particleRenderer.material = generatedSparkMaterial;
    }
}
