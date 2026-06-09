using UnityEngine;

/// <summary>
/// 패시브 아이템의 주변 조명을 플레이어에 붙여 관리합니다. 손전등(SpotLight)과 독립적으로 동작합니다.
/// </summary>
[DisallowMultipleComponent]
public class PassiveLanternController : MonoBehaviour
{
    [SerializeField] private InventoryManager inventory;
    [SerializeField] private Vector3 lightLocalOffset = new Vector3(0f, 1.15f, 0.2f);

    [Header("Flicker")]
    [SerializeField] private float minIntensityMultiplier = 0.82f;
    [SerializeField] private float maxIntensityMultiplier = 1.08f;
    [SerializeField] private float flickerSpeed = 12f;
    [SerializeField] private float flickerSharpness = 2.2f;
    [SerializeField] private float smoothing = 14f;
    [SerializeField] private float blackoutChancePerSecond = 0.18f;
    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.02f, 0.08f);
    [SerializeField] private float blackoutIntensityMultiplier = 0.35f;

    private Light pointLight;
    private PassiveItem activePassive;
    private float baseIntensity;
    private float noiseSeed;
    private float blackoutTimer;
    private bool isLampOn = true;

    public bool IsLampOn => isLampOn;

    public bool HasToggleableLamp()
    {
        PassiveItem passive = inventory != null ? inventory.GetPassiveItem() : null;
        return passive != null && passive.providesAmbientLight;
    }

    public void ToggleLamp()
    {
        if (!HasToggleableLamp())
        {
            return;
        }

        isLampOn = !isLampOn;
    }

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<InventoryManager>();
        }

        noiseSeed = Random.value * 100f;
        EnsurePointLight();
    }

    private void Update()
    {
        SyncPassiveLight();
    }

    private void EnsurePointLight()
    {
        if (pointLight != null)
        {
            return;
        }

        var lightObject = new GameObject("PassiveLanternLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = lightLocalOffset;
        lightObject.transform.localRotation = Quaternion.identity;

        pointLight = lightObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.shadows = LightShadows.Soft;
        pointLight.shadowStrength = 0.55f;
        pointLight.bounceIntensity = 0.15f;
        pointLight.enabled = false;
    }

    private void SyncPassiveLight()
    {
        if (pointLight == null)
        {
            return;
        }

        PassiveItem passive = inventory != null ? inventory.GetPassiveItem() : null;
        bool wantsLight = passive != null
            && passive.providesAmbientLight
            && isLampOn
            && (inventory == null || !inventory.IsCamcorderViewfinderActive());

        if (!wantsLight)
        {
            if (pointLight.enabled)
            {
                pointLight.enabled = false;
            }

            activePassive = null;
            blackoutTimer = 0f;
            return;
        }

        if (activePassive != passive)
        {
            activePassive = passive;
            pointLight.color = passive.ambientLightColor;
            pointLight.range = passive.ambientLightRange;
            baseIntensity = passive.ambientLightIntensity;
            pointLight.intensity = baseIntensity;
        }

        pointLight.enabled = true;

        if (passive.flickerLight)
        {
            ApplyFlicker();
        }
        else
        {
            pointLight.intensity = baseIntensity;
        }
    }

    private void ApplyFlicker()
    {
        float multiplier = GetFlickerMultiplier();
        float targetIntensity = baseIntensity * multiplier;
        pointLight.intensity = Mathf.Lerp(
            pointLight.intensity,
            targetIntensity,
            Time.deltaTime * smoothing);
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
            return blackoutIntensityMultiplier;
        }

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        float shaped = Mathf.Pow(noise, flickerSharpness);
        return Mathf.Lerp(minIntensityMultiplier, maxIntensityMultiplier, shaped);
    }
}
