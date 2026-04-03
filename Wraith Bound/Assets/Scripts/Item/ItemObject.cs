using UnityEngine;

public class ItemObject : MonoBehaviour
{
    // 이 네모 박스가 어떤 아이템인지 SO를 연결하세요
    public Items itemData;

    // 플레이어가 획득했을 때 호출될 함수
    public void OnPickedUp()
    {
        Debug.Log($"{itemData.itemName}을(를) 획득했습니다!");

        // 여기에 인벤토리에 추가하는 로직을 넣을 예정
        // Inventory.Instance.AddItem(itemData); 

        Destroy(gameObject); // 먹었으니까 월드에서 삭제
    }
}