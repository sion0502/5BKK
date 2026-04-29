using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject inventoryPanel;

    [Header("Slot Settings")]
    public GameObject slotPrefab;
    public Transform slotContainer;

    [Header("Quick Slot (Top Left UI)")]
    public Image quickIcon;            // 화면 왼쪽 상단 아이콘 이미지
    public TextMeshProUGUI quickName;  // 화면 왼쪽 상단 아이템 이름 텍스트

    private InventroyManager invManager;
    private bool isInventoryOpen = false;

    void Awake()
    {
        invManager = GetComponent<InventroyManager>();
    }

    void Start()
    {
        inventoryPanel.SetActive(false);
        UpdateQuickSlotUI(); // 시작할 때 한 번 초기화
    }

    void Update()
    {
        // 1. 전체 인벤토리 토글 (Tab)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }

        // 2. 현재 아이템 UI는 인벤토리 열림 여부와 상관없이 항상 업데이트
        UpdateQuickSlotUI();
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        inventoryPanel.SetActive(isInventoryOpen);

        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RedrawInventory(); // 전체 목록 갱신
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // [중요] 상단 현재 아이템 UI를 갱신하는 로직
    public void UpdateQuickSlotUI()
    {
        int idx = invManager.selectedSlotIndex;

        // 1. 인덱스가 유효하고, 해당 슬롯에 아이템 데이터가 있는지 확인
        if (idx < invManager.slots.Count && invManager.slots[idx].item != null)
        {
            var currentItem = invManager.slots[idx].item;
            quickIcon.sprite = currentItem.icon;
            quickName.text = currentItem.itemName;

            quickIcon.enabled = true; // 아이템이 있으면 이미지 컴포넌트 활성화
        }
        else
        {
            // 2. 아이템이 없으면 이미지 컴포넌트를 비활성화하여 투명하게 만듦
            quickIcon.enabled = false;
            quickName.text = "No Item"; // 또는 공백 ""
        }
    }

    public void RedrawInventory()
    {
        foreach (Transform child in slotContainer) Destroy(child.gameObject);

        for (int i = 0; i < invManager.slots.Count; i++)
        {
            int index = i; // 클로저(Closure) 문제를 방지하기 위해 로컬 변수에 복사
            GameObject go = Instantiate(slotPrefab, slotContainer);

            // 아이콘과 텍스트 설정
            Image icon = go.transform.Find("Icon").GetComponent<Image>();
            TextMeshProUGUI amountText = go.transform.Find("Amount_Text").GetComponent<TextMeshProUGUI>();

            var slotData = invManager.slots[i];
            if (slotData.item != null)
            {
                icon.sprite = slotData.item.icon;
                icon.enabled = true; // 아이템이 있으면 아이콘 활성화
                amountText.text = slotData.amount > 1 ? slotData.amount.ToString() : "";
            }
            else
            {
                icon.enabled = false; // 빈 슬롯은 아이콘 숨기기
                amountText.text = "";
            }

            // --- 추가: 클릭 이벤트 연결 ---
            Button slotButton = go.GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => OnSlotClicked(index));
            }
        }
    }

    // 슬롯 클릭 시 실행될 함수
    private void OnSlotClicked(int index)
    {
        invManager.selectedSlotIndex = index; // 매니저의 인덱스 변경
        invManager.UpdateHeldItem();          // 실제 손에 든 모델링 변경
        UpdateQuickSlotUI();                  // 퀵슬롯 UI 즉시 갱신
        Debug.Log(index + "번 슬롯 아이템 선택됨");
    }
}