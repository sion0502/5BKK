using UnityEngine;

public class InteractableItem : MonoBehaviour, IInteractable
{
    [SerializeField] private Items item;
    [SerializeField] private int amount = 1;

    public void Interact(GameObject interactor)
    {
        if (item == null)
        {
            Debug.LogError($"[Item Pickup] {gameObject.name}에 Item이 할당되지 않았습니다.");
            return;
        }

        InventoryManager inventory = interactor.GetComponent<InventoryManager>();

        if (inventory == null)
        {
            Debug.LogWarning("[Item Pickup] 상호작용한 오브젝트에 InventoryManager가 없습니다.");
            return;
        }

        // 아이템 획득은 InventoryManager의 통합 함수로 처리합니다.
        // ActiveItem과 Equipment는 일반 인벤토리로, PassiveItem은 패시브 슬롯으로 분기됩니다.
        bool added = inventory.TryAcquireItem(item, amount);

        if (!added)
        {
            Debug.LogWarning($"[Item Pickup] 인벤토리에 추가하지 못해서 {item.itemName}을(를) 획득하지 못했습니다.");
            return;
        }

        Debug.Log($"[Item Pickup] {item.itemName} x{amount} 획득");

        inventory.DebugPrintInventory();
        inventory.DebugPrintPassiveSlot();

        // 획득에 성공한 경우에만 맵에 배치된 아이템 오브젝트를 제거합니다.
        Destroy(gameObject);
    }

    public string GetInteractPrompt()
    {
        if (item == null)
        {
            return "[E] 아이템 획득";
        }

        return $"[E] {item.itemName} 획득";
    }
}
