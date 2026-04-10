using UnityEngine;

// IInteractable 인터페이스를 상속받아 규칙을 따릅니다.
public class ItemObject : MonoBehaviour, IInteractable
{
    [Header("연결된 아이템 데이터")]
    public Items itemData;

    // 1. 상호작용 실행 (플레이어가 E키를 눌렀을 때 호출됨)
    public void Interact(GameObject interactor)
    {
        InventroyManager inv = interactor.GetComponent<InventroyManager>();

        if (inv != null)
        {
            if (inv.AddItem(itemData))
            {
                // 아이템을 인벤토리에 넣은 후, 기존에 만드셨던 로그/효과 로직 실행
                OnPickedUp();
            }
        }
    }

    // 2. [추가] 인터페이스 에러 해결을 위한 필수 함수
    // 팀원분이 만든 인터페이스 규칙을 지키기 위해 반드시 필요합니다.
    public string GetInteractPrompt()
    {
        if (itemData != null)
        {
            return $"[E] {itemData.itemName} 획득";
        }
        return "[E] 아이템 획득";
    }

    // 아이템을 획득할 때 호출되는 함수 (기존 로직 그대로 유지)
    public void OnPickedUp()
    {
        if (itemData == null)
        {
            Debug.LogError($"{gameObject.name}에 연결된 아이템 데이터(SO)가 없습니다!");
            return;
        }

        Debug.Log($"[{itemData.type}] {itemData.itemName} 획득!");

        ItemEffectManager manager = Object.FindFirstObjectByType<ItemEffectManager>();
        if (manager != null)
        {
            manager.Use(itemData);
        }

        switch (itemData.type)
        {
            case ItemType.Active:
                ActiveItem active = (ActiveItem)itemData;
                Debug.Log($"회복/수치: {active.value}, 지속시간: {active.duration}");
                break;

            case ItemType.Equip:
                Equipment equip = (Equipment)itemData;
                Debug.Log($"최대 배터리: {equip.maxEnergy}, 소모율: {equip.consumeRate}");
                break;

            case ItemType.Passive:
                PassiveItem passive = (PassiveItem)itemData;
                Debug.Log($"능력치 보너스: {passive.statModifier}");
                break;
        }

        // 4. 바닥에서 오브젝트 제거 (상단 Interact에서 안 했다면 여기서 처리)
        Destroy(gameObject);
    }
}