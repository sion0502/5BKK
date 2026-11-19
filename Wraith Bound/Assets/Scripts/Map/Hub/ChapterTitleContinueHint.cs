using TMPro;
using UnityEngine;

/// <summary>
/// 챕터 제목 화면(ChapterTitleTransition) 아래 '아무 키나 눌러 진행' 안내 (2026-11-19 사용자 요청).
/// 제목 캔버스 자식으로 붙어 제목 화면과 함께 사라짐. 잠깐 뒤에 나타나 천천히 숨쉬듯 깜빡임 (unscaled 시간).
/// </summary>
public class ChapterTitleContinueHint : MonoBehaviour
{
    private const string Text = "아무 키나 눌러 진행";
    private const float AppearDelay = 1.0f;
    private const float FadeIn = 0.8f;
    private const float Period = 2.4f;
    private const float MinAlpha = 0.35f;
    private const float MaxAlpha = 0.85f;

    private TextMeshProUGUI label;
    private float timer;

    public static void Attach(Transform titleCanvas, float titleFontSize)
    {
        var go = new GameObject("ContinueHint", typeof(RectTransform));
        go.transform.SetParent(titleCanvas, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 48f - titleFontSize * 1.1f - 40f); // 제목(가운데 위 48) 바로 아래
        rect.sizeDelta = new Vector2(1200f, 60f);

        var hint = go.AddComponent<ChapterTitleContinueHint>();
        hint.label = go.AddComponent<TextMeshProUGUI>();
        hint.label.text = Text;
        hint.label.alignment = TextAlignmentOptions.Center;
        hint.label.fontSize = 28f;
        hint.label.characterSpacing = 6f;
        hint.label.raycastTarget = false;
        hint.label.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_FontAsset font = UIFontConfig.Story; // 제목과 같은 명조 계열
        if (font != null)
        {
            hint.label.font = font;
        }

        hint.label.color = new Color(0.93f, 0.86f, 0.74f, 0f); // 바랜 종이색, 처음엔 안 보임
    }

    private void Update()
    {
        if (label == null)
        {
            return;
        }

        timer += Time.unscaledDeltaTime;
        float t = timer - AppearDelay;
        if (t <= 0f)
        {
            return;
        }

        float wave = (Mathf.Sin(t * Mathf.PI * 2f / Period - Mathf.PI * 0.5f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, wave) * Mathf.Clamp01(t / FadeIn);
        Color c = label.color;
        c.a = alpha;
        label.color = c;
    }
}
