using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MenuUIUtility
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    public static TMP_FontAsset LoadPolFont(TMP_FontAsset preferred = null)
    {
        if (preferred != null)
        {
            return preferred;
        }

        TMP_FontAsset font = UIFontConfig.PolHumanRights;

#if UNITY_EDITOR
        if (font == null)
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PolFontAssetPath);
        }
#endif

        return font != null ? font : TMP_Settings.defaultFontAsset;
    }

    public static Canvas CreateOverlayCanvas(string name, int sortOrder, Transform parent = null)
    {
        GameObject canvasObject = new GameObject(name);
        if (parent != null)
        {
            canvasObject.transform.SetParent(parent, false);
        }

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static Image CreateFullScreenPanel(Transform parent, Color color, string name = "Panel")
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        StretchFullScreen(rect);

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    public static Button CreateMenuButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        Vector2 anchoredPosition,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        StretchFullScreen(labelRect);

        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.font = font;
        labelText.text = label;
        labelText.fontSize = 32f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        return button;
    }

    public static TextMeshProUGUI CreateTitle(
        Transform parent,
        string text,
        TMP_FontAsset font,
        Vector2 anchoredPosition,
        float fontSize,
        Color color)
    {
        GameObject titleObject = new GameObject("Title", typeof(RectTransform));
        titleObject.transform.SetParent(parent, false);

        RectTransform rect = titleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(1000f, 220f);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
        title.font = font;
        title.text = text;
        title.fontSize = fontSize;
        title.alignment = TextAlignmentOptions.Center;
        title.color = color;
        return title;
    }

    /// <summary>화면 가운데 기준으로 놓이는 반투명 패널 (설정·확인 창 바탕).</summary>
    public static RectTransform CreateCenteredPanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return rect;
    }

    public static TextMeshProUGUI CreateLabel(
        Transform parent,
        string text,
        TMP_FontAsset font,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject labelObject = new GameObject("Label_" + text, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>코드로 만드는 가로 슬라이더 (스프라이트 없이 단색 사각형).</summary>
    public static Slider CreateSlider(
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float min,
        float max,
        float value,
        UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject root = new GameObject("Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = size;
        rootRect.anchoredPosition = anchoredPosition;

        RectTransform track = CreateChild("Background", root.transform);
        track.anchorMin = new Vector2(0f, 0.35f);
        track.anchorMax = new Vector2(1f, 0.65f);
        track.offsetMin = Vector2.zero;
        track.offsetMax = Vector2.zero;
        track.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        RectTransform fillArea = CreateChild("Fill Area", root.transform);
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreateChild("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = new Color(0.62f, 0.1f, 0.1f, 1f);

        RectTransform handleArea = CreateChild("Handle Slide Area", root.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);

        RectTransform handle = CreateChild("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(16f, 0f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.9f, 0.88f, 0.85f, 1f);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);
        if (onChanged != null)
        {
            slider.onValueChanged.AddListener(onChanged);
        }

        return slider;
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
