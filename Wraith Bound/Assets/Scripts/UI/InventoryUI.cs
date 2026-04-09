using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("필수 연결")]
    public InventoryManager invManager;
    public GameObject inventoryWindow;

    [Header("HUD (좌측 상단)")]
    public TextMeshProUGUI hudNameText;
    public Image hudIconImage;

    void Start()
    {
        inventoryWindow.SetActive(false);
        UpdateHUD();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        bool isActive = !inventoryWindow.activeSelf;
        inventoryWindow.SetActive(isActive);

        Cursor.lockState = isActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isActive;
    }

    public void UpdateHUD()
    {
        if (invManager != null && invManager.currentItem != null)
        {
            hudNameText.text = invManager.currentItem.itemName;
            hudIconImage.sprite = invManager.currentItem.icon;
            hudIconImage.color = Color.white;
        }
        else
        {
            hudNameText.text = "Empty";
            hudIconImage.color = new Color(1, 1, 1, 0);
        }
    }
}