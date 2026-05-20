using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙 크로스헤어(흰 점)와, Interactable 레이어 대상의 아이템 이름 표시.
/// </summary>
public class InteractionCrosshairUI : MonoBehaviour
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    [Header("표시")]
    [SerializeField] private float dotSize = 6f;
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private float labelOffsetX = 14f;
    [SerializeField] private int canvasSortOrder = 100;
    [SerializeField] private TMP_FontAsset itemNameFont;

    private Image crosshairDot;
    private TextMeshProUGUI itemNameLabel;

    void Awake()
    {
        EnsureUI();
        SetItemName(null);
    }

    public void SetItemName(string itemName)
    {
        EnsureUI();

        if (itemNameLabel == null)
        {
            return;
        }

        bool hasName = !string.IsNullOrEmpty(itemName);
        itemNameLabel.text = hasName ? itemName : string.Empty;
        itemNameLabel.gameObject.SetActive(hasName);
    }

    private void EnsureUI()
    {
        if (crosshairDot != null && itemNameLabel != null)
        {
            return;
        }

        GameObject canvasGo = new GameObject("InteractionCrosshairCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas rootCanvas = canvasGo.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = canvasSortOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject dotGo = new GameObject("CrosshairDot");
        dotGo.transform.SetParent(canvasGo.transform, false);

        RectTransform dotRect = dotGo.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(dotSize, dotSize);

        crosshairDot = dotGo.AddComponent<Image>();
        crosshairDot.color = dotColor;
        crosshairDot.raycastTarget = false;

        GameObject labelGo = new GameObject("ItemNameLabel");
        labelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(labelOffsetX, 0f);
        labelRect.sizeDelta = new Vector2(400f, 40f);

        itemNameLabel = labelGo.AddComponent<TextMeshProUGUI>();
        itemNameLabel.font = ResolveItemNameFont();
        itemNameLabel.fontSize = 22f;
        itemNameLabel.color = Color.white;
        itemNameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        itemNameLabel.raycastTarget = false;
        itemNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        itemNameLabel.overflowMode = TextOverflowModes.Overflow;
    }

    private TMP_FontAsset ResolveItemNameFont()
    {
        if (itemNameFont != null)
        {
            return itemNameFont;
        }

        itemNameFont = UIFontConfig.PolHumanRights;

#if UNITY_EDITOR
        if (itemNameFont == null)
        {
            itemNameFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PolFontAssetPath);
        }
#endif

        return itemNameFont != null ? itemNameFont : TMP_Settings.defaultFontAsset;
    }
}
