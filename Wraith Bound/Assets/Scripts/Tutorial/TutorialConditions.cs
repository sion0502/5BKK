using UnityEngine;

/// <summary>
/// 튜토리얼 단계·팁의 완료 조건 판정.
/// 기존 플레이어 스크립트의 공개 상태값을 읽기만 하고, 아무것도 변경하지 않습니다.
/// </summary>
public class TutorialConditions
{
    private const string FlashlightResourcePath = "ItemDatas/Equipment/FlashLight";
    private const string BatteryResourcePath = "ItemDatas/Active/Battery";

    private readonly GameObject player;
    private readonly PlayerController playerController;
    private readonly InventoryManager inventory;
    private readonly EquipmentViewController equipmentView;
    private readonly CamcorderEnergyController camcorderEnergy;
    private readonly PlayerHidingController hidingController;
    private readonly Camera playerCamera;
    private readonly Equipment flashlightItem;
    private readonly ActiveItem batteryItem;

    private TutorialStep trackedStep;
    private float progressAmount;
    private Vector3 lastPosition;
    private int lastSelectedSlot;
    private bool hasSwitchedSlot;

    public Equipment FlashlightItem => flashlightItem;
    public bool IsValid => player != null && playerController != null && inventory != null;

    public TutorialConditions(GameObject player)
    {
        this.player = player;
        if (player == null)
        {
            return;
        }

        playerController = player.GetComponent<PlayerController>();
        inventory = player.GetComponent<InventoryManager>();
        equipmentView = player.GetComponent<EquipmentViewController>();
        camcorderEnergy = player.GetComponent<CamcorderEnergyController>();
        hidingController = player.GetComponent<PlayerHidingController>();
        playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        flashlightItem = Resources.Load<Equipment>(FlashlightResourcePath);
        batteryItem = Resources.Load<ActiveItem>(BatteryResourcePath);
    }

    /// <summary>새 단계 시작 시 누적값을 초기화합니다.</summary>
    public void BeginStep(TutorialStep step)
    {
        trackedStep = step;
        progressAmount = 0f;
        lastPosition = player != null ? player.transform.position : Vector3.zero;
        lastSelectedSlot = inventory != null ? inventory.selectedSlotIndex : 0;
        hasSwitchedSlot = false;
    }

    /// <summary>현재 단계 진행도(0~1). 1이면 완료.</summary>
    public float EvaluateStep(TutorialStep step)
    {
        if (step != trackedStep)
        {
            BeginStep(step);
        }

        switch (step.condition)
        {
            case TutorialCondition.Move:
                return Accumulate(GetMovedDistance(), step.requiredAmount);

            case TutorialCondition.Sprint:
                return Accumulate(playerController.isRun ? Time.deltaTime : 0f, step.requiredAmount);

            case TutorialCondition.Crouch:
                return Accumulate(playerController.isCrouching ? Time.deltaTime : 0f, step.requiredAmount);

            case TutorialCondition.CamcorderHold:
                return ToProgress(inventory.IsCamcorderHeld());

            case TutorialCondition.CamcorderViewfinder:
                return ToProgress(IsViewfinderActive());

            case TutorialCondition.CamcorderZoom:
                bool zoomed = IsViewfinderActive() && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
                return Accumulate(zoomed ? 1f : 0f, step.requiredAmount);

            case TutorialCondition.CamcorderStow:
                return ToProgress(!inventory.IsCamcorderHeld());

            case TutorialCondition.PickUpFlashlight:
                return ToProgress(HasFlashlight());

            case TutorialCondition.SelectFlashlight:
                // 이미 손전등이 선택돼 있어도, 숫자키·휠로 한 번은 직접 전환해야 완료
                TrackSlotSwitch();
                return ToProgress(hasSwitchedSlot && IsFlashlightSelected());

            case TutorialCondition.FlashlightOn:
                return ToProgress(IsFlashlightOn());

            case TutorialCondition.ExpandInventory:
                return ToProgress(Input.GetKeyDown(KeyCode.Tab));
        }

        return 1f;
    }

    public bool IsTipTriggered(TutorialTipTrigger trigger)
    {
        switch (trigger)
        {
            case TutorialTipTrigger.AcquireUsableItem:
                return HasUsableActiveItem();

            case TutorialTipTrigger.AcquireBattery:
                return batteryItem != null && inventory.CountItem(batteryItem) > 0;

            case TutorialTipTrigger.AimHidingSpot:
                return IsAimingHidingSpot();
        }

        return false;
    }

    private float Accumulate(float delta, float required)
    {
        progressAmount += delta;
        return required > 0f ? Mathf.Clamp01(progressAmount / required) : (progressAmount > 0f ? 1f : 0f);
    }

    private static float ToProgress(bool done) => done ? 1f : 0f;

    private float GetMovedDistance()
    {
        Vector3 current = player.transform.position;
        Vector3 delta = current - lastPosition;
        lastPosition = current;

        // 순간이동(낙하 보정 등)은 이동으로 치지 않음
        delta.y = 0f;
        float distance = delta.magnitude;
        return playerController.enabled && distance < 2f ? distance : 0f;
    }

    private void TrackSlotSwitch()
    {
        int current = inventory.selectedSlotIndex;
        if (current != lastSelectedSlot)
        {
            hasSwitchedSlot = true;
            lastSelectedSlot = current;
        }

        // 같은 슬롯 번호를 다시 누르거나, 슬롯이 하나뿐이라 인덱스가 안 바뀌는 경우도 전환 시도로 인정
        if (!inventory.IsCamcorderHeld() && (IsNumberKeyDown() || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f))
        {
            hasSwitchedSlot = true;
        }
    }

    private static bool IsNumberKeyDown()
    {
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsViewfinderActive()
    {
        return camcorderEnergy != null && camcorderEnergy.IsViewfinderActive;
    }

    private bool HasFlashlight()
    {
        return flashlightItem != null && inventory.CountItem(flashlightItem) > 0;
    }

    private bool IsFlashlightSelected()
    {
        return flashlightItem != null
            && !inventory.IsCamcorderHeld()
            && inventory.GetSelectedItem() == flashlightItem;
    }

    private bool IsFlashlightOn()
    {
        return IsFlashlightSelected()
            && equipmentView != null
            && equipmentView.TryGetEquipmentLight(flashlightItem, out Light light)
            && light != null
            && light.enabled;
    }

    private bool HasUsableActiveItem()
    {
        foreach (InventorySlot slot in inventory.slots)
        {
            if (slot != null
                && slot.item is ActiveItem active
                && active != batteryItem
                && (active.effectType != ActiveEffectType.None || active.isThrowable))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAimingHidingSpot()
    {
        if (hidingController == null || hidingController.isHiding || playerCamera == null)
        {
            return false;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        return Physics.Raycast(
                ray,
                out RaycastHit hit,
                hidingController.interactionDistance,
                hidingController.interactableLayer,
                QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<HidingSpot>() != null;
    }
}
