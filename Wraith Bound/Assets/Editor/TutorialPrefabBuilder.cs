using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial 프리팹 생성 (UI 구조가 프리팹 안에 실제로 구워짐 → 생성 후 에디터에서 자유롭게 수정 가능).
/// 메뉴: Tools &gt; UI &gt; Build Tutorial Prefab
/// 주의: 다시 실행하면 프리팹을 덮어써서 에디터에서 수정한 내용이 초기화됩니다.
/// </summary>
public static class TutorialPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefab/UI/Tutorial.prefab";
    private const int CanvasSortOrder = 450; // 일시정지 메뉴(500)보다 아래

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color SubTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color CheckColor = new Color(0.55f, 0.95f, 0.6f, 1f);
    private static readonly Color SkipColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
    private static readonly Color TipTitleColor = new Color(1f, 0.85f, 0.45f, 1f);

    private static TMP_FontAsset font;
    private static Sprite roundedSprite;
    private static Sprite checkSprite;

    [MenuItem("Tools/UI/Build Tutorial Prefab")]
    public static void BuildPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(PrefabPath));

        UIFontConfig fontConfig = Resources.Load<UIFontConfig>("UIFontConfig");
        font = fontConfig != null ? fontConfig.polHumanRights : null;
        roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        checkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

        GameObject root = new GameObject("Tutorial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = LayerMask.NameToLayer("UI");
        SetupCanvas(root);

        TutorialPromptUI prompt = BuildPrompt(root.transform);
        TutorialTipPopupUI tip = BuildTipPopup(root.transform);

        TutorialHighlight highlight = root.AddComponent<TutorialHighlight>();
        TutorialManager manager = root.AddComponent<TutorialManager>();
        manager.ResetToDefaults();

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("promptUI").objectReferenceValue = prompt;
        so.FindProperty("tipPopup").objectReferenceValue = tip;
        so.FindProperty("highlight").objectReferenceValue = highlight;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[Tutorial] 프리팹 생성: {PrefabPath}");
    }

    private static void SetupCanvas(GameObject root)
    {
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        // GraphicRaycaster는 일부러 넣지 않음: 클릭 입력이 필요 없고, IsPointerOverUI 판정으로 아이템 사용을 막지 않도록.
    }

    // ───────── 우상단 안내 ─────────

    private static TutorialPromptUI BuildPrompt(Transform parent)
    {
        RectTransform panel = CreateRect("PromptPanel", parent);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-40f, -40f);
        panel.sizeDelta = new Vector2(500f, 0f);

        AddImage(panel.gameObject, PanelColor, true);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        VerticalLayoutGroup vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(22, 22, 16, 16);
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 키 배지 + 문구 행
        RectTransform row = CreateRect("Row", panel);
        HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        RectTransform badge = CreateRect("KeyBadge", row);
        Image badgeImage = AddImage(badge.gameObject, Color.white, true);
        HorizontalLayoutGroup badgeLayout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.padding = new RectOffset(14, 14, 6, 6);
        badgeLayout.childControlWidth = true;
        badgeLayout.childControlHeight = true;
        badgeLayout.childAlignment = TextAnchor.MiddleCenter;
        LayoutElement badgeElement = badge.gameObject.AddComponent<LayoutElement>();
        badgeElement.minWidth = 52f;
        badgeElement.minHeight = 44f;

        TMP_Text keyText = CreateText("KeyText", badge, "WASD", 22f, new Color(0.08f, 0.08f, 0.08f, 1f), TextAlignmentOptions.Center);
        keyText.fontStyle = FontStyles.Bold;
        keyText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text message = CreateText("Message", row, "주변을 걸어서 이동하세요", 24f, Color.white, TextAlignmentOptions.Left);
        message.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Image checkIcon = CreateCheckIcon(row);

        // 건너뛰기 행
        RectTransform skip = CreateRect("Skip", panel);
        CanvasGroup skipGroup = skip.gameObject.AddComponent<CanvasGroup>();
        skipGroup.interactable = false;
        skipGroup.blocksRaycasts = false;
        HorizontalLayoutGroup skipLayout = skip.gameObject.AddComponent<HorizontalLayoutGroup>();
        skipLayout.spacing = 12f;
        skipLayout.childAlignment = TextAnchor.MiddleLeft;
        skipLayout.childControlWidth = true;
        skipLayout.childControlHeight = true;
        skipLayout.childForceExpandWidth = false;
        skipLayout.childForceExpandHeight = false;

        TMP_Text skipLabel = CreateText("SkipLabel", skip, "E 길게 눌러 건너뛰기", 16f, SubTextColor, TextAlignmentOptions.Left);
        skipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Image skipFill = CreateBar("SkipBar", skip, 4f, SkipColor);
        skipFill.transform.parent.GetComponent<LayoutElement>().flexibleWidth = 1f;

        TutorialPromptUI prompt = panel.gameObject.AddComponent<TutorialPromptUI>();
        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("panelGroup").objectReferenceValue = group;
        so.FindProperty("keyText").objectReferenceValue = keyText;
        so.FindProperty("keyBadge").objectReferenceValue = badgeImage;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("checkIcon").objectReferenceValue = checkIcon;
        so.FindProperty("skipGroup").objectReferenceValue = skipGroup;
        so.FindProperty("skipFill").objectReferenceValue = skipFill;
        so.ApplyModifiedPropertiesWithoutUndo();
        return prompt;
    }

    // ───────── 중앙 상황형 팁 ─────────

    private static TutorialTipPopupUI BuildTipPopup(Transform parent)
    {
        RectTransform root = CreateRect("TipPopup", parent);
        Stretch(root);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform dim = CreateRect("Dim", root);
        Stretch(dim);
        AddImage(dim.gameObject, new Color(0f, 0f, 0f, 0.55f), false);

        RectTransform box = CreateRect("Box", root);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(680f, 0f);
        AddImage(box.gameObject, new Color(0.05f, 0.05f, 0.05f, 0.92f), true);

        VerticalLayoutGroup vlg = box.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 32, 28);
        vlg.spacing = 18f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = CreateText("Title", box, "팁", 36f, TipTitleColor, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;

        RectTransform divider = CreateRect("Divider", box);
        AddImage(divider.gameObject, BarBackColor, false);
        divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;

        TMP_Text message = CreateText("Message", box, "", 26f, Color.white, TextAlignmentOptions.Center);
        message.lineSpacing = 12f;

        TMP_Text continueText = CreateText("Continue", box, "Space · Enter · 클릭으로 계속", 18f, SubTextColor, TextAlignmentOptions.Center);

        TutorialTipPopupUI tip = root.gameObject.AddComponent<TutorialTipPopupUI>();
        SerializedObject so = new SerializedObject(tip);
        so.FindProperty("rootGroup").objectReferenceValue = group;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("continueText").objectReferenceValue = continueText;
        so.ApplyModifiedPropertiesWithoutUndo();
        return tip;
    }

    // ───────── 헬퍼 ─────────

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image AddImage(GameObject go, Color color, bool rounded)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (rounded && roundedSprite != null)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            tmp.font = font;
        }

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    /// <summary>완료 시 튀어나오는 초록 체크. 자리는 항상 차지해서 체크가 떠도 문구가 밀리지 않음.</summary>
    public static Image CreateCheckIcon(Transform row)
    {
        if (checkSprite == null)
        {
            checkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        }

        RectTransform rect = CreateRect("CheckIcon", row);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 34f;
        element.preferredHeight = element.minHeight = 34f;

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = checkSprite;
        image.color = CheckColor;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <returns>채움(fill) 이미지</returns>
    private static Image CreateBar(string name, Transform parent, float height, Color fillColor)
    {
        RectTransform back = CreateRect(name, parent);
        AddImage(back.gameObject, BarBackColor, false);
        LayoutElement element = back.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;

        RectTransform fill = CreateRect("Fill", back);
        Stretch(fill);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = roundedSprite;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        return fillImage;
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
