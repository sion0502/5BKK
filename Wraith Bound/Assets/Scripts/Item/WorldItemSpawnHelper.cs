using UnityEngine;

/// <summary>
/// 인벤토리에서 교체되어 빠진 아이템을 월드에 배치합니다.
/// replacedPickup(방금 주운 월드 아이템)을 넘기면 그 자리에 놓고, 안 되면 플레이어 앞 바닥에 놓습니다.
/// 위치 계산은 SwappedItemPlacement 참고.
/// </summary>
public static class WorldItemSpawnHelper
{
    private const float FallbackForwardDistance = 0.6f;

    public static void SpawnDroppedItem(Items item, int amount, Vector3 worldPosition, Vector3 forward,
        GameObject replacedPickup = null)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        GameObject dropPrefab = item.GetWorldDropPrefab();
        if (dropPrefab == null)
        {
            Debug.LogWarning($"[WorldItemSpawn] {item.itemName}에 worldDropPrefab/itemPrefab이 없어 바닥에 드롭할 수 없습니다.");
            return;
        }

        // 주운 아이템 자리를 먼저 기록하고 그 콜라이더를 끔 (Destroy는 프레임 끝이라 아직 남아 있음)
        SwappedItemPlacement.Anchor pickupAnchor = SwappedItemPlacement.CaptureAndClear(replacedPickup);

        Vector3 dir = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 legacyPos = worldPosition + dir * FallbackForwardDistance + Vector3.up * 0.15f;
        GameObject instance = Object.Instantiate(dropPrefab, legacyPos, Quaternion.LookRotation(dir));

        if (!SwappedItemPlacement.TryPlace(instance, pickupAnchor))
        {
            SwappedItemPlacement.Anchor fallback =
                SwappedItemPlacement.AnchorInFrontOf(worldPosition, dir, FallbackForwardDistance);

            if (!SwappedItemPlacement.TryPlace(instance, fallback))
            {
                // 둘 다 실패하면 예전 방식(플레이어 앞 공중) 그대로
                instance.transform.SetPositionAndRotation(legacyPos, Quaternion.LookRotation(dir));
                Debug.LogWarning($"[WorldItemSpawn] {item.itemName}을(를) 놓을 빈 공간을 찾지 못해 플레이어 앞에 배치했습니다.");
            }
        }

        // 야광봉은 GlowStickController가 자체 획득 처리 (ItemObject를 더 붙이면 상호작용이 둘로 갈림)
        GlowStickController glowStick = instance.GetComponent<GlowStickController>();
        if (glowStick != null)
        {
            glowStick.SetPickupAmount(amount);
            return;
        }

        ItemObject itemObject = instance.GetComponent<ItemObject>();
        if (itemObject != null)
        {
            itemObject.itemData = item;
            itemObject.amount = amount;
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
        added.amount = amount;
    }
}
