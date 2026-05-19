using UnityEngine;

/// <summary>
/// 씬에 미리 둔 SmartPhone_Holder 루트를, 인벤에 스마트폰이 있고 해당 슬롯이 선택된 경우에만 켜고 끕니다.
/// 비활성 시 배터리·레이더 등 하위 MonoBehaviour의 Update가 호출되지 않습니다.
/// </summary>
public class SmartPhoneHolderToggle : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private Equipment smartPhoneItem;

    [Header("씬의 SmartPhone_Holder 루트 (비워두면 같은 씬에서 이름으로 검색)")]
    [SerializeField] private GameObject smartPhoneHolderOverride;

    private InventoryManager _inventory;
    private GameObject _holder;

    void Awake()
    {
        _inventory = GetComponent<InventoryManager>();
        if (_inventory == null)
            _inventory = GetComponentInParent<InventoryManager>();

        ResolveHolder();
        if (_holder != null)
        {
            EnsureHeldItemSway(_holder);
            _holder.SetActive(false);
        }
    }

    void ResolveHolder()
    {
        if (smartPhoneHolderOverride != null)
        {
            _holder = smartPhoneHolderOverride;
            return;
        }

        var scene = gameObject.scene;
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null || go.name != "SmartPhone_Holder")
                continue;
            if (!go.scene.IsValid() || go.scene != scene)
                continue;
            _holder = go;
            return;
        }
    }

    public bool IsSmartPhoneItem(Items item)
    {
        return item != null && item == smartPhoneItem;
    }

    bool HasSmartPhoneInInventory()
    {
        if (_inventory == null || smartPhoneItem == null)
            return false;

        foreach (var slot in _inventory.slots)
        {
            if (slot != null && slot.item == smartPhoneItem)
                return true;
        }

        return false;
    }

    private void EnsureHeldItemSway(GameObject holder)
    {
        if (holder != null && holder.GetComponent<HeldItemSway>() == null)
        {
            holder.AddComponent<HeldItemSway>();
        }
    }

    bool IsSmartPhoneSlotSelected()
    {
        if (_inventory == null || smartPhoneItem == null)
            return false;

        var item = _inventory.GetSelectedItem();
        return item == smartPhoneItem;
    }

    public bool CanToggleSelectedSmartPhone()
    {
        return _holder != null && smartPhoneItem != null && HasSmartPhoneInInventory() && IsSmartPhoneSlotSelected();
    }

    public bool ToggleSelectedSmartPhone()
    {
        if (!CanToggleSelectedSmartPhone())
        {
            if (_holder != null && _holder.activeSelf)
                _holder.SetActive(false);
            return false;
        }

        _holder.SetActive(!_holder.activeSelf);
        return true;
    }

    void Update()
    {
        if (_holder == null || smartPhoneItem == null)
            return;

        if (!HasSmartPhoneInInventory())
        {
            if (_holder.activeSelf)
                _holder.SetActive(false);
            return;
        }

        if (!IsSmartPhoneSlotSelected())
        {
            if (_holder.activeSelf)
                _holder.SetActive(false);
            return;
        }
    }
}
