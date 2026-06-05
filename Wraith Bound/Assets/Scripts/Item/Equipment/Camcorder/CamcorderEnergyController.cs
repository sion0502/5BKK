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
    private float currentEnergy = -1f;

    public Equipment CamcorderEquipment => camcorderEquipment;

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
            return;
        }

        EnsureInitialized();

        if (IsViewfinderActive())
        {
            currentEnergy -= camcorderEquipment.consumeRate * Time.deltaTime;
            if (currentEnergy <= 0f)
            {
                currentEnergy = 0f;
                NotifyBatteryHud();   // 0%를 HUD에 먼저 반영
                ForceCloseViewfinder();
            }
            else
            {
                NotifyBatteryHud();   // 매 프레임 HUD 갱신
            }
        }
        else
        {
            float recharge = GetRechargeRate();
            currentEnergy = Mathf.Min(camcorderEquipment.maxEnergy, currentEnergy + recharge * Time.deltaTime);
        }
    }

    /// <summary>
    /// 0=Empty, 1~4=Battery_1~4.
    /// Floor 방식: 에너지가 구간 아래로 내려가는 즉시 한 칸 줄어들어 보입니다.
    ///   100% → 4칸  /  75% → 3칸  /  50% → 2칸  /  25% → 1칸  /  0% → Empty
    /// </summary>
    public int GetBatteryLevelIndex()
    {
        if (inventory == null || !inventory.HasCamcorder)
            return 0;

        EnsureInitialized();

        if (currentEnergy <= 0f)
            return 0;

        float ratio = EnergyRatio;
        // Floor: 0~0.25 → 0(Empty 처리됨), 0.25~0.5 → 1, 0.5~0.75 → 2, 0.75~1 → 3 → +1 = 1~4
        int level = Mathf.FloorToInt(ratio * 4f);
        return Mathf.Clamp(level + 1, 1, 4);
    }

    public bool IsViewfinderActive()
    {
        if (inventory == null || !inventory.IsCamcorderHeld() || equipmentView == null || camcorderEquipment == null)
            return false;

        if (!equipmentView.TryGetCurrentView(camcorderEquipment, out GameObject view))
            return false;

        CamcorderController controller = view.GetComponentInChildren<CamcorderController>(true);
        return controller != null && controller.IsViewfinderActive;
    }

    public void ForceCloseViewfinder()
    {
        if (inventory == null || !inventory.IsCamcorderHeld() || equipmentView == null || camcorderEquipment == null)
            return;

        if (!equipmentView.TryGetCurrentView(camcorderEquipment, out GameObject view))
            return;

        CamcorderController controller = view.GetComponentInChildren<CamcorderController>(true);
        if (controller != null && controller.IsViewfinderActive)
        {
            controller.SetActive(false);
            Debug.Log("[Camcorder] 배터리 방전으로 뷰파인더를 닫습니다.");
        }
    }

    /// <summary>
    /// CamcorderController 쪽 HUD 갱신을 요청합니다.
    /// 뷰파인더 Update와 독립적으로 소모량을 즉시 반영하기 위해 에너지 측에서 직접 호출합니다.
    /// </summary>
    private void NotifyBatteryHud()
    {
        if (inventory == null || !inventory.IsCamcorderHeld() || equipmentView == null || camcorderEquipment == null)
            return;

        if (!equipmentView.TryGetCurrentView(camcorderEquipment, out GameObject view))
            return;

        CamcorderController controller = view.GetComponentInChildren<CamcorderController>(true);
        controller?.RequestBatteryHudRefresh();
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
