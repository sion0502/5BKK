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
        }


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


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
