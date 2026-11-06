using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙 크로스헤어(흰 점). 상호작용 가능한 대상을 조준하면 점 둘레에 원이 생기고,
/// 오른쪽에 키 배지 + 행동 문구("E  손전등 줍기", "E  열기")를 표시. 내용은 InteractionAimIndicator가 SetPrompt로 넣음.
/// 소모 아이템 우클릭 홀드 진행 링도 여기서 그림.
/// </summary>
public class InteractionCrosshairUI : MonoBehaviour
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    [Header("표시")]
    [SerializeField] private float dotSize = 6f;
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private int canvasSortOrder = 100;
    [SerializeField] private TMP_FontAsset itemNameFont;

    [Header("조준 링·행동 문구")]
    [SerializeField] private float aimRingDiameter = 26f;
    [SerializeField] private Color aimRingColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private float aimFadeSpeed = 12f;
    [SerializeField] private float promptOffsetX = 24f;

    [Header("소모 아이템 홀드 링")]
    [SerializeField] private float holdRingDiameter = 40f;
    [SerializeField] private Color holdRingColor = new Color(1f, 1f, 1f, 0.65f);

    private Image crosshairDot;
    private Image holdRing;
    private Image aimRing;
    private CanvasGroup promptGroup;
    private RectTransform keyBadge;
    private TextMeshProUGUI keyLabel;
    private TextMeshProUGUI actionLabel;

    private bool hasTarget;
    private string shownKey;
    private string shownAction;
    private float aimVisibility;

    private static Sprite cachedHoldRingSprite;
    private static Sprite cachedAimRingSprite;

    void Awake()
    {
        EnsureUI();
        SetPrompt(InteractionPrompt.None);
    }

    void Update()
    {
        if (aimRing == null)
        {
            return;
        }

        aimVisibility = Mathf.MoveTowards(aimVisibility, hasTarget ? 1f : 0f, Time.unscaledDeltaTime * aimFadeSpeed);

        // 원은 살짝 커지며 나타나고, 문구는 같이 페이드
        float eased = Mathf.SmoothStep(0f, 1f, aimVisibility);
        aimRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, eased);
        Color c = aimRingColor;
        c.a *= eased;
        aimRing.color = c;
        aimRing.enabled = eased > 0.001f;
        promptGroup.alpha = eased;
    }

    /// <summary>조준 대상 안내. None이면 원·문구가 페이드 아웃.</summary>
    public void SetPrompt(InteractionPrompt prompt)
    {
        EnsureUI();
        hasTarget = prompt.IsValid;
        if (!hasTarget || (prompt.Key == shownKey && prompt.Action == shownAction))
        {
            return; // 사라지는 동안엔 마지막 문구 유지
        }

        shownKey = prompt.Key;
        shownAction = prompt.Action;

        bool hasKey = !string.IsNullOrEmpty(prompt.Key);
        keyBadge.gameObject.SetActive(hasKey);
        float x = 0f;
        if (hasKey)
        {
            keyLabel.text = prompt.Key;
            float width = Mathf.Max(26f, keyLabel.GetPreferredValues(prompt.Key).x + 14f);
            keyBadge.sizeDelta = new Vector2(width, 26f);
            x = width + 8f;
        }

        actionLabel.text = prompt.Action;
        actionLabel.rectTransform.anchoredPosition = new Vector2(x, 0f);
    }

    /// <summary>예전 API — "[E] 손전등 획득" 같은 문구를 받아 SetPrompt로 넘김.</summary>
    public void SetItemName(string itemName)
    {
        SetPrompt(InteractionPrompt.FromLegacyPrompt(itemName));
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

    private void EnsureUI()
    {
        if (crosshairDot != null && holdRing != null && aimRing != null)
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

        crosshairDot = CreateCentered("CrosshairDot", canvasGo.transform, dotSize).gameObject.AddComponent<Image>();
        crosshairDot.color = dotColor;
        crosshairDot.raycastTarget = false;

        aimRing = CreateCentered("AimRing", canvasGo.transform, aimRingDiameter).gameObject.AddComponent<Image>();
        aimRing.sprite = GetOrBuildAimRingSprite();
        aimRing.raycastTarget = false;
        aimRing.enabled = false;

        holdRing = CreateCentered("ActiveItemHoldRing", canvasGo.transform, holdRingDiameter).gameObject.AddComponent<Image>();
        holdRing.sprite = GetOrBuildHoldRingSprite();
        holdRing.type = Image.Type.Filled;
        holdRing.fillMethod = Image.FillMethod.Radial360;
        holdRing.fillOrigin = (int)Image.Origin360.Top;
        holdRing.fillClockwise = true;
        holdRing.fillAmount = 0f;
        holdRing.color = holdRingColor;
        holdRing.raycastTarget = false;
        holdRing.gameObject.SetActive(false);

        BuildPrompt(canvasGo.transform);
    }

    private static RectTransform CreateCentered(string name, Transform parent, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }

    // 원 오른쪽: [키 배지] 행동 문구
    private void BuildPrompt(Transform canvas)
    {
        TMP_FontAsset font = ResolveItemNameFont();

        GameObject groupGo = new GameObject("InteractionPrompt");
        groupGo.transform.SetParent(canvas, false);
        RectTransform groupRect = groupGo.AddComponent<RectTransform>();
        groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.pivot = new Vector2(0f, 0.5f);
        groupRect.anchoredPosition = new Vector2(promptOffsetX, 0f);
        groupRect.sizeDelta = new Vector2(500f, 30f);
        promptGroup = groupGo.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        promptGroup.interactable = false;

        GameObject badgeGo = new GameObject("KeyBadge");
        badgeGo.transform.SetParent(groupGo.transform, false);
        keyBadge = badgeGo.AddComponent<RectTransform>();
        keyBadge.anchorMin = keyBadge.anchorMax = keyBadge.pivot = new Vector2(0f, 0.5f);
        keyBadge.anchoredPosition = Vector2.zero;
        keyBadge.sizeDelta = new Vector2(26f, 26f);
        Image badgeBg = badgeGo.AddComponent<Image>();
        badgeBg.color = new Color(0f, 0f, 0f, 0.5f);
        badgeBg.raycastTarget = false;

        // 얇은 흰 테두리 (키보드 키 느낌)
        const float border = 1.5f;
        Color borderColor = new Color(1f, 1f, 1f, 0.85f);
        CreateBorder("Top", keyBadge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, border), borderColor);
        CreateBorder("Bottom", keyBadge, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, border), borderColor);
        CreateBorder("Left", keyBadge, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(border, 0f), borderColor);
        CreateBorder("Right", keyBadge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(border, 0f), borderColor);

        keyLabel = CreatePromptText("Key", keyBadge, font, 18f, TextAlignmentOptions.Center);
        RectTransform keyRect = keyLabel.rectTransform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = Vector2.zero;
        keyRect.offsetMax = Vector2.zero;

        actionLabel = CreatePromptText("Action", groupGo.transform, font, 22f, TextAlignmentOptions.MidlineLeft);
        RectTransform actionRect = actionLabel.rectTransform;
        actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(0f, 0.5f);
        actionRect.sizeDelta = new Vector2(440f, 30f);
    }

    private static void CreateBorder(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI CreatePromptText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        TmpTextShadow.Apply(label, 0.5f, 0.3f, 0.35f, 0.85f);
        return label;
    }

    private static Sprite GetOrBuildAimRingSprite()
    {
        if (cachedAimRingSprite == null)
        {
            cachedAimRingSprite = BuildRingSprite("AimRing", 0.48f, 0.405f);
        }

        return cachedAimRingSprite;
    }

    private static Sprite GetOrBuildHoldRingSprite()
    {
        if (cachedHoldRingSprite == null)
        {
            cachedHoldRingSprite = BuildRingSprite("ActiveItemHoldRing", 0.48f, 0.34f);
        }

        return cachedHoldRingSprite;
    }

    /// <summary>도넛형 링 텍스처를 절차적으로 생성합니다(에셋 불필요). 반지름은 텍스처 크기 대비 비율.</summary>
    private static Sprite BuildRingSprite(string name, float outerRadius, float innerRadius)
    {
        const int size = 128;
        const float edgeSoftness = 0.012f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name + "_Tex",
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

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        sprite.name = name + "_Sprite";
        return sprite;
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
