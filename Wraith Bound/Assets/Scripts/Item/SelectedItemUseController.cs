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
    private ActiveItem holdingActiveItem;
    private float holdTimer;

    void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        player = GetComponent<PlayerController>();
        smartPhoneToggle = GetComponent<SmartPhoneHolderToggle>();
        equipmentView = GetComponent<EquipmentViewController>();
    }

    void Update()
    {
        if (IsPointerOverUI())
        {
            ResetActiveHold();
            return;
        }

        HandleEquipmentUse();
        HandleActiveItemHoldUse();
    }

    private void HandleEquipmentUse()
    {
        if (!Input.GetKeyDown(KeyCode.Mouse0))
        {
            return;
        }

        Items selectedItem = inventory != null ? inventory.GetSelectedItem() : null;
        if (selectedItem is not Equipment equipment)
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

        light.enabled = !light.enabled;
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
