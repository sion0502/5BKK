using UnityEngine;

/// <summary>
/// 캠코더 내장 배터리: 뷰파인더 펼침(active) 동안 소모, 그 외(장착 여부 무관) 충전.
/// 에너지는 플레이어에 유지되어 F로 내려놔도 충전이 이어집니다.
/// </summary>
public class CamcorderEnergyController : MonoBehaviour
{
    private const string CamcorderResourcePath = "ItemDatas/Equipment/Camcorder";

    [SerializeField] private Equipment camcorderEquipment;

    private InventoryManager inventory;
    private EquipmentViewController equipmentView;
    private CamcorderController activeCamcorderController;
    private float currentEnergy = -1f;

    public Equipment CamcorderEquipment => camcorderEquipment;

    /// <summary>뷰파인더(야간투시)가 켜져 있으면 true. 인벤 슬롯 입력 차단 등에 사용.</summary>
    public bool IsViewfinderActive =>
        activeCamcorderController != null && activeCamcorderController.IsViewfinderActive;

    public float CurrentEnergy
    {
        get
        {
            EnsureInitialized();
            return currentEnergy;
        }
    }

    public float MaxEnergy => camcorderEquipment != null ? camcorderEquipment.maxEnergy : 0f;

    public float EnergyRatio
    {
        get
        {
            if (camcorderEquipment == null || camcorderEquipment.maxEnergy <= 0f)
                return 0f;

            EnsureInitialized();
            return Mathf.Clamp01(currentEnergy / camcorderEquipment.maxEnergy);
        }
    }

    public bool CanOpenViewfinder
    {
        get
        {
            if (inventory == null || !inventory.HasCamcorder)
                return false;

            EnsureInitialized();
            return currentEnergy > 0f;
        }
    }

    void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        equipmentView = GetComponent<EquipmentViewController>();

        if (camcorderEquipment == null)
            camcorderEquipment = Resources.Load<Equipment>(CamcorderResourcePath);
    }

    void Update()
    {
        if (camcorderEquipment == null || inventory == null || !inventory.HasCamcorder)
        {
            currentEnergy = -1f;
            activeCamcorderController = null;
            return;
        }

        EnsureInitialized();

        if (IsViewfinderActive)
        {
            currentEnergy -= camcorderEquipment.consumeRate * Time.deltaTime;
            if (currentEnergy <= 0f)
            {
                currentEnergy = 0f;
                NotifyBatteryHud();
                ForceCloseViewfinder();
            }
            else
            {
                NotifyBatteryHud();
            }
        }
        else
        {
            activeCamcorderController = null;
            float recharge = GetRechargeRate();
            currentEnergy = Mathf.Min(camcorderEquipment.maxEnergy, currentEnergy + recharge * Time.deltaTime);
        }
    }

    public void RegisterActiveViewfinder(CamcorderController controller)
    {
        if (controller != null)
            activeCamcorderController = controller;
    }

    public void UnregisterActiveViewfinder(CamcorderController controller)
    {
        if (activeCamcorderController == controller)
            activeCamcorderController = null;
    }

    /// <summary>
    /// 0=Empty, 1~4=Battery_1~4.
    /// Floor: 75%→4칸, 50%→3칸, 25%→2칸, 0% 초과→1칸.
    /// </summary>
    public int GetBatteryLevelIndex()
    {
        if (inventory == null || !inventory.HasCamcorder)
            return 0;

        EnsureInitialized();

        if (currentEnergy <= 0f)
            return 0;

        float ratio = EnergyRatio;
        int level = Mathf.FloorToInt(ratio * 4f);
        return Mathf.Clamp(level + 1, 1, 4);
    }

    public void ForceCloseViewfinder()
    {
        if (activeCamcorderController == null || !activeCamcorderController.IsViewfinderActive)
            return;

        activeCamcorderController.SetActive(false);
        Debug.Log("[Camcorder] 배터리 방전으로 뷰파인더를 닫습니다.");
    }

    private void NotifyBatteryHud()
    {
        activeCamcorderController?.RequestBatteryHudRefresh();
    }

    private float GetRechargeRate()
    {
        if (camcorderEquipment.rechargeRate > 0f)
            return camcorderEquipment.rechargeRate;

        return camcorderEquipment.consumeRate * 0.5f;
    }

    private void EnsureInitialized()
    {
        if (currentEnergy < 0f && camcorderEquipment != null)
            currentEnergy = camcorderEquipment.maxEnergy;
    }
}
