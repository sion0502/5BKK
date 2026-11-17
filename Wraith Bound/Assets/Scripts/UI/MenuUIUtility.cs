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

    /// <summary>
    /// 메뉴 버튼: 박스 없이 글자 + 양옆 핀 장식. 마우스를 올리면 붓질 띠·붉은 실선이 번지고 글자가 붉어짐 (MenuButtonStyle).
    /// </summary>
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

        // 클릭 영역만 (보이지 않음)
        Image hitArea = buttonObject.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None; // 클릭 후 선택 상태로 남아 강조가 붙어 있지 않게
        button.navigation = navigation;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        MenuButtonStyle style = buttonObject.AddComponent<MenuButtonStyle>();

        // 붓질 띠 (버튼보다 넓게)
        Sprite bandSprite = MenuSkin.BrushBand;
        style.band = MenuSkin.AddImage(buttonObject.transform, "Band", bandSprite,
            bandSprite != null ? new Color(1f, 1f, 1f, 0f) : new Color(0f, 0f, 0f, 0f));
        MenuSkin.Stretch(style.band, -size.x * 0.2f, -size.y * 0.15f, -size.x * 0.2f, -size.y * 0.15f);

        // 위아래 붉은 실선
        style.bandTop = MenuSkin.AddImage(buttonObject.transform, "BandTop", MenuSkin.LineFade, Color.clear);
        MenuSkin.Place(style.bandTop, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(size.x * 0.95f, 3f));
        style.bandBottom = MenuSkin.AddImage(buttonObject.transform, "BandBottom", MenuSkin.LineFade, Color.clear);
        MenuSkin.Place(style.bandBottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(size.x * 0.95f, 3f));

        // 양옆 핀 장식 (오른쪽은 좌우 반전)
        Sprite pin = MenuSkin.LinePin;
        if (pin != null)
        {
            Vector2 pinSize = new Vector2(Mathf.Min(size.x * 0.22f, 110f), 14f);
            style.leftPin = MenuSkin.AddImage(buttonObject.transform, "PinLeft", pin, MenuSkin.Ornament);
            MenuSkin.Place(style.leftPin, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(pinSize.x * 0.5f, 0f), pinSize);
            style.rightPin = MenuSkin.AddImage(buttonObject.transform, "PinRight", pin, MenuSkin.Ornament);
            MenuSkin.Place(style.rightPin, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-pinSize.x * 0.5f, 0f), pinSize);
            style.rightPin.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        StretchFullScreen(labelRect);

        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.font = font;
        labelText.text = label;
        labelText.fontSize = Mathf.Min(32f, size.y * 0.5f);
        labelText.characterSpacing = 6f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = MenuSkin.Text;
        labelText.raycastTarget = false;
        style.label = labelText;

        return button;
    }

    /// <summary>
    /// 패널 제목: 붓질 띠 위에 자간 넓은 글자 + 아래 장식 구분선 (참고: 일시정지 'PAUSED' 머리).
    /// anchor 기준 위치 (기본 = 부모 가운데).
    /// </summary>
    public static TextMeshProUGUI CreateHeader(Transform parent, string text, TMP_FontAsset font, Vector2 anchoredPosition,
        float width, Vector2? anchor = null)
    {
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        GameObject root = new GameObject("Header_" + text, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        MenuSkin.Place(root.GetComponent<RectTransform>(), a, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(width, 80f));

        Sprite bandSprite = MenuSkin.BrushBand;
        if (bandSprite != null)
        {
            Image band = MenuSkin.AddImage(root.transform, "Band", bandSprite, new Color(1f, 1f, 1f, 0.85f));
            MenuSkin.Place(band, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(width * 0.8f, 84f));
        }

        TextMeshProUGUI label = CreateLabel(root.transform, text, font, new Vector2(0f, 4f), new Vector2(width, 60f), 34f,
            TextAlignmentOptions.Center, MenuSkin.Text);
        label.characterSpacing = 16f;

        CreateDivider(root.transform, new Vector2(0f, -32f), width);
        return label;
    }

    /// <summary>가운데 마름모·덩굴 장식이 있는 얇은 구분선.</summary>
    public static Image CreateDivider(Transform parent, Vector2 anchoredPosition, float width, Vector2? anchor = null)
    {
        Sprite sprite = MenuSkin.Divider;
        Image divider = MenuSkin.AddImage(parent, "Divider", sprite,
            sprite != null ? MenuSkin.Ornament : new Color(1f, 1f, 1f, 0.25f));
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        MenuSkin.Place(divider, a, new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(width, sprite != null ? width * 40f / 1024f : 2f));
        return divider;
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

    /// <summary>
    /// 화면 가운데 기준 패널: 가장자리가 연기처럼 흐려지는 어두운 바탕 + 손으로 그은 듯한 얇은 테두리.
    /// color의 알파 = 바탕 진하기.
    /// </summary>
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

        // 뒤쪽 클릭 막기 (보이지 않음)
        Image blocker = panelObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        Sprite smoke = MenuSkin.PanelBg;
        if (smoke != null)
        {
            Image bg = MenuSkin.AddImage(panelObject.transform, "Smoke", smoke, new Color(1f, 1f, 1f, color.a), sliced: true);
            MenuSkin.Stretch(bg, -70f, -70f, -70f, -70f); // 패널보다 넓게 깔아 가장자리가 번지게
        }
        else
        {
            MenuSkin.Stretch(MenuSkin.AddImage(panelObject.transform, "Background", null, color));
        }

        Sprite frame = MenuSkin.Frame;
        if (frame != null)
        {
            MenuSkin.Stretch(MenuSkin.AddImage(panelObject.transform, "Frame", frame, MenuSkin.Ornament, sliced: true));
        }

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
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(0f, -1f);
        track.offsetMax = new Vector2(0f, 1f);
        track.gameObject.AddComponent<Image>().color = new Color(MenuSkin.Ornament.r, MenuSkin.Ornament.g, MenuSkin.Ornament.b, 0.35f);

        RectTransform fillArea = CreateChild("Fill Area", root.transform);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        RectTransform fill = CreateChild("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(0f, -2f);
        fill.offsetMax = new Vector2(0f, 2f);
        fill.gameObject.AddComponent<Image>().color = MenuSkin.Accent;

        RectTransform handleArea = CreateChild("Handle Slide Area", root.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);

        RectTransform handle = CreateChild("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0.5f);
        handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(26f, 26f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = MenuSkin.Diamond; // 마름모 손잡이 (없으면 사각형)
        handleImage.color = MenuSkin.Text;

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
