using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>
/// 컷씬용 전체 화면 오버레이 공통 부분. 자기 GameObject에 Screen Space Overlay 캔버스와 CanvasGroup을 두고,
/// 실제 UI는 Awake에서 BuildContent()로 코드 생성합니다.
/// 보이기/숨기기는 CanvasGroup.alpha로 — Timeline의 Cutscene Blend 트랙이 Blend(=알파)를 움직입니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class CutsceneOverlayBase : CutsceneBlendTarget
{
    [SerializeField] private int sortingOrder = 600;
    [Tooltip("시작할 때의 알파 (Timeline이 곧바로 덮어씀)")]
    [SerializeField, Range(0f, 1f)] private float initialAlpha;

    private CanvasGroup group;

    public CanvasGroup Group => group != null ? group : (group = GetComponent<CanvasGroup>());

    public float Alpha
    {
        get => Group.alpha;
        set => Group.alpha = Mathf.Clamp01(value);
    }

    public override float Blend
    {
        get => Alpha;
        set => Alpha = value;
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        driver.AddFromName<CanvasGroup>(gameObject, "m_Alpha");
    }

    /// <summary>Awake 전에 호출해야 적용됩니다 (비활성 상태에서 AddComponent 후 호출).</summary>
    public void SetSortingOrder(int order)
    {
        sortingOrder = order;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = order;
        }
    }

    protected virtual void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Group.interactable = false;
        Group.blocksRaycasts = false;
        Group.alpha = initialAlpha;

        BuildContent((RectTransform)transform);
    }

    protected abstract void BuildContent(RectTransform root);

    protected static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    protected static RectTransform CreateStretched(string name, Transform parent, float inset = 0f)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    protected static RectTransform CreateAnchored(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
