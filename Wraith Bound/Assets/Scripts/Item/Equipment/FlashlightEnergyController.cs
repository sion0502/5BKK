using UnityEngine;

/// <summary>
/// 손전등 배터리: 켜져 있을 때만 consumeRate 소모, R키 충전은 SelectedItemUseController에서 호출.
/// </summary>
public class FlashlightEnergyController : MonoBehaviour
{
    private const string TurnOnClipPath = "Sound/Flashlight_TurnOn";
    private const string TurnOffClipPath = "Sound/Flashlight_TurnOff";

    [SerializeField] private Equipment flashlightEquipment;
    [SerializeField] private ActiveItem batteryItem;

    [Header("Toggle Sound")]
    [SerializeField] private AudioClip turnOnClip;
    [SerializeField] private AudioClip turnOffClip;
    [SerializeField] private float toggleSoundVolume = 0.7f;

    [Header("Low Battery Flicker")]
    [SerializeField] private float lowBatteryRatio = 0.15f;
    [SerializeField] private float minIntensityMultiplier = 0.2f;
    [SerializeField] private float maxIntensityMultiplier = 1f;
    [SerializeField] private float flickerSpeed = 20f;
    [SerializeField] private float flickerSharpness = 2.2f;
    [SerializeField] private float flickerSmoothing = 18f;
    [SerializeField] private float blackoutChancePerSecond = 0.4f;
    [SerializeField] private Vector2 blackoutDuration = new Vector2(0.03f, 0.12f);
    [SerializeField] private float blackoutIntensityMultiplier = 0.15f;

    private InventoryManager inventory;
    private EquipmentViewController equipmentView;
    private float currentEnergy = -1f;
    private float baseLightIntensity = -1f;
    private float noiseSeed;
    private float blackoutTimer;

    public Equipment FlashlightEquipment => flashlightEquipment;

    public bool CanTurnLightOn
    {
        get
        {
            EnsureInitialized();
            return currentEnergy > 0f;
        }
    }

    public bool IsFlashlightEquipment(Equipment equipment)
    {
        return equipment != null && equipment == flashlightEquipment;
    }

    void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        equipmentView = GetComponent<EquipmentViewController>();
        noiseSeed = Random.value * 100f;

        if (flashlightEquipment == null)
        {
            flashlightEquipment = Resources.Load<Equipment>("ItemDatas/Equipment/FlashLight");
        }

        if (batteryItem == null)
        {
            batteryItem = Resources.Load<ActiveItem>("ItemDatas/Active/Battery");
        }

        if (turnOnClip == null)
        {
            turnOnClip = Resources.Load<AudioClip>(TurnOnClipPath);
        }

        if (turnOffClip == null)
        {
            turnOffClip = Resources.Load<AudioClip>(TurnOffClipPath);
        }
    }

    void Update()
    {
        if (flashlightEquipment == null)
        {
            return;
        }

        EnsureInitialized();

        if (!TryGetFlashlightLight(out Light light))
        {
            return;
        }

        if (!light.enabled)
        {
            ResetLightIntensityCache();
            return;
        }

        EnsureBaseLightIntensity(light);

        float energyRatio = flashlightEquipment.maxEnergy > 0f
            ? currentEnergy / flashlightEquipment.maxEnergy
            : 0f;

        if (energyRatio <= lowBatteryRatio)
        {
            ApplyLowBatteryFlicker(light, energyRatio);
        }
        else
        {
            blackoutTimer = 0f;
            light.enabled = true;
            light.intensity = baseLightIntensity;
        }

        currentEnergy -= flashlightEquipment.consumeRate * Time.deltaTime;

        if (currentEnergy <= 0f)
        {
            currentEnergy = 0f;
            light.enabled = false;
            ResetLightIntensityCache();
            Debug.Log("[Flashlight] 배터리가 방전되어 꺼졌습니다.");
        }
    }

    public void PlayToggleSound(bool turningOn, Vector3 position)
    {
        AudioClip clip = turningOn ? turnOnClip : turnOffClip;
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, toggleSoundVolume);
    }

    public void OnFlashlightToggled(bool turningOn)
    {
        if (!turningOn)
        {
            ResetLightIntensityCache();
        }
    }

    /// <summary>
    /// 손전등 슬롯 선택 + 인벤에 건전지 있을 때 R키로 호출.
    /// </summary>
    public bool TryRechargeFromInventoryBattery()
    {
        if (flashlightEquipment == null || batteryItem == null || inventory == null)
        {
            return false;
        }

        if (inventory.GetSelectedItem() != flashlightEquipment)
        {
            return false;
        }

        if (inventory.CountItem(batteryItem) < 1)
        {
            Debug.LogWarning("[Flashlight] 인벤토리에 건전지가 없습니다.");
            return false;
        }

        EnsureInitialized();

        if (currentEnergy >= flashlightEquipment.maxEnergy - 0.01f)
        {
            Debug.Log("[Flashlight] 이미 완전히 충전되어 있습니다.");
            return false;
        }

        if (!inventory.TryConsumeItem(batteryItem, 1))
        {
            return false;
        }

        currentEnergy = Mathf.Min(flashlightEquipment.maxEnergy, currentEnergy + batteryItem.value);
        Debug.Log($"[Flashlight] 충전됨 ({Mathf.CeilToInt(currentEnergy)}/{flashlightEquipment.maxEnergy})");
        return true;
    }

    private void EnsureInitialized()
    {
        if (currentEnergy < 0f && flashlightEquipment != null)
        {
            currentEnergy = flashlightEquipment.maxEnergy;
        }
    }

    private void EnsureBaseLightIntensity(Light light)
    {
        if (baseLightIntensity < 0f && light != null)
        {
            baseLightIntensity = light.intensity;
        }
    }

    private void ResetLightIntensityCache()
    {
        baseLightIntensity = -1f;
        blackoutTimer = 0f;
    }

    private void ApplyLowBatteryFlicker(Light light, float energyRatio)
    {
        float stress = 1f - Mathf.Clamp01(energyRatio / Mathf.Max(lowBatteryRatio, 0.0001f));
        float scaledBlackoutChance = blackoutChancePerSecond * Mathf.Lerp(0.35f, 1f, stress);
        float multiplier = GetFlickerMultiplier(scaledBlackoutChance);

        light.enabled = multiplier > 0.05f;
        if (!light.enabled)
        {
            return;
        }

        float targetIntensity = baseLightIntensity * multiplier;
        light.intensity = Mathf.Lerp(
            light.intensity,
            targetIntensity,
            Time.deltaTime * flickerSmoothing);
    }

    private float GetFlickerMultiplier(float scaledBlackoutChance)
    {
        if (blackoutTimer > 0f)
        {
            blackoutTimer -= Time.deltaTime;
            return blackoutIntensityMultiplier;
        }

        if (Random.value < scaledBlackoutChance * Time.deltaTime)
        {
            blackoutTimer = Random.Range(blackoutDuration.x, blackoutDuration.y);
            return blackoutIntensityMultiplier;
        }

        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        float shaped = Mathf.Pow(noise, flickerSharpness);
        return Mathf.Lerp(minIntensityMultiplier, maxIntensityMultiplier, shaped);
    }

    private bool TryGetFlashlightLight(out Light light)
    {
        light = null;

        if (equipmentView == null || inventory == null)
        {
            return false;
        }

        if (inventory.GetSelectedItem() != flashlightEquipment)
        {
            return false;
        }

        return equipmentView.TryGetEquipmentLight(flashlightEquipment, out light);
    }
}
