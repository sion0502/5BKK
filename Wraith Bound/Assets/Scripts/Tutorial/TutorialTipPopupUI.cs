using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 중앙 상황형 팁 팝업. 표시 중에는 시간을 멈추고(timeScale=0) 입력을 잠그며,
/// Space / Enter / 클릭으로 닫습니다.
/// </summary>
public class TutorialTipPopupUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text continueText;

    [Header("Timing")]
    [Tooltip("뜨자마자 실수로 닫히지 않도록 입력을 무시하는 시간(실시간 초)")]
    [SerializeField] private float inputDelay = 0.4f;
    [SerializeField] private float fadeSpeed = 6f;

    private TutorialInputBlocker inputBlocker;
    private Action onClosed;
    private float shownAt;
    private bool isClosing;
    private int unblockFrame = -1;

    public bool IsShowing { get; private set; }

    void Awake()
    {
        ApplyFallbackFont();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
        }
    }

    public void Show(TutorialTip tip, TutorialInputBlocker blocker, Action closed)
    {
        if (IsShowing)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = tip.title;
        }

        if (messageText != null)
        {
            messageText.text = tip.message;
        }

        if (continueText != null && string.IsNullOrEmpty(continueText.text))
        {
            continueText.text = "Space · Enter · 클릭으로 계속";
        }

        inputBlocker = blocker;
        onClosed = closed;
        shownAt = Time.unscaledTime;
        isClosing = false;
        IsShowing = true;

        inputBlocker?.Block();
        Time.timeScale = 0f;

        // 팁은 커서 없이 동작. 이후 커서가 보이면 일시정지 메뉴가 열린 것으로 판단합니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>튜토리얼 건너뛰기 등으로 즉시 닫을 때 사용합니다.</summary>
    public void ForceClose()
    {
        if (!IsShowing)
        {
            return;
        }

        Close();
    }

    void Update()
    {
        if (rootGroup != null)
        {
            float target = IsShowing && !isClosing ? 1f : 0f;
            rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
        }

        // 닫은 다음 프레임에 입력 잠금을 풀어, 닫기 클릭이 손전등 토글 등으로 새지 않게 함
        if (unblockFrame >= 0 && Time.frameCount > unblockFrame)
        {
            unblockFrame = -1;
            inputBlocker?.Unblock();
        }

        if (!IsShowing)
        {
            return;
        }

        // 일시정지 메뉴가 커서를 띄운 상태면 팁 입력은 무시 (메뉴 버튼 클릭이 팁을 닫지 않도록)
        if (Cursor.visible)
        {
            return;
        }

        // 일시정지 메뉴에서 '계속하기'를 누르면 timeScale이 1로 돌아오므로 다시 멈춤
        if (Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
        }

        if (Time.unscaledTime - shownAt < inputDelay)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetMouseButtonDown(0))
        {
            Close();
        }
    }

    private void Close()
    {
        IsShowing = false;
        isClosing = true;
        Time.timeScale = 1f;
        unblockFrame = Time.frameCount;

        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    void OnDisable()
    {
        if (IsShowing)
        {
            IsShowing = false;
            Time.timeScale = 1f;
        }

        inputBlocker?.Unblock();
        unblockFrame = -1;
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
