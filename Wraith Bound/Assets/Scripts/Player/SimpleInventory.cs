using UnityEngine;
using System.Collections.Generic;

public class SimpleInventory : MonoBehaviour
{
    // 획득한 장비들을 담는 리스트
    public List<Equipment> ownedItems = new List<Equipment>();

    // 현재 손에 들고 있는 스마트폰 오브젝트 (기존에 만든 것)
    public GameObject smartphoneObject;

    void Start()
    {
        // 시작할 때는 스마트폰이 없으므로 꺼둡니다.
        if (smartphoneObject != null)
            smartphoneObject.SetActive(false);
    }

    void Update()
    {
        // 1. 인벤토리에 '스마트폰' 타입의 아이템이 있는지 확인
        bool hasSmartphone = CheckItemInInventory("Smartphone"); // SO의 ItemName 기준

        if (hasSmartphone)
        {
            // 2. 왼쪽 클릭: 꺼내기
            if (Input.GetMouseButtonDown(0))
            {
                smartphoneObject.SetActive(true);
            }
            // 3. 오른쪽 클릭: 넣기
            if (Input.GetMouseButtonDown(1))
            {
                smartphoneObject.SetActive(false);
            }
        }
    }

    // 리스트 안에 특정 이름의 아이템이 있는지 체크하는 함수
    public bool CheckItemInInventory(string itemName)
    {
        foreach (Equipment item in ownedItems)
        {
            if (item.itemName == itemName) return true;
        }
        return false;
    }

    // 아이템을 리스트에 추가하는 함수
    public void AddItem(Equipment newItem)
    {
        ownedItems.Add(newItem);
        Debug.Log($"{newItem.itemName}이(가) 인벤토리에 추가되었습니다.");
    }
}