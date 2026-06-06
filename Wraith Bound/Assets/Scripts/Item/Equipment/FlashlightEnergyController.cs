using UnityEngine;

/// <summary>
/// 손전등 배터리: 켜져 있을 때만 consumeRate 소모, R키 충전은 SelectedItemUseController에서 호출.
/// </summary>
public class FlashlightEnergyController : MonoBehaviour
{
    [SerializeField] private Equipment flashlightEquipment;
    [SerializeField] private ActiveItem batteryItem;

    private InventoryManager inventory;
    private EquipmentViewController equipmentView;
    private float currentEnergy = -1f;

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

        if (flashlightEquipment == null)
        {
            flashlightEquipment = Resources.Load<Equipment>("ItemDatas/Equipment/FlashLight");
        }

        if (batteryItem == null)
        {
            batteryItem = Resources.Load<ActiveItem>("ItemDatas/Active/Battery");
        }
    }

    void Update()
    {
        if (flashlightEquipment == null)
        {
            return;
        }

        EnsureInitialized();

        if (!TryGetFlashlightLight(out Light light) || !light.enabled)
        {
            return;
        }

        currentEnergy -= flashlightEquipment.consumeRate * Time.deltaTime;

        if (currentEnergy <= 0f)
        {
            currentEnergy = 0f;
            light.enabled = false;
            Debug.Log("[Flashlight] 배터리가 방전되어 꺼졌습니다.");
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
