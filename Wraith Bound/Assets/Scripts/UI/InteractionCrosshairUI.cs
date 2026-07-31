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

    [Header("소모 아이템 홀드 링")]
    [SerializeField] private float holdRingDiameter = 40f;
    [SerializeField] private Color holdRingColor = new Color(1f, 1f, 1f, 0.65f);

    private Image crosshairDot;
    private TextMeshProUGUI itemNameLabel;
    private Image holdRing;

    private static Sprite cachedHoldRingSprite;

    void Awake()
    {
        EnsureUI();
        SetItemName(null);
    }

    /// <summary>소모 아이템 홀드 사용 진행도(0~1) 표시. 0 이하면 링을 숨깁니다.</summary>
    public void SetActiveItemHoldProgress(float progress01)
    {
        EnsureUI();

        if (holdRing == null)
        {
            return;
        }

        if (progress01 <= 0f)
        {
            holdRing.fillAmount = 0f;
            holdRing.gameObject.SetActive(false);
            return;
        }

        if (!holdRing.gameObject.activeSelf)
        {
            holdRing.gameObject.SetActive(true);
        }

        holdRing.fillAmount = Mathf.Clamp01(progress01);
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
        if (crosshairDot != null && itemNameLabel != null && holdRing != null)
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

        GameObject ringGo = new GameObject("ActiveItemHoldRing");
        ringGo.transform.SetParent(canvasGo.transform, false);

        RectTransform ringRect = ringGo.AddComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = Vector2.zero;
        ringRect.sizeDelta = new Vector2(holdRingDiameter, holdRingDiameter);

        holdRing = ringGo.AddComponent<Image>();
        holdRing.sprite = GetOrBuildHoldRingSprite();
        holdRing.type = Image.Type.Filled;
        holdRing.fillMethod = Image.FillMethod.Radial360;
        holdRing.fillOrigin = (int)Image.Origin360.Top;
        holdRing.fillClockwise = true;
        holdRing.fillAmount = 0f;
        holdRing.color = holdRingColor;
        holdRing.raycastTarget = false;
        ringGo.SetActive(false);

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

    /// <summary>도넛형 링 텍스처를 절차적으로 생성합니다(에셋 불필요). 인스턴스 간 공유 캐시.</summary>
    private static Sprite GetOrBuildHoldRingSprite()
    {
        if (cachedHoldRingSprite != null)
        {
            return cachedHoldRingSprite;
        }

        const int size = 128;
        const float outerRadius = 0.48f;
        const float innerRadius = 0.34f;
        const float edgeSoftness = 0.012f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ActiveItemHoldRing_Tex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / size;

                float outerAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outerRadius - edgeSoftness, outerRadius, dist));
                float innerAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius - edgeSoftness, innerRadius, dist));
                float alpha = Mathf.Clamp01(outerAlpha) * Mathf.Clamp01(innerAlpha);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        cachedHoldRingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f));
        cachedHoldRingSprite.name = "ActiveItemHoldRing_Sprite";

        return cachedHoldRingSprite;
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
