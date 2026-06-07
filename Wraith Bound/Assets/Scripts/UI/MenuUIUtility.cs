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
        button.onClick.AddListener(onClick);

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

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
