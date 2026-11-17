using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 UI 질감(붓질 띠·흐려지는 선·핀 장식·구분선·거친 테두리·연기 패널·마름모)과 색.
/// 텍스처: Resources/UI/MenuSkin/*.png (Tools/MenuSkin/menu_skin.py로 생성). 없으면 null → 단색으로 대체.
/// </summary>
public static class MenuSkin
{
    private const string Dir = "UI/MenuSkin/";

    public static readonly Color Text = new Color(0.84f, 0.82f, 0.78f, 1f);
    public static readonly Color TextHover = new Color(0.98f, 0.64f, 0.58f, 1f);
    public static readonly Color TextDim = new Color(0.55f, 0.53f, 0.5f, 1f);
    public static readonly Color Ornament = new Color(0.86f, 0.84f, 0.8f, 0.6f);
    public static readonly Color Accent = new Color(0.78f, 0.14f, 0.12f, 1f);

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite BrushBand => Get("BrushBand", Vector4.zero);
    public static Sprite LineFade => Get("LineFade", Vector4.zero);
    public static Sprite LinePin => Get("LinePin", Vector4.zero);
    public static Sprite Divider => Get("Divider", Vector4.zero);
    public static Sprite Diamond => Get("Diamond", Vector4.zero);
    /// <summary>9-slice (테두리 32px).</summary>
    public static Sprite Frame => Get("Frame", new Vector4(32f, 32f, 32f, 32f));
    /// <summary>9-slice (테두리 80px).</summary>
    public static Sprite PanelBg => Get("PanelBg", new Vector4(80f, 80f, 80f, 80f));

    private static Sprite Get(string name, Vector4 border)
    {
        if (Cache.TryGetValue(name, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = Resources.Load<Texture2D>(Dir + name);
        if (texture == null)
        {
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        sprite.name = name;
        Cache[name] = sprite;
        return sprite;
    }

    /// <summary>자식 Image 하나 생성 (레이캐스트 끔). 스프라이트가 없으면 단색 사각형.</summary>
    public static Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sliced && sprite != null)
        {
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    public static RectTransform Stretch(Component component, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        RectTransform rect = (RectTransform)component.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static RectTransform Place(Component component, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)component.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
