using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

public class InventoryManager : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int baseCapacity = 3;
    [SerializeField] private int maxCapacity = 7;
    [SerializeField] private int bonusCapacity = 0;

    [Header("Passive Slot")]
    [SerializeField] private PassiveItem passiveSlot;

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

    public List<InventorySlot> slots = new List<InventorySlot>();

    public bool TryAcquireItem(Items itemToAcquire, int amount = 1)
    {
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

        // PassiveItem은 일반 인벤토리 slots를 사용하지 않고 passiveSlot으로만 들어갑니다.
        if (itemToAcquire is PassiveItem passiveItem)
        {
            return AddPassiveItem(passiveItem);
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
            return AddItem(itemToAcquire, amount);
        }

        // 클래스 판별이 애매한 경우에도 ItemType이 Active 또는 Equip이면 기존 흐름을 유지하기 위해 일반 아이템으로 처리합니다.
        if (itemToAcquire.type == ItemType.Active || itemToAcquire.type == ItemType.Equip)
        {
            return AddItem(itemToAcquire, amount);
        }

        Debug.LogWarning($"[Inventory] 획득할 수 없는 아이템 타입입니다: {itemToAcquire.itemName}");
        return false;
    }

    public bool AddItem(Items itemToAdd, int amount = 1)
    {
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

        // 새 슬롯을 만들어야 하는 경우 capacity를 넘지 않는지 확인합니다.
        if (slots.Count >= capacity)
        {
            Debug.LogWarning("[Inventory] 인벤토리 슬롯이 가득 찼습니다.");
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

    public void RemoveItem(Items itemToRemove, int amount = 1)
    {
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

    void Update()
    {
        HandleSlotInput();
    }

    private void HandleSlotInput()
    {
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

    private void ChangeSelectedSlot(int direction)
    {
        if (capacity <= 0)
        {
            return;
        }

        selectedSlotIndex += direction;

        if (selectedSlotIndex < 0)
        {
            selectedSlotIndex = capacity - 1;
        }
        else if (selectedSlotIndex >= capacity)
        {
            selectedSlotIndex = 0;
        }

        DebugPrintSelectedSlot();
    }

    private void SetSelectedSlot(int index)
    {
        if (index < 0 || index >= capacity)
        {
            return;
        }

        selectedSlotIndex = index;
        DebugPrintSelectedSlot();
    }

    public void UpdateHeldItem()
    {
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }

        if (selectedSlotIndex < slots.Count && slots[selectedSlotIndex].item != null)
        {
            Items item = slots[selectedSlotIndex].item;

            // showInHand가 켜진 장비 아이템만 손 위치에 프리팹을 생성합니다.
            if (item.showInHand && item.itemPrefab != null)
            {
                currentHeldItem = Instantiate(item.itemPrefab, holdPos);

                currentHeldItem.transform.localPosition = Vector3.zero;
                currentHeldItem.transform.localRotation = Quaternion.identity;

                SetLayerRecursively(currentHeldItem, LayerMask.NameToLayer("PickupItem"));
            }
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

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
