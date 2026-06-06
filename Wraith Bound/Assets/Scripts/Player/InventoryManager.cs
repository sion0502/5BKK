using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

public enum HeldItemSource
{
    QuickSlot,
    Camcorder
}

public class InventoryManager : MonoBehaviour
{
    private const string CamcorderResourcePath = "ItemDatas/Equipment/Camcorder";

    [Header("Capacity")]
    [SerializeField] private int baseCapacity = 3;
    [SerializeField] private int maxCapacity = 7;
    [SerializeField] private int bonusCapacity = 0;

    [Header("Camcorder Slot (3칸 인벤과 별도)")]
    [SerializeField] private Equipment camcorderEquipment;
    [SerializeField] private bool grantCamcorderOnStart = true;
    [SerializeField] private KeyCode camcorderSelectKey = KeyCode.F;

    [Header("Passive Slot")]
    [SerializeField] private PassiveItem passiveSlot;

    private bool ownsCamcorder;
    private HeldItemSource heldSource = HeldItemSource.QuickSlot;

    public HeldItemSource HeldSource => heldSource;
    public bool HasCamcorder => ownsCamcorder && camcorderEquipment != null;
    public Equipment GetCamcorder() => HasCamcorder ? camcorderEquipment : null;
    public bool IsCamcorderHeld() => heldSource == HeldItemSource.Camcorder && HasCamcorder;

    public int capacity
    {
        get
        {
            return Mathf.Clamp(baseCapacity + bonusCapacity, baseCapacity, maxCapacity);
        }
    }

    public void AddCapacityBonus(int value)
    {
        if (value <= 0)
        {
            return;
        }

        int beforeCapacity = capacity;
        bonusCapacity += value;
        bonusCapacity = Mathf.Clamp(bonusCapacity, 0, maxCapacity - baseCapacity);

        Debug.Log($"[Inventory] 인벤토리 칸 증가: {beforeCapacity} >> {capacity}");
    }

    public bool RemoveCapacityBonus(int value)
    {
        if (value <= 0)
        {
            return false;
        }

        int beforeCapacity = capacity;
        int targetBonus = Mathf.Clamp(bonusCapacity - value, 0, maxCapacity - baseCapacity);
        int targetCapacity = Mathf.Clamp(baseCapacity + targetBonus, baseCapacity, maxCapacity);

        // 감소 후 capacity보다 현재 슬롯 수가 많으면 아이템이 잘릴 수 있으므로 감소를 막습니다.
        if (slots.Count > targetCapacity)
        {
            Debug.LogWarning($"[Inventory] 슬롯에 아이템이 있어서 인벤토리 칸을 줄일 수 없습니다. 현재 슬롯 수: {slots.Count}, 목표 칸 수: {targetCapacity}");
            return false;
        }

        bonusCapacity = targetBonus;

        // 현재 선택 슬롯이 capacity 밖으로 나가면 마지막 슬롯으로 보정합니다.
        if (selectedSlotIndex >= capacity)
        {
            selectedSlotIndex = capacity - 1;
        }

        Debug.Log($"[Inventory] 인벤토리 칸 감소: {beforeCapacity} → {capacity}");
        return true;
    }

    public void DebugPrintCapacity()
    {
        Debug.Log($"[Inventory] Capacity: {capacity} / Max {maxCapacity} | Base {baseCapacity}, Bonus {bonusCapacity}");
    }

    public PassiveItem GetPassiveItem()
    {
        return passiveSlot;
    }

    private void ApplyPassiveEffect(PassiveItem passiveItem)
    {
        if (passiveItem == null)
        {
            return;
        }

        // 현재 1단계에서는 인벤토리 확장 효과(extraSlots)만 먼저 적용합니다.
        if (passiveItem.extraSlots > 0)
        {
            AddCapacityBonus(passiveItem.extraSlots);
        }

        Debug.Log($"[Inventory] 패시브 효과 적용: {passiveItem.itemName}");
    }

    public bool AddPassiveItem(PassiveItem passiveItem)
    {
        if (passiveItem == null)
        {
            Debug.LogWarning("[Inventory] 추가하려는 패시브 아이템이 null입니다.");
            return false;
        }

        if (passiveSlot != null)
        {
            Debug.LogWarning($"[Inventory] 이미 패시브 슬롯에 {passiveSlot.itemName}이(가) 있습니다.");
            return false;
        }

        // 패시브 아이템은 일반 slots에 넣지 않고 전용 passiveSlot에만 보관합니다.
        passiveSlot = passiveItem;
        ApplyPassiveEffect(passiveItem);

        Debug.Log($"[Inventory] 패시브 아이템 획득: {passiveItem.itemName}");
        DebugPrintPassiveSlot();

        return true;
    }

    public void DebugPrintPassiveSlot()
    {
        if (passiveSlot == null)
        {
            Debug.Log("[Inventory] 패시브 슬롯: 비어 있음");
            return;
        }

        Debug.Log($"[Inventory] 패시브 슬롯: {passiveSlot.itemName}");
    }

    private bool RevertPassiveEffects(PassiveItem passiveItem)
    {
        if (passiveItem == null)
        {
            return true;
        }

        if (passiveItem.extraSlots > 0)
        {
            return RemoveCapacityBonus(passiveItem.extraSlots);
        }

        return true;
    }

    /// <summary>
    /// 패시브 슬롯이 이미 차 있을 때만: 기존 패시브와 새 패시브를 교환합니다. 일반 슬롯과는 섞지 않습니다.
    /// </summary>
    private bool TrySwapPassiveWithIncoming(PassiveItem newPassive, out Items droppedFromInventory, out int droppedAmount)
    {
        droppedFromInventory = null;
        droppedAmount = 0;

        PassiveItem oldPassive = passiveSlot;
        if (oldPassive == null)
        {
            return AddPassiveItem(newPassive);
        }

        if (!RevertPassiveEffects(oldPassive))
        {
            Debug.LogWarning("[Inventory] 패시브 교체 불가: 기존 패시브의 인벤 확장을 되돌릴 수 없습니다(슬롯이 너무 많이 찼습니다).");
            return false;
        }

        passiveSlot = newPassive;
        ApplyPassiveEffect(newPassive);

        droppedFromInventory = oldPassive;
        droppedAmount = 1;

        Debug.Log($"[Inventory] 패시브 교체: {oldPassive.itemName} -> {newPassive.itemName}");
        DebugPrintPassiveSlot();

        return true;
    }

    public List<InventorySlot> slots = new List<InventorySlot>();

    public bool TryAcquireItem(Items itemToAcquire, int amount = 1)
    {
        return TryAcquireItem(itemToAcquire, amount, out _, out _);
    }

    public bool TryAcquireItem(Items itemToAcquire, int amount, out Items droppedFromInventory, out int droppedInventoryAmount)
    {
        droppedFromInventory = null;
        droppedInventoryAmount = 0;

        if (itemToAcquire == null)
        {
            Debug.LogWarning("[Inventory] 획득하려는 아이템이 null입니다.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning("[Inventory] 획득 수량은 1 이상이어야 합니다.");
            return false;
        }

        if (IsCamcorderItem(itemToAcquire))
        {
            return TryAcquireCamcorder(out droppedFromInventory, out droppedInventoryAmount);
        }

        // PassiveItem은 일반 인벤토리 slots를 사용하지 않고 passiveSlot으로만 들어갑니다.
        if (itemToAcquire is PassiveItem passiveItem)
        {
            if (passiveSlot == null)
            {
                return AddPassiveItem(passiveItem);
            }

            return TrySwapPassiveWithIncoming(passiveItem, out droppedFromInventory, out droppedInventoryAmount);
        }

        // item.type이 Passive로 설정되어 있는데 실제 클래스가 PassiveItem이 아니면 잘못된 데이터로 보고 실패시킵니다.
        if (itemToAcquire.type == ItemType.Passive)
        {
            Debug.LogWarning($"[Inventory] {itemToAcquire.itemName}은(는) Passive 타입이지만 PassiveItem 클래스가 아닙니다.");
            return false;
        }

        // ActiveItem과 Equipment만 일반 인벤토리 slots에 들어갈 수 있습니다.
        if (itemToAcquire is ActiveItem || itemToAcquire is Equipment)
        {
            return AddItem(itemToAcquire, amount, out droppedFromInventory, out droppedInventoryAmount);
        }

        // 클래스 판별이 애매한 경우에도 ItemType이 Active 또는 Equip이면 기존 흐름을 유지하기 위해 일반 아이템으로 처리합니다.
        if (itemToAcquire.type == ItemType.Active || itemToAcquire.type == ItemType.Equip)
        {
            return AddItem(itemToAcquire, amount, out droppedFromInventory, out droppedInventoryAmount);
        }

        Debug.LogWarning($"[Inventory] 획득할 수 없는 아이템 타입입니다: {itemToAcquire.itemName}");
        return false;
    }

    public bool AddItem(Items itemToAdd, int amount = 1)
    {
        return AddItem(itemToAdd, amount, out _, out _);
    }

    public bool AddItem(Items itemToAdd, int amount, out Items droppedItem, out int droppedAmount)
    {
        droppedItem = null;
        droppedAmount = 0;

        if (itemToAdd == null)
        {
            Debug.LogWarning("[Inventory] 추가하려는 아이템이 null입니다.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning("[Inventory] 추가 수량은 1 이상이어야 합니다.");
            return false;
        }

        if (IsCamcorderItem(itemToAdd))
        {
            Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 캠코더 전용 슬롯 아이템이라 일반 인벤토리에 넣을 수 없습니다.");
            return TryAcquireCamcorder(out droppedItem, out droppedAmount);
        }

        // PassiveItem은 일반 slots에 들어가면 안 되므로 여기서 한 번 더 방어합니다.
        if (itemToAdd is PassiveItem || itemToAdd.type == ItemType.Passive)
        {
            Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 패시브 아이템이라 일반 인벤토리 슬롯에 추가할 수 없습니다.");
            return false;
        }

        // 일반 slots에는 ActiveItem과 Equipment만 들어갈 수 있도록 제한합니다.
        if (!(itemToAdd is ActiveItem) && !(itemToAdd is Equipment) && itemToAdd.type != ItemType.Active && itemToAdd.type != ItemType.Equip)
        {
            Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 일반 인벤토리에 추가할 수 없는 아이템입니다.");
            return false;
        }

        int maxCount = Mathf.Max(1, itemToAdd.maxCount);

        // 기존 슬롯에 같은 아이템이 있으면 새 슬롯을 쓰지 않고 수량만 증가시킵니다.
        foreach (var slot in slots)
        {
            if (slot.item == itemToAdd)
            {
                if (slot.amount >= maxCount)
                {
                    Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 최대 소지 수량입니다. {slot.amount}/{maxCount}");
                    return false;
                }

                int newAmount = slot.amount + amount;
                if (newAmount > maxCount)
                {
                    Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 최대 {maxCount}개까지만 소지할 수 있습니다.");
                    return false;
                }

                slot.AddAmount(amount);
                return true;
            }
        }

        // 새 슬롯이 필요할 때 가득 찼으면, 현재 선택 슬롯과 교체합니다(같은 종류 스택 여유는 위에서 이미 처리됨).
        if (slots.Count >= capacity)
        {
            if (TrySwapWithSelectedSlot(itemToAdd, amount, out droppedItem, out droppedAmount))
            {
                return true;
            }

            Debug.LogWarning("[Inventory] 인벤토리 슬롯이 가득 찼고, 선택 슬롯과 교체할 수 없습니다.");
            return false;
        }

        if (amount > maxCount)
        {
            Debug.LogWarning($"[Inventory] {itemToAdd.itemName}은(는) 최대 {maxCount}개까지만 소지할 수 있습니다.");
            return false;
        }

        slots.Add(new InventorySlot(itemToAdd, amount));
        return true;
    }

    /// <summary>
    /// 인벤이 가득 찼을 때, 현재 선택된 슬롯 내용을 바닥으로 보내고 새 아이템으로 덮어씁니다.
    /// </summary>
    private bool TrySwapWithSelectedSlot(Items newItem, int newAmount, out Items droppedItem, out int droppedAmount)
    {
        droppedItem = null;
        droppedAmount = 0;

        if (selectedSlotIndex < 0 || selectedSlotIndex >= slots.Count)
        {
            return false;
        }

        InventorySlot slot = slots[selectedSlotIndex];
        if (slot == null || slot.item == null || slot.amount < 1)
        {
            return false;
        }

        int maxNew = Mathf.Max(1, newItem.maxCount);
        if (newAmount > maxNew)
        {
            return false;
        }

        // 같은 아이템인데 스택 여유가 있었다면 위 루프에서 이미 처리됨. 여기까지 오면 교체만 가능.
        droppedItem = slot.item;
        droppedAmount = slot.amount;

        slot.item = newItem;
        slot.amount = newAmount;

        OnSelectedSlotChanged();

        Debug.Log($"[Inventory] 선택 슬롯 {selectedSlotIndex} 교체: {droppedItem.itemName} x{droppedAmount} -> {newItem.itemName} x{newAmount}");

        return true;
    }

    public int CountItem(Items item)
    {
        if (item == null)
        {
            return 0;
        }

        int total = 0;
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == item)
            {
                total += slot.amount;
            }
        }

        return total;
    }

    public bool TryConsumeItem(Items item, int amount = 1)
    {
        if (item == null || amount <= 0)
        {
            return false;
        }

        if (CountItem(item) < amount)
        {
            return false;
        }

        RemoveItem(item, amount);
        return true;
    }

    public void RemoveItem(Items itemToRemove, int amount = 1)
    {
        if (IsCamcorderItem(itemToRemove))
        {
            Debug.LogWarning("[Inventory] 캠코더는 제거할 수 없습니다.");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item == itemToRemove)
            {
                if (slots[i].amount > amount)
                {
                    slots[i].RemoveAmount(amount);
                }
                else
                {
                    // 수량이 0 이하가 되면 슬롯에서 제거합니다.
                    slots.RemoveAt(i);
                }
            }
        }
    }

    [Header("Selection")]
    public int selectedSlotIndex = 0;
    public GameObject currentHeldItem;
    public Transform holdPos;

    private EquipmentViewController equipmentView;
    private SmartPhoneHolderToggle smartPhoneToggle;
    private CamcorderEnergyController camcorderEnergy;

    void Awake()
    {
        equipmentView = GetComponent<EquipmentViewController>();
        smartPhoneToggle = GetComponent<SmartPhoneHolderToggle>();
        camcorderEnergy = GetComponent<CamcorderEnergyController>();
        ResolveCamcorderReference();
    }

    void Start()
    {
        if (grantCamcorderOnStart)
        {
            GrantCamcorder();
        }

        UpdateHeldItem();
    }

    void Update()
    {
        HandleSlotInput();
    }

    public bool IsCamcorderItem(Items item)
    {
        return item != null && camcorderEquipment != null && item == camcorderEquipment;
    }

    private void ResolveCamcorderReference()
    {
        if (camcorderEquipment != null)
        {
            return;
        }

        camcorderEquipment = Resources.Load<Equipment>(CamcorderResourcePath);
        if (camcorderEquipment == null)
        {
            Debug.LogWarning($"[Inventory] 캠코더 Equipment를 찾을 수 없습니다: Resources/{CamcorderResourcePath}");
        }
    }

    private bool GrantCamcorder()
    {
        if (camcorderEquipment == null)
        {
            return false;
        }

        ownsCamcorder = true;
        return true;
    }

    private bool TryAcquireCamcorder(out Items droppedFromInventory, out int droppedInventoryAmount)
    {
        droppedFromInventory = null;
        droppedInventoryAmount = 0;

        if (camcorderEquipment == null)
        {
            Debug.LogWarning("[Inventory] 캠코더 데이터가 없어 획득할 수 없습니다.");
            return false;
        }

        if (ownsCamcorder)
        {
            Debug.Log("[Inventory] 이미 캠코더를 소지하고 있습니다.");
            return true;
        }

        ownsCamcorder = true;
        Debug.Log($"[Inventory] 캠코더 전용 슬롯 획득: {camcorderEquipment.itemName}");
        return true;
    }

    /// <summary>F키: 캠코더를 꺼냈다가 다시 누르면 집어넣기(퀵슬롯 표시로 복귀).</summary>
    public void ToggleCamcorderHeld()
    {
        if (!HasCamcorder)
        {
            return;
        }

        if (IsCamcorderHeld())
        {
            SelectQuickSlot();
        }
        else
        {
            heldSource = HeldItemSource.Camcorder;
        }

        UpdateHeldItem();
    }

    public void SelectCamcorder() => ToggleCamcorderHeld();

    private void SelectQuickSlot()
    {
        heldSource = HeldItemSource.QuickSlot;
    }

    /// <summary>지금 손에 표시·사용할 장비. F(캠코더) 또는 퀵슬롯 선택 기준.</summary>
    public Equipment GetActiveHeldEquipment()
    {
        if (IsCamcorderHeld())
        {
            return GetCamcorder();
        }

        Items item = GetSelectedItem();
        return item as Equipment;
    }

    private void HandleSlotInput()
    {
        if (Input.GetKeyDown(camcorderSelectKey))
        {
            ToggleCamcorderHeld();
        }

        if (IsCamcorderViewfinderActive())
        {
            return;
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (wheel > 0f) ChangeSelectedSlot(-1);
        else if (wheel < 0f) ChangeSelectedSlot(1);

        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SetSelectedSlot(i);
                break;
            }
        }
    }

    /// <summary>뷰파인더 ON 중 퀵슬롯·스크롤 전환 차단 (스크롤은 캠코더 줌 전용).</summary>
    public bool IsCamcorderViewfinderActive()
    {
        if (camcorderEnergy == null)
            camcorderEnergy = GetComponent<CamcorderEnergyController>();

        return camcorderEnergy != null && camcorderEnergy.IsViewfinderActive;
    }

    private void ChangeSelectedSlot(int direction)
    {
        if (IsCamcorderViewfinderActive())
        {
            return;
        }

        if (capacity <= 0)
        {
            return;
        }

        SelectQuickSlot();
        selectedSlotIndex += direction;

        if (selectedSlotIndex < 0)
        {
            selectedSlotIndex = capacity - 1;
        }
        else if (selectedSlotIndex >= capacity)
        {
            selectedSlotIndex = 0;
        }

        OnSelectedSlotChanged();
    }

    public void SetSelectedSlot(int index)
    {
        if (IsCamcorderViewfinderActive())
        {
            return;
        }

        if (index < 0 || index >= capacity)
        {
            return;
        }

        SelectQuickSlot();
        selectedSlotIndex = index;
        OnSelectedSlotChanged();
    }

    private void OnSelectedSlotChanged()
    {
        DebugPrintSelectedSlot();
        UpdateHeldItem();
    }

    public void UpdateHeldItem()
    {
        // 현재 슬롯이 폰이 아니면 폰 UI를 즉시 강제 숨김 (Update() 타이밍 문제 방지)
        if (smartPhoneToggle != null)
        {
            smartPhoneToggle.HideIfPhoneNotSelected();
        }

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }

        if (IsCamcorderHeld())
        {
            ShowHeldEquipment(GetCamcorder());
            return;
        }

        Items item = GetSelectedItem();
        if (item == null)
        {
            if (equipmentView != null)
            {
                equipmentView.HideCurrent();
            }
            return;
        }

        if (item is Equipment equipment)
        {
            ShowHeldEquipment(equipment);
            return;
        }

        if (equipmentView != null)
        {
            equipmentView.HideCurrent();
        }

        // 장비가 아닌 아이템만 기존 holdPos 표시 경로를 사용합니다.
        if (item.showInHand && item.itemPrefab != null)
        {
            if (holdPos == null)
            {
                Debug.LogWarning("[Inventory] holdPos가 비어 있어 손 아이템을 표시할 수 없습니다.");
                return;
            }

            currentHeldItem = Instantiate(item.itemPrefab, holdPos);

            currentHeldItem.transform.localPosition = Vector3.zero;
            currentHeldItem.transform.localRotation = Quaternion.identity;

            SetLayerRecursively(currentHeldItem, LayerMask.NameToLayer("PickupItem"));
            EnsureHeldItemSway(currentHeldItem);
        }
    }

    private void ShowHeldEquipment(Equipment equipment)
    {
        if (equipment == null)
        {
            if (equipmentView != null)
            {
                equipmentView.HideCurrent();
            }
            return;
        }

        if (smartPhoneToggle != null && smartPhoneToggle.IsSmartPhoneItem(equipment))
        {
            if (equipmentView != null)
            {
                equipmentView.HideCurrent();
            }
            return;
        }

        if (equipmentView != null)
        {
            equipmentView.ShowEquipment(equipment);
        }
    }

    public InventorySlot GetSelectedSlot()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= slots.Count)
        {
            return null;
        }

        return slots[selectedSlotIndex];
    }

    public Items GetSelectedItem()
    {
        InventorySlot slot = GetSelectedSlot();

        if (slot == null)
        {
            return null;
        }

        return slot.item;
    }

    private void DebugPrintSelectedSlot()
    {
        InventorySlot slot = GetSelectedSlot();

        if (slot == null || slot.item == null)
        {
            Debug.Log($"[Inventory] 선택 슬롯 {selectedSlotIndex}: 비어 있음");
            return;
        }

        Debug.Log($"[Inventory] 선택 슬롯 {selectedSlotIndex}: {slot.item.itemName} x{slot.amount}");
    }

    public void DebugPrintInventory()
    {
        Debug.Log("[Inventory] 현재 슬롯 목록");

        if (slots.Count == 0)
        {
            Debug.Log("[Inventory] 비어 있음");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            string itemName = slots[i].item != null ? slots[i].item.itemName : "NULL";
            Debug.Log($"{i}: {itemName} x{slots[i].amount}");
        }
    }

    private void EnsureHeldItemSway(GameObject itemObject)
    {
        if (itemObject != null && itemObject.GetComponent<HeldItemSway>() == null)
        {
            itemObject.AddComponent<HeldItemSway>();
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
