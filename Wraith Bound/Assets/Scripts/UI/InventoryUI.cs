using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public GameObject inventoryPanel; // 에디터에서 Inventory_Panel을 드래그해서 넣어주세요.
    private bool isInventoryOpen = false;

    void Start()
    {
        // 시작할 때는 인벤토리를 닫아둡니다.
        inventoryPanel.SetActive(false);
    }

    void Update()
    {
        // 탭 키를 누르면 상태를 반전시킵니다.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        inventoryPanel.SetActive(isInventoryOpen);

        if (isInventoryOpen)
        {
            // 인벤토리가 열리면 마우스 커서를 자유롭게 풀어줍니다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 인벤토리가 닫히면 다시 마우스를 화면 중앙에 가둡니다 (1인칭 게임 기본).
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}