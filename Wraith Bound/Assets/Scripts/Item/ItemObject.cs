using UnityEngine;

public class ItemObject : MonoBehaviour
{
    [Header("연결된 아이템 데이터")]
    // ActiveItem, Equipment, PassiveItem 모두 'Items'를 상속받았으므로 
    // 이 변수 하나에 셋 다 넣을 수 있습니다.
    public Items itemData;

    // 아이템을 획득할 때 호출되는 함수
    public void OnPickedUp()
    {
        if (itemData == null)
        {
            Debug.LogError($"{gameObject.name}에 연결된 아이템 데이터(SO)가 없습니다!");
            return;
        }

        // 1. 아이템 타입에 따른 로그 출력 (테스트용)
        Debug.Log($"[{itemData.type}] {itemData.itemName} 획득!");


        ItemEffectManager manager = FindObjectOfType<ItemEffectManager>();
        if (manager != null)
        {
            manager.Use(itemData); // 아이템 데이터 전달 및 기능 실행
        }

        // 2. [중요] 아이템 종류별 특수 로직 분기 (나중에 구현)
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

        // 3. 인벤토리에 데이터 넘겨주기 (예정)
        // InventoryManager.Instance.AddItem(itemData);

        // 4. 바닥에서 박스 제거
        Destroy(gameObject);
    }
}