using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌상단 퀵슬롯 인벤토리 HUD.
/// - 중앙(현재 선택 슬롯, 큰 박스) + 좌/우(이전/다음 슬롯, 작은 박스) + 좌하단(패시브 슬롯)
/// - InventoryManager의 selectedSlotIndex / capacity / slots / GetPassiveItem()를 데이터 소스로 사용합니다.
/// - capacity 변동(가방 패시브 등)에 자동 대응합니다. 표시 슬롯은 항상 3개(이전/현재/다음)이며 인덱스만 순환합니다.
/// </summary>
public class QuickInventoryHudUI : MonoBehaviour
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    [Header("References")]
    [SerializeField] private InventoryManager inventory;

    [Header("Layout (px @ 1920x1080)")]
    [SerializeField] private Vector2 rootAnchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private float mainSlotSize = 120f;
    [SerializeField] private float sideSlotSize = 60f;
    [SerializeField] private float passiveSlotSize = 48f;
    [SerializeField] private float slotSpacing = 12f;
    [SerializeField] private float passiveOffsetY = 14f;
    [SerializeField] private float labelPadding = 6f;

    [Header("Style")]
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private float borderThickness = 2f;
    [SerializeField] private int slotNumberFontSize = 18;
    [SerializeField] private int itemNameFontSize = 16;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset font;

    private SlotView prevView;
    private SlotView currentView;
    private SlotView nextView;
    private SlotView passiveView;

    private RectTransform rootRect;

    private class SlotView
    {
        public GameObject root;
        public RectTransform rect;
        public Image background;
        public Image icon;
        public TextMeshProUGUI slotNumberLabel;
        public TextMeshProUGUI itemNameLabel;
    }

    void Awake()
    {
        if (inventory == null)
        {
            inventory = FindFirstObjectByType<InventoryManager>();
        }

        EnsureHudRoot();
        BuildUI();
    }

    void LateUpdate()
    {
        if (inventory == null)
        {
            return;
        }

        Refresh();
    }

    private void EnsureHudRoot()
    {
        if (GetComponent<Canvas>() != null)
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[QuickInventoryHudUI] Canvas를 찾을 수 없습니다. HUD가 표시되지 않습니다.");
            return;
        }

        transform.SetParent(canvas.transform, false);

        // 자신의 RectTransform이 Canvas를 꽉 채우도록 설정
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private void BuildUI()
    {
        GameObject rootPanel = new GameObject("QuickInventoryHud", typeof(RectTransform));
        rootPanel.transform.SetParent(transform, false);

        rootRect = rootPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = rootAnchoredPosition;

        float totalWidth = sideSlotSize + slotSpacing + mainSlotSize + slotSpacing + sideSlotSize;
        float totalHeight = mainSlotSize + passiveOffsetY + passiveSlotSize;
        rootRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        float sideTopOffset = (mainSlotSize - sideSlotSize) * 0.5f;

        prevView = CreateSlot(rootPanel.transform, "PrevSlot", sideSlotSize, isMain: false);
        PlaceSlotTopLeft(prevView, new Vector2(0f, -sideTopOffset));

        currentView = CreateSlot(rootPanel.transform, "CurrentSlot", mainSlotSize, isMain: true);
        PlaceSlotTopLeft(currentView, new Vector2(sideSlotSize + slotSpacing, 0f));

        nextView = CreateSlot(rootPanel.transform, "NextSlot", sideSlotSize, isMain: false);
        PlaceSlotTopLeft(nextView,
            new Vector2(sideSlotSize + slotSpacing + mainSlotSize + slotSpacing, -sideTopOffset));

        passiveView = CreateSlot(rootPanel.transform, "PassiveSlot", passiveSlotSize, isMain: false);
        PlaceSlotTopLeft(passiveView, new Vector2(0f, -(mainSlotSize + passiveOffsetY)));
    }

    private void PlaceSlotTopLeft(SlotView view, Vector2 anchoredPos)
    {
        view.rect.anchorMin = new Vector2(0f, 1f);
        view.rect.anchorMax = new Vector2(0f, 1f);
        view.rect.pivot = new Vector2(0f, 1f);
        view.rect.anchoredPosition = anchoredPos;
    }

    private SlotView CreateSlot(Transform parent, string name, float size, bool isMain)
    {
        SlotView view = new SlotView();

        GameObject slotGo = new GameObject(name, typeof(RectTransform));
        slotGo.transform.SetParent(parent, false);
        view.root = slotGo;
        view.rect = slotGo.GetComponent<RectTransform>();
        view.rect.sizeDelta = new Vector2(size, size);

        view.background = slotGo.AddComponent<Image>();
        view.background.color = backgroundColor;
        view.background.raycastTarget = false;

        AddBorder(slotGo.transform, borderColor, borderThickness);

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(slotGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        float iconInset = Mathf.Max(borderThickness + 2f, size * 0.12f);
        iconRect.offsetMin = new Vector2(iconInset, iconInset);
        iconRect.offsetMax = new Vector2(-iconInset, -iconInset);

        view.icon = iconGo.AddComponent<Image>();
        view.icon.color = Color.white;
        view.icon.raycastTarget = false;
        view.icon.preserveAspect = true;
        view.icon.enabled = false;

        if (isMain)
        {
            view.slotNumberLabel = CreateLabel(slotGo.transform, "SlotNumber",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(labelPadding, -labelPadding),
                width: size * 0.5f,
                height: slotNumberFontSize + 4f,
                fontSize: slotNumberFontSize,
                alignment: TextAlignmentOptions.TopLeft);

            view.itemNameLabel = CreateLabel(slotGo.transform, "ItemName",
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f),
                anchoredPos: new Vector2(-labelPadding, labelPadding),
                width: size - labelPadding * 2f,
                height: itemNameFontSize + 6f,
                fontSize: itemNameFontSize,
                alignment: TextAlignmentOptions.BottomRight);
        }

        return view;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos,
        float width, float height, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject labelGo = new GameObject(name, typeof(RectTransform));
        labelGo.transform.SetParent(parent, false);

        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = string.Empty;

        return label;
    }

    private void AddBorder(Transform parent, Color color, float thickness)
    {
        CreateBorderEdge(parent, "BorderTop",
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), sizeDelta: new Vector2(0f, thickness),
            anchoredPos: Vector2.zero, color: color);

        CreateBorderEdge(parent, "BorderBottom",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), sizeDelta: new Vector2(0f, thickness),
            anchoredPos: Vector2.zero, color: color);

        CreateBorderEdge(parent, "BorderLeft",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 0.5f), sizeDelta: new Vector2(thickness, 0f),
            anchoredPos: Vector2.zero, color: color);

        CreateBorderEdge(parent, "BorderRight",
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(1f, 0.5f), sizeDelta: new Vector2(thickness, 0f),
            anchoredPos: Vector2.zero, color: color);
    }

    private void CreateBorderEdge(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPos, Color color)
    {
        GameObject edgeGo = new GameObject(name, typeof(RectTransform));
        edgeGo.transform.SetParent(parent, false);

        RectTransform rect = edgeGo.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPos;

        Image image = edgeGo.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void Refresh()
    {
        int capacity = inventory.capacity;

        if (capacity <= 0)
        {
            ApplyToView(currentView, slotIndex: -1, item: null, showLabels: true);
            ApplyToView(prevView, slotIndex: -1, item: null, showLabels: false);
            ApplyToView(nextView, slotIndex: -1, item: null, showLabels: false);
            ApplyPassive(null);
            return;
        }

        int selected = Mathf.Clamp(inventory.selectedSlotIndex, 0, capacity - 1);
        int prevIndex = (selected - 1 + capacity) % capacity;
        int nextIndex = (selected + 1) % capacity;

        ApplyToView(currentView, selected, GetItemAtSlotIndex(selected), showLabels: true);

        if (capacity >= 2)
        {
            ApplyToView(prevView, prevIndex, GetItemAtSlotIndex(prevIndex), showLabels: false);
            ApplyToView(nextView, nextIndex, GetItemAtSlotIndex(nextIndex), showLabels: false);
        }
        else
        {
            ApplyToView(prevView, slotIndex: -1, item: null, showLabels: false);
            ApplyToView(nextView, slotIndex: -1, item: null, showLabels: false);
        }

        ApplyPassive(inventory.GetPassiveItem());
    }

    private Items GetItemAtSlotIndex(int slotIndex)
    {
        if (inventory == null || inventory.slots == null)
        {
            return null;
        }

        if (slotIndex < 0 || slotIndex >= inventory.slots.Count)
        {
            return null;
        }

        InventorySlot slot = inventory.slots[slotIndex];
        return slot != null ? slot.item : null;
    }

    private void ApplyToView(SlotView view, int slotIndex, Items item, bool showLabels)
    {
        if (view == null || view.root == null)
        {
            return;
        }

        bool hasIndex = slotIndex >= 0;
        bool hasItem = item != null;

        if (view.icon != null)
        {
            view.icon.sprite = hasItem ? item.icon : null;
            view.icon.enabled = hasItem && item.icon != null;
        }

        if (view.slotNumberLabel != null)
        {
            if (showLabels && hasIndex)
            {
                view.slotNumberLabel.text = (slotIndex + 1).ToString();
                view.slotNumberLabel.gameObject.SetActive(true);
            }
            else
            {
                view.slotNumberLabel.gameObject.SetActive(false);
            }
        }

        if (view.itemNameLabel != null)
        {
            if (showLabels && hasItem)
            {
                view.itemNameLabel.text = item.itemName;
                view.itemNameLabel.gameObject.SetActive(true);
            }
            else
            {
                view.itemNameLabel.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyPassive(PassiveItem passive)
    {
        if (passiveView == null || passiveView.icon == null)
        {
            return;
        }

        bool hasIcon = passive != null && passive.icon != null;
        passiveView.icon.sprite = hasIcon ? passive.icon : null;
        passiveView.icon.enabled = hasIcon;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (font != null)
        {
            return font;
        }

        font = UIFontConfig.PolHumanRights;

#if UNITY_EDITOR
        if (font == null)
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PolFontAssetPath);
        }
#endif

        return font != null ? font : TMP_Settings.defaultFontAsset;
    }
}
