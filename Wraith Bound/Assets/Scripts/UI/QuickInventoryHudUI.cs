using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌상단 퀵슬롯 인벤토리 HUD.
/// - 기본 상태: 현재 슬롯(=slot[0])만 표시 + 좌하단 패시브 슬롯
/// - Tab(기본키) 홀드: 모든 슬롯이 오른쪽으로 펼쳐지며 표시 (페이드+슬라이드)
/// - 슬롯 전환(휠/숫자키)에는 슬롯 컨테이너 슬라이드 애니메이션 적용
/// - 슬롯 위치 i에는 (selectedSlotIndex + i) % capacity 데이터가 매핑되어 좌상단이 항상 현재 슬롯이 됩니다.
/// - InventoryManager.capacity 변동에 자동 대응합니다.
/// </summary>
public class QuickInventoryHudUI : MonoBehaviour
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    [Header("References")]
    [SerializeField] private InventoryManager inventory;

    [Header("Layout (px @ 1920x1080)")]
    [SerializeField] private Vector2 rootAnchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private float slotSize = 100f;
    [SerializeField] private float passiveSlotSize = 48f;
    [SerializeField] private float slotSpacing = 8f;
    [SerializeField] private float passiveOffsetY = 14f;
    [SerializeField] private float labelPadding = 6f;

    [Header("Style")]
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private float borderThickness = 2f;
    [SerializeField] private int slotNumberFontSize = 16;
    [SerializeField] private int itemNameFontSize = 14;

    [Header("Animation")]
    [SerializeField] private KeyCode expandKey = KeyCode.Tab;
    [SerializeField] private float expandDuration = 0.22f;
    [SerializeField] private float expandSlideDistance = 24f;
    [SerializeField] private float slotChangeDuration = 0.12f;
    [SerializeField] private float slotChangeSlideDistance = 40f;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset font;

    private RectTransform rootRect;
    private RectTransform slotsContainer;
    private SlotView passiveView;
    private readonly List<SlotView> slotViews = new List<SlotView>();
    private int lastSelectedIndex = int.MinValue;
    private bool isExpanded;
    private Coroutine expandCoroutine;
    private Coroutine slotChangeCoroutine;

    private class SlotView
    {
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup canvasGroup;
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

    void Update()
    {
        if (inventory == null)
        {
            return;
        }

        HandleExpandInput();
    }

    void LateUpdate()
    {
        if (inventory == null)
        {
            return;
        }

        EnsureSlotPool(inventory.capacity);
        DetectAndAnimateSlotChange();
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

        // 자신의 RectTransform이 Canvas를 꽉 채우도록 설정 (자식 좌표 기준점)
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
        // 좌상단 고정 루트
        GameObject rootPanel = new GameObject("QuickInventoryHud", typeof(RectTransform));
        rootPanel.transform.SetParent(transform, false);

        rootRect = rootPanel.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = rootAnchoredPosition;
        rootRect.sizeDelta = new Vector2(slotSize, slotSize + passiveOffsetY + passiveSlotSize);

        // 슬롯 컨테이너 (가로 일렬 배치, 슬롯 전환 흔들기 대상)
        GameObject containerGo = new GameObject("SlotsContainer", typeof(RectTransform));
        containerGo.transform.SetParent(rootPanel.transform, false);
        slotsContainer = containerGo.GetComponent<RectTransform>();
        slotsContainer.anchorMin = new Vector2(0f, 1f);
        slotsContainer.anchorMax = new Vector2(0f, 1f);
        slotsContainer.pivot = new Vector2(0f, 1f);
        slotsContainer.anchoredPosition = Vector2.zero;
        slotsContainer.sizeDelta = new Vector2(slotSize, slotSize);

        // 패시브 슬롯 (슬롯 0 좌측 하단)
        passiveView = CreateSlot(rootPanel.transform, "PassiveSlot", passiveSlotSize, withLabels: false);
        passiveView.rect.anchorMin = new Vector2(0f, 1f);
        passiveView.rect.anchorMax = new Vector2(0f, 1f);
        passiveView.rect.pivot = new Vector2(0f, 1f);
        passiveView.rect.anchoredPosition = new Vector2(0f, -(slotSize + passiveOffsetY));
    }

    private void EnsureSlotPool(int capacity)
    {
        // 부족하면 신규 슬롯 생성 (capacity 만큼)
        while (slotViews.Count < capacity)
        {
            int index = slotViews.Count;
            string name = "Slot_" + index;

            SlotView view = CreateSlot(slotsContainer, name, slotSize, withLabels: true);
            view.rect.anchorMin = new Vector2(0f, 1f);
            view.rect.anchorMax = new Vector2(0f, 1f);
            view.rect.pivot = new Vector2(0f, 1f);

            CanvasGroup cg = view.root.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = view.root.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = false;
            cg.interactable = false;
            view.canvasGroup = cg;

            slotViews.Add(view);
            ApplySlotVisibilityImmediate(view, index);
        }

        // capacity가 줄어든 경우엔 남는 슬롯을 비활성
        for (int i = 0; i < slotViews.Count; i++)
        {
            bool shouldShow = i < capacity;
            if (slotViews[i].root.activeSelf != shouldShow)
            {
                slotViews[i].root.SetActive(shouldShow);
            }
        }
    }

    private void ApplySlotVisibilityImmediate(SlotView view, int index)
    {
        if (view == null || view.canvasGroup == null)
        {
            return;
        }

        if (index == 0)
        {
            view.canvasGroup.alpha = 1f;
            view.rect.anchoredPosition = new Vector2(0f, 0f);
        }
        else
        {
            view.canvasGroup.alpha = isExpanded ? 1f : 0f;
            float baseX = index * (slotSize + slotSpacing);
            float offX = isExpanded ? 0f : -expandSlideDistance;
            view.rect.anchoredPosition = new Vector2(baseX + offX, 0f);
        }
    }

    private void HandleExpandInput()
    {
        bool wantExpanded = Input.GetKey(expandKey);
        if (wantExpanded != isExpanded)
        {
            isExpanded = wantExpanded;
            StartExpandAnimation();
        }
    }

    private void StartExpandAnimation()
    {
        if (expandCoroutine != null)
        {
            StopCoroutine(expandCoroutine);
        }

        expandCoroutine = StartCoroutine(AnimateExpand());
    }

    private IEnumerator AnimateExpand()
    {
        float duration = Mathf.Max(0.01f, expandDuration);
        float elapsed = 0f;

        int count = slotViews.Count;
        float[] startAlphas = new float[count];
        float[] endAlphas = new float[count];
        Vector2[] startPositions = new Vector2[count];
        Vector2[] endPositions = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            SlotView view = slotViews[i];
            if (view == null || view.canvasGroup == null)
            {
                continue;
            }

            startAlphas[i] = view.canvasGroup.alpha;
            startPositions[i] = view.rect.anchoredPosition;

            if (i == 0)
            {
                endAlphas[i] = 1f;
                endPositions[i] = new Vector2(0f, 0f);
            }
            else
            {
                endAlphas[i] = isExpanded ? 1f : 0f;
                float baseX = i * (slotSize + slotSpacing);
                float offX = isExpanded ? 0f : -expandSlideDistance;
                endPositions[i] = new Vector2(baseX + offX, 0f);
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = t * t * (3f - 2f * t);

            for (int i = 0; i < count; i++)
            {
                SlotView view = slotViews[i];
                if (view == null || view.canvasGroup == null)
                {
                    continue;
                }

                view.canvasGroup.alpha = Mathf.Lerp(startAlphas[i], endAlphas[i], s);
                view.rect.anchoredPosition = Vector2.Lerp(startPositions[i], endPositions[i], s);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            SlotView view = slotViews[i];
            if (view == null || view.canvasGroup == null)
            {
                continue;
            }

            view.canvasGroup.alpha = endAlphas[i];
            view.rect.anchoredPosition = endPositions[i];
        }

        expandCoroutine = null;
    }

    private void DetectAndAnimateSlotChange()
    {
        int capacity = inventory.capacity;
        if (capacity <= 0)
        {
            return;
        }

        int selected = Mathf.Clamp(inventory.selectedSlotIndex, 0, capacity - 1);

        if (lastSelectedIndex == int.MinValue)
        {
            lastSelectedIndex = selected;
            return;
        }

        if (selected != lastSelectedIndex)
        {
            int direction = ComputeShortestDirection(lastSelectedIndex, selected, capacity);
            lastSelectedIndex = selected;
            StartSlotChangeAnimation(direction);
        }
    }

    private int ComputeShortestDirection(int from, int to, int capacity)
    {
        // 순환 인벤토리에서 더 짧은 방향을 결정합니다.
        int diff = to - from;
        if (capacity > 0)
        {
            if (diff > capacity / 2)
            {
                diff -= capacity;
            }
            else if (diff < -capacity / 2)
            {
                diff += capacity;
            }
        }

        return diff >= 0 ? 1 : -1;
    }

    private void StartSlotChangeAnimation(int direction)
    {
        if (slotChangeCoroutine != null)
        {
            StopCoroutine(slotChangeCoroutine);
        }

        slotChangeCoroutine = StartCoroutine(AnimateSlotChange(direction));
    }

    private IEnumerator AnimateSlotChange(int direction)
    {
        float duration = Mathf.Max(0.01f, slotChangeDuration);
        float elapsed = 0f;

        Vector2 basePos = Vector2.zero;
        Vector2 startOffset = new Vector2(slotChangeSlideDistance * direction, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = t * t * (3f - 2f * t);

            Vector2 offset = Vector2.Lerp(startOffset, Vector2.zero, s);
            if (slotsContainer != null)
            {
                slotsContainer.anchoredPosition = basePos + offset;
            }

            yield return null;
        }

        if (slotsContainer != null)
        {
            slotsContainer.anchoredPosition = basePos;
        }

        slotChangeCoroutine = null;
    }

    private void Refresh()
    {
        int capacity = inventory.capacity;

        if (capacity <= 0)
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                ApplyToView(slotViews[i], -1, null);
            }
            ApplyPassive(null);
            return;
        }

        int selected = Mathf.Clamp(inventory.selectedSlotIndex, 0, capacity - 1);

        for (int i = 0; i < slotViews.Count; i++)
        {
            if (i >= capacity)
            {
                ApplyToView(slotViews[i], -1, null);
                continue;
            }

            int logicalIndex = (selected + i) % capacity;
            ApplyToView(slotViews[i], logicalIndex, GetItemAtSlotIndex(logicalIndex));
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

    private void ApplyToView(SlotView view, int logicalIndex, Items item)
    {
        if (view == null || view.root == null)
        {
            return;
        }

        bool hasIndex = logicalIndex >= 0;
        bool hasItem = item != null;

        if (view.icon != null)
        {
            view.icon.sprite = hasItem ? item.icon : null;
            view.icon.enabled = hasItem && item.icon != null;
        }

        if (view.slotNumberLabel != null)
        {
            if (hasIndex)
            {
                view.slotNumberLabel.text = (logicalIndex + 1).ToString();
                view.slotNumberLabel.gameObject.SetActive(true);
            }
            else
            {
                view.slotNumberLabel.gameObject.SetActive(false);
            }
        }

        if (view.itemNameLabel != null)
        {
            if (hasItem)
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

    private SlotView CreateSlot(Transform parent, string name, float size, bool withLabels)
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

        if (withLabels)
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
