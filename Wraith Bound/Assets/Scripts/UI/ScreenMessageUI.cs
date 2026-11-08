using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 크로스헤어 아래에 잠깐 뜨는 짧은 안내 문구 (예: "열쇠가 필요합니다").
/// 씬에 배치할 필요 없이 ScreenMessageUI.Show(...)를 호출하면 처음 호출 때 캔버스를 코드로 만듭니다.
/// 씬이 바뀌면 함께 사라지고 다음 호출 때 다시 만들어집니다. 연출은 unscaled 시간.
/// </summary>
public class ScreenMessageUI : MonoBehaviour
{
    private const int CanvasSortOrder = 600;
    private const float DefaultDuration = 1.6f;
    private const float FadeDuration = 0.3f;
    private const float OffsetBelowCrosshair = -90f;

    private static ScreenMessageUI instance;

    private TextMeshProUGUI label;
    private CanvasGroup group;
    private float remaining;

    /// <summary>문구가 표시될 때마다 (기 '타고난 신체'·해금 진행이 "열쇠가 필요합니다"를 감지).</summary>
    public static event System.Action<string> Shown;

    /// <summary>문구를 표시합니다. 이미 떠 있으면 문구를 바꾸고 시간을 다시 셉니다.</summary>
    public static void Show(string message, float duration = DefaultDuration)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Shown?.Invoke(message);

        if (instance == null)
        {
            instance = new GameObject("ScreenMessageUI").AddComponent<ScreenMessageUI>();
            instance.Build();
        }

        instance.label.text = message;
        instance.remaining = Mathf.Max(FadeDuration, duration);
        instance.group.alpha = 1f;
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

        GameObject labelGo = new GameObject("Message");
        labelGo.transform.SetParent(transform, false);

        RectTransform rect = labelGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, OffsetBelowCrosshair);
        rect.sizeDelta = new Vector2(900f, 50f);

        label = labelGo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = MenuUIUtility.LoadPolFont();
        if (font != null)
        {
            label.font = font;
        }

        label.fontSize = 26f;
        label.color = new Color(1f, 0.86f, 0.86f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        TmpTextShadow.Apply(label); // outline은 Pol 폰트에서 글자를 검게 덮어버림
    }

    private void Update()
    {
        if (remaining <= 0f)
        {
            return;
        }

        remaining -= Time.unscaledDeltaTime;
        group.alpha = remaining >= FadeDuration ? 1f : Mathf.Clamp01(remaining / FadeDuration);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
