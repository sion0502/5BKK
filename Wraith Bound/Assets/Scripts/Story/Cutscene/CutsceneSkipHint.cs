using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 컷씬 오른쪽 아래 "ESC 건너뛰기" 안내. 처음 몇 초만 보였다가 사라지고, 아무 키나 누르면 다시 잠깐 보입니다.
/// CutsceneDirector가 Show/Hide 호출. 씬이 바뀌면 함께 사라집니다.
/// </summary>
public class CutsceneSkipHint : MonoBehaviour
{
    private const int CanvasSortOrder = 950;
    private const float VisibleSeconds = 3.5f;
    private const float FadeDuration = 0.4f;
    private const float MaxAlpha = 0.75f;

    private static CutsceneSkipHint instance;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private float remaining;
    private bool active;

    public static void Show(KeyCode key)
    {
        if (instance == null)
        {
            instance = new GameObject("CutsceneSkipHint").AddComponent<CutsceneSkipHint>();
            instance.Build();
        }

        instance.label.text = $"{KeyName(key)}  건너뛰기";
        instance.active = true;
        instance.remaining = VisibleSeconds;
    }

    public static void Hide()
    {
        if (instance == null)
        {
            return;
        }

        instance.active = false;
        instance.group.alpha = 0f;
    }

    private static string KeyName(KeyCode key)
    {
        return key == KeyCode.Escape ? "ESC" : key.ToString().ToUpperInvariant();
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)labelGo.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-48f, 36f);
        rect.sizeDelta = new Vector2(400f, 40f);

        label = labelGo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = MenuUIUtility.LoadPolFont();
        if (font != null)
        {
            label.font = font;
        }

        label.fontSize = 24f;
        label.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        label.alignment = TextAlignmentOptions.Right;
        label.raycastTarget = false;
        TmpTextShadow.Apply(label);
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        if (Input.anyKeyDown)
        {
            remaining = VisibleSeconds;
        }

        remaining -= Time.unscaledDeltaTime;
        float target = remaining > 0f ? MaxAlpha : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / FadeDuration * MaxAlpha);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
