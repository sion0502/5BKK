using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우상단 기본 진행 안내: 키 배지 + 문구 + 완료 체크 + 건너뛰기(E 길게) 게이지.
/// 모든 연출은 unscaled time 기준이라 일시정지·팁 표시 중에도 자연스럽게 유지됩니다.
/// </summary>
public class TutorialPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private Image keyBadge;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image checkIcon;
    [SerializeField] private CanvasGroup skipGroup;
    [SerializeField] private Image skipFill;

    [Header("Style")]
    [SerializeField] private Color messageColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color completeColor = new Color(0.55f, 0.95f, 0.6f, 1f);
    [SerializeField] private Color badgeColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float checkPopDuration = 0.25f;

    private float targetAlpha;
    private float checkShownAt = -1f;

    void Awake()
    {
        ApplyFallbackFont();

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
        }

        SetSkipProgress(0f);
        HideCheck();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (panelGroup != null)
        {
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, dt * fadeSpeed);
        }

        UpdateCheckPop();
    }

    public bool IsFadedOut => panelGroup == null || panelGroup.alpha <= 0.001f;

    public void ShowStep(TutorialStep step, int index, int total)
    {
        if (keyText != null)
        {
            keyText.text = step.keyLabel;
        }

        if (keyBadge != null)
        {
            keyBadge.gameObject.SetActive(!string.IsNullOrEmpty(step.keyLabel));
            keyBadge.color = badgeColor;
        }

        if (messageText != null)
        {
            messageText.text = step.message;
            messageText.color = messageColor;
        }

        HideCheck();
        targetAlpha = 1f;
    }

    public void MarkComplete()
    {
        ShowCheck();

        if (messageText != null)
        {
            messageText.color = completeColor;
        }

        if (keyBadge != null)
        {
            keyBadge.color = completeColor;
        }
    }

    public void ShowFinished(string message)
    {
        if (keyBadge != null)
        {
            keyBadge.gameObject.SetActive(false);
        }

        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = completeColor;
        }

        ShowCheck();
        SetSkipVisible(false);
        targetAlpha = 1f;
    }

    public void SetSkipProgress(float progress)
    {
        if (skipFill != null)
        {
            skipFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    public void SetSkipVisible(bool visible)
    {
        if (skipGroup != null)
        {
            skipGroup.alpha = visible ? 1f : 0f;
        }
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    private void ShowCheck()
    {
        if (checkIcon == null)
        {
            return;
        }

        checkIcon.color = completeColor;
        checkIcon.enabled = true;
        checkShownAt = Time.unscaledTime;
        checkIcon.rectTransform.localScale = Vector3.zero;
    }

    private void HideCheck()
    {
        checkShownAt = -1f;
        if (checkIcon != null)
        {
            checkIcon.enabled = false;
        }
    }

    // 체크가 0 → 1.25 → 1 크기로 튀어나오는 연출
    private void UpdateCheckPop()
    {
        if (checkIcon == null || checkShownAt < 0f)
        {
            return;
        }

        float t = checkPopDuration > 0f ? (Time.unscaledTime - checkShownAt) / checkPopDuration : 1f;
        float scale = t < 0.6f
            ? Mathf.Lerp(0f, 1.25f, t / 0.6f)
            : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.6f) / 0.4f));
        checkIcon.rectTransform.localScale = Vector3.one * scale;
    }

    private void ApplyFallbackFont()
    {
        TMP_FontAsset font = UIFontConfig.PolHumanRights;
        if (font == null)
        {
            return;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == null)
            {
                text.font = font;
            }
        }
    }
}
