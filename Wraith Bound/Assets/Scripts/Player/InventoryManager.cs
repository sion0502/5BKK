using UnityEngine;
using System.Collections.Generic;


public class InventoryManager : MonoBehaviour
{
    [Header("아이템 데이터")]
    public List<Items> inventory = new List<Items>();
    public Items currentItem;
    private int currentIndex = -1;

    [Header("생성 위치")]
    public Transform itemHolder; // 보통 Main Camera를 연결합니다.
    public InventoryUI ui;

    public void AddItem(Items item)
    {
        inventory.Add(item);

        if (!(item is PassiveItem) && item.itemPrefab != null)
        {
            GameObject instance = Instantiate(item.itemPrefab, itemHolder);
            item.spawnedInstance = instance;

            instance.transform.localPosition = item.itemPrefab.transform.position;
            instance.transform.localRotation = item.itemPrefab.transform.rotation;

            // [중요] 생성 시점에는 무조건 꺼둡니다.
            instance.SetActive(false);
        }

        // 데이터상으로는 현재 아이템으로 잡지만, 
        // ChangeItem(index)을 호출하면 모델이 켜지므로 인덱스만 갱신합니다.
        currentIndex = inventory.Count - 1;
        currentItem = inventory[currentIndex];

        Debug.Log($"{item.itemName} 획득! (꺼내려면 클릭하세요)");
        if (ui != null) ui.UpdateHUD();
    }

    public void ChangeItem(int index)
    {
        if (index < 0 || index >= inventory.Count) return;

        // 1. 현재 아이템을 '손에 들고 있었는지' 여부를 저장합니다.
        bool wasActive = false;
        if (currentItem != null && currentItem.spawnedInstance != null)
        {
            wasActive = currentItem.spawnedInstance.activeSelf;
            // 이전 아이템 모델은 일단 끕니다.
            currentItem.spawnedInstance.SetActive(false);
        }

        // 2. 새로운 아이템으로 데이터를 교체합니다.
        currentIndex = index;
        currentItem = inventory[currentIndex];

        // 3. 이전 아이템이 켜져 있었다면, 새 아이템도 즉시 켭니다.
        // (만약 아무것도 안 들고 있었다면 그대로 꺼진 상태 유지)
        if (currentItem.spawnedInstance != null)
        {
            currentItem.spawnedInstance.SetActive(wasActive);
        }

        Debug.Log($"아이템 전환: {currentItem.itemName} (상태 계승: {wasActive})");
        if (ui != null) ui.UpdateHUD();
    }

    public void HandleScroll(float scroll)
    {
        if (inventory.Count == 0) return;

        int newIndex = currentIndex + (scroll > 0 ? -1 : 1);

        if (newIndex < 0) newIndex = inventory.Count - 1;
        if (newIndex >= inventory.Count) newIndex = 0;

        ChangeItem(newIndex);
    }

    public void SelectByIndex(int index)
    {
        if (index >= 0 && index < inventory.Count)
            ChangeItem(index);
    }

    public void RemoveItem(Items item)
    {
        if (inventory.Contains(item))
        {
            if (item.spawnedInstance != null) Destroy(item.spawnedInstance);
            inventory.Remove(item);

            if (inventory.Count > 0) ChangeItem(0);
            else { currentItem = null; currentIndex = -1; }
        }
    }
}