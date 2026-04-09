using UnityEngine;

// IInteractable 인터페이스를 상속받아 규칙을 따릅니다.
public class ItemObject : MonoBehaviour, IInteractable
{
    [Header("연결된 아이템 데이터")]
    public Items itemData;

    // 플레이어가 E키를 눌렀을 때, PlayerController가 이 함수를 호출합니다.
    public void Interact(PlayerController player)
    {
        // 만약 매개변수로 들어온 player가 비어있다면 직접 찾기
        PlayerController pc = player;
        if (pc == null)
        {
            pc = Object.FindFirstObjectByType<PlayerController>();
        }

        if (pc != null)
        {
            // 플레이어에게서 InventoryManager를 찾아서 아이템을 추가합니다.
            if (pc.TryGetComponent<InventoryManager>(out var inv))
            {
                inv.AddItem(itemData);
            }
            OnPickedUp(); // 기존 로직(로그 출력, 삭제 등) 실행
        }
        else
        {
            Debug.LogError("플레이어를 찾을 수 없어 아이템을 인벤토리에 넣지 못했습니다!");
        }
    }

    // 아이템을 획득할 때 호출되는 함수 (기존 로직 그대로 유지)
    public void OnPickedUp()
    {
        if (itemData == null)
        {
            Debug.LogError($"{gameObject.name}에 연결된 아이템 데이터(SO)가 없습니다!");
            return;
        }

        // 1. 아이템 타입에 따른 로그 출력
        Debug.Log($"[{itemData.type}] {itemData.itemName} 획득!");

        // 아이템 효과 매니저 호출 로직
        ItemEffectManager manager = Object.FindFirstObjectByType<ItemEffectManager>();
        if (manager != null)
        {
            manager.Use(itemData);
        }

        // 2. 아이템 종류별 특수 로직 분기 (작성하신 내용 유지)
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

        // 4. 바닥에서 오브젝트 제거
        Destroy(gameObject);
    }
}