using System.Collections.Generic;
using UnityEngine;

public class EquipmentViewController : MonoBehaviour
{
    [Header("View Setup")]
    [SerializeField] private Camera itemViewCamera;
    [SerializeField] private Transform itemViewAnchor;
    [SerializeField] private string viewLayerName = "PickupItem";

    /// <summary>손에 든 아이템 렌더 카메라. 캠코더 등에서 사용 중 Orthographic Size 조절용.</summary>
    public Camera ItemViewCamera => itemViewCamera;

    private readonly Dictionary<Equipment, GameObject> spawnedViews = new();
    private InventoryManager inventory;
    private Equipment currentEquipment;
    private FlashlightEnergyController flashlightEnergy;
    private bool smartPhoneViewActive;

    void Awake()
    {
        inventory = GetComponent<InventoryManager>();
        flashlightEnergy = GetComponent<FlashlightEnergyController>();
        SetCameraActive(false);
    }

    void Update()
    {
        if (currentEquipment == null || inventory == null)
        {
            return;
        }

        if (inventory.GetActiveHeldEquipment() != currentEquipment)
        {
            HideCurrent();
        }
    }

    public bool ToggleEquipment(Equipment equipment)
    {
        if (equipment == null || equipment.itemPrefab == null || itemViewAnchor == null)
        {
            HideCurrent();
            return false;
        }

        if (currentEquipment == equipment && TryGetView(equipment, out GameObject currentView) && currentView.activeSelf)
        {
            currentView.SetActive(false);
            currentEquipment = null;
            SetCameraActive(false);
            return true;
        }

        return ShowEquipment(equipment);
    }

    public bool ShowEquipment(Equipment equipment)
    {
        if (equipment == null || equipment.itemPrefab == null || itemViewAnchor == null)
        {
            HideCurrent();
            return false;
        }

        if (currentEquipment == equipment && TryGetView(equipment, out GameObject currentView) && currentView.activeSelf)
        {
            SetCameraActive(true);
            return true;
        }

        HideCurrent();

        GameObject view = GetOrCreateView(equipment);
        if (view == null)
        {
            return false;
        }

        currentEquipment = equipment;
        view.SetActive(true);
        SetCameraActive(true);
        return true;
    }

    public void HideCurrent()
    {
        if (currentEquipment != null && TryGetView(currentEquipment, out GameObject currentView))
        {
            currentView.SetActive(false);
        }

        currentEquipment = null;

        if (!smartPhoneViewActive)
            SetCameraActive(false);
    }

    public bool TryGetCurrentView(Equipment equipment, out GameObject view)
    {
        if (equipment != null && currentEquipment == equipment && TryGetView(equipment, out view) && view.activeSelf)
        {
            return true;
        }

        view = null;
        return false;
    }

    public bool TryGetEquipmentLight(Equipment equipment, out Light light)
    {
        light = null;

        if (equipment == null || !TryGetView(equipment, out GameObject view) || view == null || !view.activeSelf)
        {
            return false;
        }

        light = view.GetComponentInChildren<Light>(true);
        return light != null;
    }

    private GameObject GetOrCreateView(Equipment equipment)
    {
        if (TryGetView(equipment, out GameObject existingView))
        {
            return existingView;
        }

        GameObject view = Instantiate(equipment.itemPrefab, itemViewAnchor);
        view.name = $"{equipment.itemName}_View";
        view.transform.localPosition = Vector3.zero;
        view.transform.localRotation = Quaternion.identity;
        view.transform.localScale = Vector3.one;
        StripWorldOnlyComponents(view);
        SetLayerRecursively(view, LayerMask.NameToLayer(viewLayerName));
        EnsureHeldItemSway(view);
        EnsureFlashlightClipPreventer(equipment, view);
        view.SetActive(false);

        spawnedViews[equipment] = view;
        return view;
    }

    private void StripWorldOnlyComponents(GameObject view)
    {
        foreach (Collider collider in view.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }

        foreach (Rigidbody rigidbody in view.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rigidbody);
        }

        foreach (ItemObject itemObject in view.GetComponentsInChildren<ItemObject>(true))
        {
            Destroy(itemObject);
        }

        foreach (InteractableItem interactableItem in view.GetComponentsInChildren<InteractableItem>(true))
        {
            Destroy(interactableItem);
        }
    }

    private void EnsureHeldItemSway(GameObject view)
    {
        if (view != null && view.GetComponent<HeldItemSway>() == null)
        {
            view.AddComponent<HeldItemSway>();
        }
    }

    private void EnsureFlashlightClipPreventer(Equipment equipment, GameObject view)
    {
        if (view == null)
            return;

        bool isFlashlight = flashlightEnergy != null && flashlightEnergy.IsFlashlightEquipment(equipment);
        if (!isFlashlight)
            return;

        if (view.GetComponent<FlashlightClipPreventer>() == null)
            view.AddComponent<FlashlightClipPreventer>();
    }

    private bool TryGetView(Equipment equipment, out GameObject view)
    {
        if (spawnedViews.TryGetValue(equipment, out view) && view != null)
        {
            return true;
        }

        view = null;
        return false;
    }

    /// <summary>
    /// SmartPhoneHolderToggle에서 스마트폰 표시/숨김 시 호출.
    /// 다른 장비 뷰가 없을 때만 카메라를 끈다.
    /// </summary>
    public void SetSmartPhoneViewActive(bool active)
    {
        smartPhoneViewActive = active;
        if (active)
            SetCameraActive(true);
        else if (currentEquipment == null)
            SetCameraActive(false);
    }

    private void SetCameraActive(bool isActive)
    {
        if (itemViewCamera != null)
        {
            itemViewCamera.gameObject.SetActive(isActive);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0)
        {
            Debug.LogWarning($"[EquipmentView] {viewLayerName} 레이어를 찾을 수 없습니다.");
            return;
        }

        Light light = obj.GetComponent<Light>();
        obj.layer = light != null ? LayerMask.NameToLayer("Default") : layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
