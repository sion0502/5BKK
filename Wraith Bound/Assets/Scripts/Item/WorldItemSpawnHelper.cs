using UnityEngine;

/// <summary>
/// 인벤토리에서 교체되어 바닥으로 떨어진 아이템을 월드에 배치합니다.
/// </summary>
public static class WorldItemSpawnHelper
{
    public static void SpawnDroppedItem(Items item, int amount, Vector3 worldPosition, Vector3 forward)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        Vector3 dir = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 pos = worldPosition + dir * 0.6f + Vector3.up * 0.15f;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject dropPrefab = item.GetWorldDropPrefab();
        if (dropPrefab == null)
        {
            Debug.LogWarning($"[WorldItemSpawn] {item.itemName}에 worldDropPrefab/itemPrefab이 없어 바닥에 드롭할 수 없습니다.");
            return;
        }

        GameObject instance = Object.Instantiate(dropPrefab, pos, rot);

        ItemObject itemObject = instance.GetComponent<ItemObject>();
        if (itemObject != null)
        {
            itemObject.itemData = item;
            return;
        }

        InteractableItem interactable = instance.GetComponent<InteractableItem>();
        if (interactable != null)
        {
            interactable.SetDroppedItem(item, amount);
            return;
        }

        ItemObject added = instance.AddComponent<ItemObject>();
        added.itemData = item;
    }
}
