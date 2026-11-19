using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 진행 안내를 '단계 카드'로 보이게 함 (2026-11-19 — 오른쪽 위 작은 상자는 관람객이 못 보고 지나침).
///  · 위치: 화면 가운데 아래쪽, 크게. 맨 위에 '단계 2 / 9'.
///  · 새 단계: 이전 카드가 왼쪽으로 빠지고 새 카드가 오른쪽에서 미끄러져 들어옴.
///  · 완료: 효과음. 10초 넘게 안 하면 4초마다 흔들며 커짐(다시 눈에 띄게).
/// TutorialPromptUI가 Awake에서 붙이고, 카드 내용 교체(applyContent)를 이 컴포넌트의 전환 중간에 맡김. unscaled 시간.
/// </summary>
public class TutorialStepCardStyle : MonoBehaviour
{
    private const float BottomY = 200f;
    private const float Width = 780f;
    private const float SlideDistance = 90f;
    private const float OutSeconds = 0.18f;
    private const float InSeconds = 0.28f;
    private const float NudgeAfter = 10f;
    private const float NudgeEvery = 4f;
    private const float NudgeSeconds = 0.55f;

    private RectTransform panel;
    private CanvasGroup group;
    private TMP_Text progressLabel;
    private AudioSource audioSource;
    private AudioClip completeClip;

    private float slideX;
    private float shownAt = -1f;
    private bool completed;
    private Coroutine transition;

    public bool IsAnimating => transition != null;

    public void Apply(CanvasGroup panelGroup, TMP_Text keyText, Image keyBadge, TMP_Text messageText)
    {
        panel = (RectTransform)transform;
        group = panelGroup;

        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, BottomY);
        panel.sizeDelta = new Vector2(Width, panel.sizeDelta.y);

        if (messageText != null)
        {
            messageText.fontSize = 30f;
        }

        if (keyText != null)
        {
            keyText.fontSize = 28f;
        }

        if (keyBadge != null)
        {
            LayoutElement le = keyBadge.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minWidth = 72f;
                le.minHeight = 58f;
            }
        }

        VerticalLayoutGroup vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.padding = new RectOffset(30, 30, 18, 20);
            vlg.spacing = 12f;
        }

        var go = new GameObject("StepProgress", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(0);
        progressLabel = go.AddComponent<TextMeshProUGUI>();
        progressLabel.fontSize = 19f;
        progressLabel.characterSpacing = 4f;
        progressLabel.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
        progressLabel.alignment = TextAlignmentOptions.Left;
        progressLabel.raycastTarget = false;
        TMP_FontAsset font = UIFontConfig.UI;
        if (font != null)
        {
            progressLabel.font = font;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        completeClip = Resources.Load<AudioClip>("Sound/PickUp_Generic-003");
    }

    /// <summary>새 단계로 교체. 이미 보이는 카드가 있으면 빠지는 연출 뒤 applyContent를 부르고 들어오는 연출.</summary>
    public void ShowStep(int index, int total, System.Action applyContent)
    {
        if (transition != null)
        {
            StopCoroutine(transition);
        }

        transition = StartCoroutine(SwapRoutine(index, total, applyContent));
    }

    public void MarkComplete()
    {
        completed = true;
        if (audioSource != null && completeClip != null)
        {
            audioSource.PlayOneShot(completeClip, 0.55f);
        }
    }

    public void SetProgressText(string text)
    {
        if (progressLabel != null)
        {
            progressLabel.text = text;
        }
    }

    private IEnumerator SwapRoutine(int index, int total, System.Action applyContent)
    {
        if (group != null && group.alpha > 0.5f)
        {
            float a0 = group.alpha;
            for (float t = 0f; t < OutSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / OutSeconds;
                slideX = Mathf.Lerp(0f, -SlideDistance, k * k);
                group.alpha = Mathf.Lerp(a0, 0f, k);
                yield return null;
            }
        }

        applyContent?.Invoke();
        SetProgressText(total > 1 ? "단계 " + (index + 1) + " / " + total : string.Empty);
        completed = false;
        shownAt = Time.unscaledTime;

        for (float t = 0f; t < InSeconds; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / InSeconds, 3f);
            slideX = Mathf.Lerp(SlideDistance, 0f, k);
            if (group != null)
            {
                group.alpha = k;
            }

            yield return null;
        }

        slideX = 0f;
        if (group != null)
        {
            group.alpha = 1f;
        }

        transition = null;
    }

    private void LateUpdate()
    {
        if (panel == null)
        {
            return;
        }

        float shake = 0f;
        float scale = 1f;
        if (!completed && shownAt >= 0f && transition == null)
        {
            float waited = Time.unscaledTime - shownAt - NudgeAfter;
            if (waited > 0f)
            {
                float phase = waited % NudgeEvery;
                if (phase < NudgeSeconds)
                {
                    float k = phase / NudgeSeconds;
                    float env = Mathf.Sin(k * Mathf.PI);
                    shake = Mathf.Sin(k * Mathf.PI * 6f) * 10f * env;
                    scale = 1f + 0.06f * env;
                }
            }
        }

        panel.anchoredPosition = new Vector2(slideX + shake, BottomY);
        panel.localScale = new Vector3(scale, scale, 1f);
    }
}
