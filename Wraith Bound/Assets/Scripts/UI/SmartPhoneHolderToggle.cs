using System.Collections;
using UnityEngine;

/// <summary>
/// 씬에 미리 둔 SmartPhone_Holder UI를, 스마트폰 슬롯 선택 후 좌클릭 시 아래에서 올라오게 표시합니다.
/// 비활성 시 배터리·레이더 등 하위 MonoBehaviour의 Update가 호출되지 않습니다.
/// </summary>
public class SmartPhoneHolderToggle : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private Equipment smartPhoneItem;

    [Header("씬의 SmartPhone_Holder 루트 (비워두면 같은 씬에서 이름으로 검색)")]
    [SerializeField] private GameObject smartPhoneHolderOverride;

    [Header("꺼내기 애니메이션 (UI RectTransform)")]
    [SerializeField] private string animatedChildName = "SmartPhone";
    [SerializeField] private Vector2 hiddenAnchoredOffset = new Vector2(0f, -620f);
    [SerializeField] private float animationDuration = 0.45f;

    private InventoryManager _inventory;
    private EquipmentViewController _equipmentView;
    private GameObject _holder;
    private RectTransform _animatedRect;
    private Vector2 shownAnchoredPosition;
    private Vector2 hiddenAnchoredPosition;
    private Coroutine animationCoroutine;
    private bool isVisible;

    void Awake()
    {
        _inventory = GetComponent<InventoryManager>();
        if (_inventory == null)
            _inventory = GetComponentInParent<InventoryManager>();

        _equipmentView = GetComponent<EquipmentViewController>();
        if (_equipmentView == null)
            _equipmentView = GetComponentInParent<EquipmentViewController>();

        ResolveHolder();
        CacheAnimatedTarget();
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

    private void CacheAnimatedTarget()
    {
        if (_holder == null)
            return;

        Transform child = _holder.transform.Find(animatedChildName);
        if (child == null)
        {
            Debug.LogWarning($"[SmartPhone] '{animatedChildName}' 자식을 찾을 수 없습니다.");
            _holder.SetActive(false);
            return;
        }

        _animatedRect = child as RectTransform;
        if (_animatedRect == null)
        {
            Debug.LogWarning("[SmartPhone] 애니메이션 대상에 RectTransform이 없습니다.");
            _holder.SetActive(false);
            return;
        }

        shownAnchoredPosition = _animatedRect.anchoredPosition;
        hiddenAnchoredPosition = shownAnchoredPosition + hiddenAnchoredOffset;

        _animatedRect.anchoredPosition = hiddenAnchoredPosition;
        isVisible = false;
        _holder.SetActive(false);
    }

    public bool IsSmartPhoneItem(Items item)
    {
        return item != null && item == smartPhoneItem;
    }

    /// <summary>
    /// 현재 선택 슬롯이 스마트폰이 아닐 때 즉시 UI를 숨깁니다.
    /// UpdateHeldItem에서 슬롯 교체 직후 호출하여 Update() 폴링 타이밍 문제를 방지합니다.
    /// </summary>
    public void HideIfPhoneNotSelected()
    {
        Items current = _inventory != null ? _inventory.GetSelectedItem() : null;
        bool phoneIsCurrentItem = IsSmartPhoneItem(current);

        if (!phoneIsCurrentItem && _holder != null && _holder.activeSelf)
        {
            HideImmediate();
        }
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

    bool IsSmartPhoneSlotSelected()
    {
        if (_inventory == null || smartPhoneItem == null)
            return false;

        var item = _inventory.GetSelectedItem();
        return item == smartPhoneItem;
    }

    public bool CanToggleSelectedSmartPhone()
    {
        return _holder != null && _animatedRect != null && smartPhoneItem != null && HasSmartPhoneInInventory() && IsSmartPhoneSlotSelected();
    }

    public bool ToggleSelectedSmartPhone()
    {
        if (!CanToggleSelectedSmartPhone())
        {
            HideImmediate();
            return false;
        }

        SetVisibleAnimated(!isVisible);
        return true;
    }

    void Update()
    {
        if (_holder == null || smartPhoneItem == null)
            return;

        if (!HasSmartPhoneInInventory())
        {
            if (_holder.activeSelf)
                HideImmediate();
            return;
        }

        if (!IsSmartPhoneSlotSelected())
        {
            if (_holder.activeSelf)
                HideImmediate();
        }
    }

    private void SetVisibleAnimated(bool visible)
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(AnimateVisibility(visible));
    }

    private IEnumerator AnimateVisibility(bool visible)
    {
        isVisible = visible;

        if (visible)
        {
            _equipmentView?.SetSmartPhoneViewActive(true);
            _holder.SetActive(true);
            _animatedRect.anchoredPosition = hiddenAnchoredPosition;
        }

        Vector2 start = _animatedRect.anchoredPosition;
        Vector2 target = visible ? shownAnchoredPosition : hiddenAnchoredPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            _animatedRect.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        _animatedRect.anchoredPosition = target;

        if (!visible)
        {
            _holder.SetActive(false);
            _equipmentView?.SetSmartPhoneViewActive(false);
        }

        animationCoroutine = null;
    }

    private void HideImmediate()
    {
        if (_holder == null || _animatedRect == null)
            return;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        isVisible = false;
        _animatedRect.anchoredPosition = hiddenAnchoredPosition;
        _holder.SetActive(false);
        _equipmentView?.SetSmartPhoneViewActive(false);
    }
}
