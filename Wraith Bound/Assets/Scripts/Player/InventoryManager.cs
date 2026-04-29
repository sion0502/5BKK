using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;

public class InventroyManager : MonoBehaviour
{
    [SerializeField]
    public int capacity;
    public List<InventorySlot> slots = new List<InventorySlot>();

    public bool AddItem(Items itemToAdd, int amount = 1)
    {
        // 이미 인벤토리에 있는 아이템이고 스택 가능한 아이템인지 확인
        foreach (var slot in slots)
        {
            if (slot.item == itemToAdd && slot.amount < slot.item.maxCount)
            {
                int stackSpace = slot.item.maxCount - slot.amount;
                if (amount <= stackSpace)
                {
                    slot.AddAmount(amount);
                    return true;
                }
                else
                {
                    slot.AddAmount(stackSpace);
                    amount -= stackSpace;
                }
            }
        }

        // 빈 슬롯이 있는지 확인하고 새 슬롯 추가
        if (slots.Count < capacity)
        {
            slots.Add(new InventorySlot(itemToAdd, amount));
            return true;
        }

        /*
        if(itemToAdd.showInHand && itemToAdd.itemPrefab != null)
{
            // 씬에 있는 ItemHolder를 찾음 (또는 미리 SerializeField로 받아둔 변수 사용)
            GameObject holder = GameObject.Find("ItemHolder");

            if (holder != null && itemToAdd.spawnedInstance == null)
            {
                // 프리팹 생성 및 부모 설정
                GameObject instance = Instantiate(itemToAdd.itemPrefab, holder.transform, false);

                // SO에 생성된 실체 연결
                itemToAdd.spawnedInstance = instance;

                // 처음엔 비활성화 (인벤토리에서 '사용' 누를 때 켜짐)
                instance.SetActive(false);
            }
        } */


        Debug.LogWarning("Inventory is full!");
        return false;
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
        for (int i = 0; i < 10; i++)
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
        selectedSlotIndex += direction;
        if (selectedSlotIndex < 0) selectedSlotIndex = capacity - 1;
        else if (selectedSlotIndex >= capacity) selectedSlotIndex = 0;

        UpdateHeldItem();
    }

    private void SetSelectedSlot(int index)
    {
        if (index < capacity)
        {
            selectedSlotIndex = index;
            UpdateHeldItem();
        }
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
                // (레이어 이름이 "Weapon"인지 "PickupItem"인지 확인하세요)
                SetLayerRecursively(currentHeldItem, LayerMask.NameToLayer("PickupItem"));
            }
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
