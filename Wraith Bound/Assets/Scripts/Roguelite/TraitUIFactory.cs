using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기 고르기 화면 공용 UI 생성 도우미 (사각형 배치·글자·이미지·아이콘).</summary>
public static class TraitUIFactory
{
    public const string IconResourceDir = "Roguelite/TraitIcons/"; // Resources/Roguelite/TraitIcons/<TraitId>.png

    public static readonly Color Text = new Color(0.9f, 0.88f, 0.83f);
    public static readonly Color TextDim = new Color(0.56f, 0.54f, 0.5f);
    public static readonly Color Accent = new Color(0.86f, 0.2f, 0.17f);
    public static readonly Color Ink = new Color(0.36f, 0.05f, 0.04f); // 노란 부적 종이 위 붉은 먹

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>부모 가운데 기준 (pos = 가운데 좌표).</summary>
    public static RectTransform Centered(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        return Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
    }

    public static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool raycast = false, Image.Type type = UnityEngine.UI.Image.Type.Simple)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null)
        {
            image.type = type;
        }

        return image;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
    {
        RectTransform rect = Centered(parent, name, pos, box);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        return label;
    }

    public static Texture2D Icon(TraitId id)
    {
        return Resources.Load<Texture2D>(IconResourceDir + id);
    }

    public static RawImage IconImage(Transform parent, string name, Vector2 pos, float size)
    {
        RawImage image = Centered(parent, name, pos, new Vector2(size, size)).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>해금 조건 문구 (진행 표시 포함). 시연 범위 밖이면 '추후 공개'.</summary>
    public static string UnlockLine(TraitDef def)
    {
        if (!def.Implemented)
        {
            return "추후 공개";
        }

        int cur = Mathf.Min(TraitProgress.Get(def.Counter), def.Goal);
        return def.UnlockText + (def.Goal > 1 ? " (" + cur + "/" + def.Goal + ")" : string.Empty);
    }

    public static bool IsUnlocked(TraitDef def)
    {
        return def.Implemented && TraitProgress.IsUnlocked(def.Id);
    }
}
