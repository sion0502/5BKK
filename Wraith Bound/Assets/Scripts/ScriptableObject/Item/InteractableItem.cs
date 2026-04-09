using UnityEngine;

public class InteractableItem : MonoBehaviour, IInteractable
{
    public Items item;
    public int amount = 1;

    public void Interact(GameObject interactor)
    {
        if (item == null)
        {
            Debug.LogError($"[인벤토리 에러] '{gameObject.name}' 오브젝트의 InteractableItem 컴포넌트에 Item이 할당되지 않았습니다!");
            return;
        }
        InventroyManager inventory = interactor.GetComponent<InventroyManager>();

        if (inventory != null)
        {
            if (inventory.AddItem(item, amount))
            {
                Debug.Log($"{item.itemName}을(를) {amount}개 획득했습니다.");

                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning("인벤토리가 꽉 차서 아이템을 획득할 수 없습니다.");
            }
        }
    }

    public string GetInteractPrompt()
    {
        return $"[E] {item.itemName} 획득";
    }
}
