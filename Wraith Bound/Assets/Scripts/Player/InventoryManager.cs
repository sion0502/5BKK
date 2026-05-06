using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

public class InventoryManager : MonoBehaviour
{
    [SerializeField]
    public int capacity;
    public List<InventorySlot> slots = new List<InventorySlot>();

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

        int maxCount = Mathf.Max(1, itemToAdd.maxCount);

        // 1. 기존 슬롯 확인 및 수량 추가
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

        // 2. 새 슬롯 추가 조건 검사
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

        // 3. 새 슬롯 생성 및 추가
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
                    // 수량이 0 이하가 되면 슬롯에서 제거
                    slots.RemoveAt(i);
                }
            }
        }
    }


    // 현재 선택된 슬롯 번호를 추적하고, 휠/숫자키 입력을 처리하는 로직을 추가
    [Header("Selection")]
    public int selectedSlotIndex = 0; // 현재 선택된 슬롯 인덱스 (0~8 등)
    public GameObject currentHeldItem; // 현재 손에 생성된 아이템 오브젝트
    public Transform holdPos; // PickUpScript에서 가져올 위치

    void Update()
    {
        HandleSlotInput();
    }

    private void HandleSlotInput()
    {
        // 1. 마우스 휠로 슬롯 전환
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (wheel > 0f) ChangeSelectedSlot(-1);
        else if (wheel < 0f) ChangeSelectedSlot(1);

        // 2. 숫자키(1~9)로 슬롯 전환
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

    // InventoryManager.cs 내부

    public void UpdateHeldItem()
    {
        // 1. 이전 아이템 삭제
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }

        // 2. 현재 선택된 인덱스에 아이템이 있는지 검사
        if (selectedSlotIndex < slots.Count && slots[selectedSlotIndex].item != null)
        {
            Items item = slots[selectedSlotIndex].item;

            // 3. 손에 보여주는 설정이 켜져 있고 프리팹이 있다면 생성
            if (item.showInHand && item.itemPrefab != null)
            {
                // holdPos는 MainCamera 자식의 HoldPosition 오브젝트여야 함
                currentHeldItem = Instantiate(item.itemPrefab, holdPos);

                // 위치/회전 초기화 (부모인 holdPos의 위치를 따름)
                currentHeldItem.transform.localPosition = Vector3.zero;
                currentHeldItem.transform.localRotation = Quaternion.identity;

                // 4. PickupCam에서만 보이도록 레이어 설정
                // (레이어 이름이 "Weapon"인지 "PickupItem"인지 확인 필요)
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

    private void DebugPrintSelectedSlot() // 디버그 로그
    {
        InventorySlot slot = GetSelectedSlot();

        if (slot == null || slot.item == null)
        {
            Debug.Log($"[Inventory] 선택 슬롯 {selectedSlotIndex}: 비어 있음");
            return;
        }

        Debug.Log($"[Inventory] 선택 슬롯 {selectedSlotIndex}: {slot.item.itemName} x{slot.amount}");
    }

    public void DebugPrintInventory() // 디버그 로그
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
