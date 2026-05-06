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
            Debug.LogWarning("[Item Pickup] 상호작용한 오브젝트에 InventroyManager가 없습니다.");
            return;
        }

        bool added = inventory.AddItem(item, amount);

        if (!added)
        {
            Debug.LogWarning($"[Item Pickup] 인벤토리가 가득 차서 {item.itemName}을(를) 획득하지 못했습니다.");
            return;
        }

        Debug.Log($"[Item Pickup] {item.itemName} x{amount} 획득");
        inventory.DebugPrintInventory(); Destroy(gameObject);
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