using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가운데에 종이 한 장(문서)을 띄우는 오버레이. 인트로의 기획서 클로즈업용.
/// 글은 인스펙터에서 수정. 나중에 일지 읽기 UI의 기반으로도 씁니다.
/// </summary>
public class CutsceneDocumentOverlay : CutsceneOverlayBase
{
    [Header("Content")]
    [SerializeField] private string title = "출입 금지된 섬의 폐병원에서 3일 버티기";
    [SerializeField, TextArea(3, 10)] private string body =
        "준비물: 손전등, 식량, 핸드폰, 캠코더, 라이터\n\n" +
        "촬영지: 지도에 없는 섬, 20년 전 폐쇄된 병원\n\n" +
        "제보: 익명\n<i>\"그 병원은 아직 문이 열려 있습니다.\"</i>";

    [Header("Look")]
    [SerializeField] private Vector2 paperSize = new Vector2(760f, 900f);
    [SerializeField] private float paperTilt = -2.5f;
    [SerializeField] private Color paperColor = new Color(0.86f, 0.82f, 0.72f, 1f);
    [SerializeField] private Color inkColor = new Color(0.12f, 0.1f, 0.09f, 1f);
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);

    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;

    public void SetText(string newTitle, string newBody)
    {
        title = newTitle;
        body = newBody;
        if (titleLabel != null)
        {
            titleLabel.text = title;
            bodyLabel.text = body;
        }
    }

    protected override void BuildContent(RectTransform root)
    {
        TMP_FontAsset font = MenuUIUtility.LoadPolFont();

        Image dim = CreateStretched("Dim", root).gameObject.AddComponent<Image>();
        dim.color = dimColor;
        dim.raycastTarget = false;

        RectTransform paperRect = CreateAnchored("Paper", root, new Vector2(0.5f, 0.5f), Vector2.zero, paperSize);
        paperRect.pivot = new Vector2(0.5f, 0.5f);
        paperRect.localRotation = Quaternion.Euler(0f, 0f, paperTilt);
        Image paper = paperRect.gameObject.AddComponent<Image>();
        paper.color = paperColor;
        paper.raycastTarget = false;
        Shadow shadow = paperRect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(10f, -12f);

        titleLabel = CreateLabel("Title", paperRect, font, 40f, FontStyles.Bold);
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(60f, -200f);
        titleRect.offsetMax = new Vector2(-60f, -70f);
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.text = title;

        RectTransform lineRect = CreateRect("Line", paperRect);
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.offsetMin = new Vector2(60f, -222f);
        lineRect.offsetMax = new Vector2(-60f, -218f);
        Image line = lineRect.gameObject.AddComponent<Image>();
        line.color = new Color(inkColor.r, inkColor.g, inkColor.b, 0.7f);
        line.raycastTarget = false;

        bodyLabel = CreateLabel("Body", paperRect, font, 30f, FontStyles.Normal);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(70f, 70f);
        bodyRect.offsetMax = new Vector2(-70f, -260f);
        bodyLabel.alignment = TextAlignmentOptions.TopLeft;
        bodyLabel.lineSpacing = 12f;
        bodyLabel.text = body;
    }

    private TextMeshProUGUI CreateLabel(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.fontSize = size;
        label.fontStyle = style;
        label.color = inkColor;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
}
