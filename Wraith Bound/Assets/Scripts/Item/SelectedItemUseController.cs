using UnityEngine;
using UnityEngine.EventSystems;

public class SelectedItemUseController : MonoBehaviour
{
    [Header("Use Timing")]
    [SerializeField] private float activeItemHoldTime = 0.75f;

    private InventoryManager inventory;
    private PlayerController player;
    private SmartPhoneHolderToggle smartPhoneToggle;
    private EquipmentViewController equipmentView;
    private FlashlightEnergyController flashlightEnergy;
    private CamcorderEnergyController camcorderEnergy;
    private PassiveLanternController passiveLantern;
    private ActiveItem holdingActiveItem;
    private float holdTimer;

    void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        player = GetComponent<PlayerController>();
        smartPhoneToggle = GetComponent<SmartPhoneHolderToggle>();
        equipmentView = GetComponent<EquipmentViewController>();
        flashlightEnergy = GetComponent<FlashlightEnergyController>();

        if (flashlightEnergy == null)
        {
            flashlightEnergy = gameObject.AddComponent<FlashlightEnergyController>();
        }

        camcorderEnergy = GetComponent<CamcorderEnergyController>();
        if (camcorderEnergy == null)
        {
            camcorderEnergy = gameObject.AddComponent<CamcorderEnergyController>();
        }

        passiveLantern = GetComponent<PassiveLanternController>();
    }

    void Update()
    {
        if (IsPointerOverUI())
        {
            ResetActiveHold();
            return;
        }

        HandleFlashlightBatteryRecharge();
        HandlePassiveLampToggle();
        HandleEquipmentUse();
        HandleActiveItemHoldUse();
    }

    private void HandlePassiveLampToggle()
    {
        if (!Input.GetKeyDown(KeyCode.Mouse1) || passiveLantern == null || !passiveLantern.HasToggleableLamp())
        {
            return;
        }

        Items selectedItem = inventory != null ? inventory.GetSelectedItem() : null;
        if (selectedItem is ActiveItem)
        {
            return;
        }

        passiveLantern.ToggleLamp();
    }

    private void HandleFlashlightBatteryRecharge()
    {
        if (!Input.GetKeyDown(KeyCode.R) || flashlightEnergy == null)
        {
            return;
        }

        flashlightEnergy.TryRechargeFromInventoryBattery();
    }

    private void HandleEquipmentUse()
    {
        if (!Input.GetKeyDown(KeyCode.Mouse0))
        {
            return;
        }

        if (inventory != null && inventory.IsCamcorderHeld() && inventory.HasCamcorder)
        {
            Equipment camcorder = inventory.GetCamcorder();
            if (equipmentView != null
                && equipmentView.TryGetCurrentView(camcorder, out GameObject camcorderView))
            {
                CamcorderController camcorderController =
                    camcorderView.GetComponentInChildren<CamcorderController>(true);
                if (camcorderController != null)
                {
                    if (!camcorderController.IsViewfinderActive
                        && camcorderEnergy != null
                        && !camcorderEnergy.CanOpenViewfinder)
                    {
                        Debug.LogWarning("[Camcorder] 배터리가 방전되어 뷰파인더를 켤 수 없습니다.");
                        return;
                    }

                    camcorderController.ToggleRaise();
                    return;
                }
            }
        }

        Items selectedItem = inventory != null ? inventory.GetSelectedItem() : null;
        if (selectedItem is not Equipment equipment)
        {
            return;
        }

        if (inventory != null && inventory.IsCamcorderItem(equipment))
        {
            return;
        }

        if (smartPhoneToggle != null && smartPhoneToggle.IsSmartPhoneItem(equipment))
        {
            if (equipmentView != null)
            {
                equipmentView.HideCurrent();
            }

            smartPhoneToggle.ToggleSelectedSmartPhone();
            return;
        }

        if (equipment.useMode == EquipmentUseMode.PassiveOnSelect)
        {
            return;
        }

        if (equipment.useMode == EquipmentUseMode.ToggleOnClick && TryToggleEquipmentLight(equipment))
        {
            return;
        }

        equipment.Use(player);
    }

    private bool TryToggleEquipmentLight(Equipment equipment)
    {
        if (equipmentView == null || !equipmentView.TryGetCurrentView(equipment, out GameObject currentView))
        {
            return false;
        }

        Light light = currentView.GetComponentInChildren<Light>(true);
        if (light == null)
        {
            return false;
        }

        bool turningOn = !light.enabled;

        if (turningOn
            && flashlightEnergy != null
            && flashlightEnergy.IsFlashlightEquipment(equipment)
            && !flashlightEnergy.CanTurnLightOn)
        {
            Debug.LogWarning("[Flashlight] 배터리가 없어 켤 수 없습니다. 건전지로 R키 충전.");
            return true;
        }

        light.enabled = turningOn;

        if (flashlightEnergy != null && flashlightEnergy.IsFlashlightEquipment(equipment))
        {
            flashlightEnergy.PlayToggleSound(turningOn, currentView.transform.position);
            flashlightEnergy.OnFlashlightToggled(turningOn);
        }

        return true;
    }

    private void HandleActiveItemHoldUse()
    {
        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            Items selectedItem = inventory != null ? inventory.GetSelectedItem() : null;
            holdingActiveItem = selectedItem as ActiveItem;
            holdTimer = 0f;
        }

        if (!Input.GetKey(KeyCode.Mouse1))
        {
            ResetActiveHold();
            return;
        }

        if (holdingActiveItem == null)
        {
            return;
        }

        Items currentItem = inventory != null ? inventory.GetSelectedItem() : null;
        if (currentItem != holdingActiveItem)
        {
            ResetActiveHold();
            return;
        }

        holdTimer += Time.deltaTime;
        if (holdTimer < activeItemHoldTime)
        {
            return;
        }

        if (holdingActiveItem.deployPrefab != null)
        {
            Debug.LogWarning($"[Item Use] {holdingActiveItem.itemName}은(는) 설치형 아이템이라 아직 사용하지 않습니다.");
            ResetActiveHold();
            return;
        }

        holdingActiveItem.Use(player);
        ResetActiveHold();
    }

    private void ResetActiveHold()
    {
        holdingActiveItem = null;
        holdTimer = 0f;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
