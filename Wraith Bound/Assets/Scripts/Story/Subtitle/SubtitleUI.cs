using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 하단 자막(화자 이름 + 대사, 타자기 효과).
/// 씬에 배치할 필요 없이 정적 함수를 호출하면 처음 호출 때 캔버스를 코드로 만듭니다. unscaled 시간.
///
/// 두 가지 넘김 방식:
/// - 자동: Play()/Show()/ShowForDuration() — 시간이 지나면 다음 줄
/// - 클릭: ShowUntilClick() — 좌클릭(또는 Space/Enter)으로 넘김. 타자 중 클릭하면 문장을 바로 다 보여주고,
///   다 보인 뒤 클릭하면 닫히며 콜백 호출. 넘길 수 있을 때 오른쪽 아래 표시가 깜빡임. (컷씬 대사용)
/// </summary>
public class SubtitleUI : MonoBehaviour
{
    private const int CanvasSortOrder = 700;
    private const float CharsPerSecond = 28f;
    private const float FadeDuration = 0.25f;
    private const float BottomOffset = 120f;
    private const float AutoHoldBase = 1.2f;
    private const float AutoHoldPerChar = 0.05f;
    private const float ClickGuardSeconds = 0.15f;

    private static readonly Color SpeakerColor = new Color(0.95f, 0.55f, 0.5f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.93f, 0.9f, 1f);

    private static SubtitleUI instance;

    private readonly Queue<SubtitleLine> queue = new Queue<SubtitleLine>();
    private Action onFinished;

    private TextMeshProUGUI speakerLabel;
    private TextMeshProUGUI textLabel;
    private CanvasGroup group;
    private Image nextIndicator;

    private SubtitleLine current;
    private float typedChars;
    private float holdRemaining;
    private bool fadingOut;
    private bool waitForClick;
    private float lineAge;

    /// <summary>자막이 재생 중이면 true (페이드 아웃 포함).</summary>
    public static bool IsPlaying => instance != null && (instance.current != null || instance.queue.Count > 0 || instance.fadingOut);

    /// <summary>클릭 대기 중인 줄이 떠 있으면 true.</summary>
    public static bool IsWaitingForClick => instance != null && instance.current != null && instance.waitForClick;

    /// <summary>한 줄만 표시합니다(자동 넘김). 재생 중이던 자막은 중단됩니다.</summary>
    public static void Show(string speaker, string text, float holdSeconds = 0f)
    {
        Play(new[] { new SubtitleLine(speaker, text, holdSeconds) });
    }

    /// <summary>
    /// 한 줄을 totalSeconds 동안(타자 + 유지, 페이드 아웃 포함) 표시합니다. Timeline 자막 클립(자동 모드)용.
    /// </summary>
    public static void ShowForDuration(string speaker, string text, float totalSeconds)
    {
        int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        float hold = totalSeconds - length / CharsPerSecond - FadeDuration;
        Show(speaker, text, Mathf.Max(0.1f, hold));
    }

    /// <summary>
    /// 한 줄을 클릭할 때까지 표시합니다. 클릭으로 닫히면 dismissed 호출(Clear()로 지워지면 호출 안 됨).
    /// </summary>
    public static void ShowUntilClick(string speaker, string text, Action dismissed)
    {
        SubtitleUI ui = GetOrCreate();
        ui.queue.Clear();
        ui.queue.Enqueue(new SubtitleLine(speaker, text));
        ui.onFinished = dismissed;
        ui.fadingOut = false;
        ui.Advance();
        ui.waitForClick = true;
    }

    /// <summary>시퀀스를 처음부터 재생합니다(자동 넘김). 재생 중이던 자막은 중단됩니다(이전 onFinished는 호출 안 됨).</summary>
    public static void Play(SubtitleSequence sequence, Action finished = null)
    {
        Play(sequence != null ? sequence.lines : null, finished);
    }

    /// <summary>줄 목록을 처음부터 재생합니다(자동 넘김). 재생 중이던 자막은 중단됩니다(이전 onFinished는 호출 안 됨).</summary>
    public static void Play(IEnumerable<SubtitleLine> lines, Action finished = null)
    {
        SubtitleUI ui = GetOrCreate();
        ui.queue.Clear();
        ui.current = null;
        ui.fadingOut = false;
        ui.onFinished = finished;

        if (lines != null)
        {
            foreach (SubtitleLine line in lines)
            {
                if (line != null && !string.IsNullOrEmpty(line.text))
                {
                    ui.queue.Enqueue(line);
                }
            }
        }

        ui.Advance();
    }

    /// <summary>현재 줄을 건너뜁니다. 타자 중이면 문장을 끝까지 보여주고, 다 보였으면 다음 줄로.</summary>
    public static void SkipLine()
    {
        if (instance != null)
        {
            instance.HandleSkip();
        }
    }

    /// <summary>자막을 즉시 지웁니다. 콜백은 호출하지 않습니다.</summary>
    public static void Clear()
    {
        if (instance == null)
        {
            return;
        }

        instance.queue.Clear();
        instance.current = null;
        instance.onFinished = null;
        instance.fadingOut = false;
        instance.waitForClick = false;
        instance.group.alpha = 0f;
        instance.nextIndicator.enabled = false;
    }

    private static SubtitleUI GetOrCreate()
    {
        if (instance == null)
        {
            instance = new GameObject("SubtitleUI").AddComponent<SubtitleUI>();
            instance.Build();
        }

        return instance;
    }

    private bool IsTyping => textLabel.maxVisibleCharacters < textLabel.textInfo.characterCount;

    private void HandleSkip()
    {
        if (current == null)
        {
            return;
        }

        if (IsTyping)
        {
            typedChars = int.MaxValue;
            textLabel.maxVisibleCharacters = textLabel.textInfo.characterCount;
            return;
        }

        Advance();
    }

    private void Advance()
    {
        waitForClick = false;
        nextIndicator.enabled = false;

        if (queue.Count == 0)
        {
            current = null;
            fadingOut = group.alpha > 0f;
            if (!fadingOut)
            {
                Finish();
            }

            return;
        }

        current = queue.Dequeue();
        fadingOut = false;
        lineAge = 0f;

        bool hasSpeaker = !string.IsNullOrEmpty(current.speaker);
        speakerLabel.gameObject.SetActive(hasSpeaker);
        speakerLabel.text = hasSpeaker ? current.speaker : string.Empty;

        textLabel.text = current.text;
        textLabel.ForceMeshUpdate();
        textLabel.maxVisibleCharacters = 0;
        typedChars = 0f;

        int length = textLabel.textInfo.characterCount;
        holdRemaining = current.holdSeconds > 0f
            ? current.holdSeconds
            : AutoHoldBase + length * AutoHoldPerChar;
    }

    private void Finish()
    {
        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (fadingOut)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt / FadeDuration);
            if (group.alpha <= 0f)
            {
                fadingOut = false;
                Finish();
            }

            return;
        }

        if (current == null)
        {
            return;
        }

        lineAge += dt;
        group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt / FadeDuration);

        if (waitForClick && lineAge > ClickGuardSeconds && ClickPressed())
        {
            HandleSkip();
            if (current == null)
            {
                return;
            }
        }

        int total = textLabel.textInfo.characterCount;
        if (textLabel.maxVisibleCharacters < total)
        {
            typedChars += CharsPerSecond * dt;
            textLabel.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(typedChars));
            return;
        }

        if (waitForClick)
        {
            nextIndicator.enabled = Mathf.Repeat(lineAge, 1f) < 0.6f;
            return;
        }

        holdRemaining -= dt;
        if (holdRemaining <= 0f)
        {
            Advance();
        }
    }

    private static bool ClickPressed()
    {
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
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

        TMP_FontAsset font = MenuUIUtility.LoadPolFont();

        // 하단 반투명 띠 — 밝은 배경에서도 글자가 읽히도록
        GameObject backdropGo = new GameObject("Backdrop");
        backdropGo.transform.SetParent(transform, false);
        RectTransform backdropRect = backdropGo.AddComponent<RectTransform>();
        backdropRect.anchorMin = new Vector2(0.5f, 0f);
        backdropRect.anchorMax = new Vector2(0.5f, 0f);
        backdropRect.pivot = new Vector2(0.5f, 0f);
        backdropRect.anchoredPosition = new Vector2(0f, BottomOffset - 30f);
        backdropRect.sizeDelta = new Vector2(1300f, 180f);
        Image backdrop = backdropGo.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.45f);
        backdrop.raycastTarget = false;

        // 화자 이름은 자막 띠 왼쪽 위에 (대사 본문은 가운데)
        speakerLabel = CreateLabel("Speaker", font, 28f, SpeakerColor, new Vector2(0f, BottomOffset + 100f), new Vector2(1200f, 40f));
        speakerLabel.alignment = TextAlignmentOptions.BottomLeft;
        textLabel = CreateLabel("Text", font, 34f, TextColor, new Vector2(0f, BottomOffset), new Vector2(1200f, 90f));
        textLabel.textWrappingMode = TextWrappingModes.Normal;
        textLabel.alignment = TextAlignmentOptions.Top;

        // 클릭 대기 표시 (작은 마름모, 깜빡임)
        GameObject indicatorGo = new GameObject("NextIndicator");
        indicatorGo.transform.SetParent(transform, false);
        RectTransform indicatorRect = indicatorGo.AddComponent<RectTransform>();
        indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(0.5f, 0f);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRect.anchoredPosition = new Vector2(620f, BottomOffset - 8f);
        indicatorRect.sizeDelta = new Vector2(12f, 12f);
        indicatorRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        nextIndicator = indicatorGo.AddComponent<Image>();
        nextIndicator.color = SpeakerColor;
        nextIndicator.raycastTarget = false;
        nextIndicator.enabled = false;
    }

    private TextMeshProUGUI CreateLabel(string name, TMP_FontAsset font, float size, Color color, Vector2 position, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Bottom;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        TmpTextShadow.Apply(label); // outline은 Pol 폰트 글자를 검게 덮어버려서 그림자 사용
        return label;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
