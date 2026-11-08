using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 일지·쪽지 읽기 화면. CutsceneDocumentOverlay(종이 UI)를 런타임에 띄우고 조작을 잠근 뒤,
/// E·좌클릭·Space로 닫으면 closed 콜백. 시간은 멈추지 않음(공포 연출이 계속 흐르게).
/// </summary>
public class StoryDocumentReader : MonoBehaviour
{
    private const int SortingOrder = 650;
    private const float FadeDuration = 0.25f;
    private const float MinShowTime = 0.6f;

    public static bool IsOpen { get; private set; }

    private CutsceneDocumentOverlay overlay;
    private PlayerControlLock controlLock;
    private Action closed;

    public static void Open(string title, string body, Action onClosed = null)
    {
        if (IsOpen)
        {
            return;
        }

        GameObject go = new GameObject("StoryDocumentReader", typeof(RectTransform), typeof(CanvasGroup));
        go.SetActive(false); // Awake 전에 정렬 순서·투명도 지정
        CutsceneDocumentOverlay overlay = go.AddComponent<CutsceneDocumentOverlay>();
        StoryDocumentReader reader = go.AddComponent<StoryDocumentReader>();
        go.SetActive(true);

        overlay.SetSortingOrder(SortingOrder);
        overlay.SetText(title, body);
        overlay.Alpha = 0f;
        AddCloseHint(go.transform);

        reader.overlay = overlay;
        reader.closed = onClosed;
        GameObject player = GameObject.FindWithTag("Player");
        // 캠코더 화면을 보던 중이면 내림 — 켜 둔 채 읽으면 그동안 배터리가 닳았음
        CamcorderController camcorder = player != null ? player.GetComponentInChildren<CamcorderController>() : null;
        if (camcorder != null && camcorder.IsViewfinderActive)
        {
            camcorder.SetActive(false);
        }

        reader.controlLock = PlayerControlLock.Lock(player);
        IsOpen = true;
        reader.StartCoroutine(reader.Run());
    }

    // 화면 아래 "E · 클릭  닫기"
    private static void AddCloseHint(Transform root)
    {
        GameObject hintGo = new GameObject("CloseHint", typeof(RectTransform));
        hintGo.transform.SetParent(root, false);
        RectTransform rect = (RectTransform)hintGo.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(600f, 40f);

        TMPro.TextMeshProUGUI label = hintGo.AddComponent<TMPro.TextMeshProUGUI>();
        TMPro.TMP_FontAsset font = MenuUIUtility.LoadPolFont();
        if (font != null)
        {
            label.font = font;
        }

        label.text = "E · 클릭   닫기";
        label.fontSize = 24f;
        label.color = new Color(1f, 1f, 1f, 0.75f);
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false;
        TmpTextShadow.Apply(label);
    }

    private IEnumerator Run()
    {
        yield return Fade(0f, 1f);

        float shown = 0f;
        while (true)
        {
            shown += Time.unscaledDeltaTime;
            bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Space);
            if (shown >= MinShowTime && pressed)
            {
                break;
            }

            yield return null;
        }

        yield return Fade(1f, 0f);
        controlLock?.Release();
        IsOpen = false;
        Action callback = closed;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
        {
            overlay.Alpha = Mathf.Lerp(from, to, t / FadeDuration);
            yield return null;
        }

        overlay.Alpha = to;
    }

    private void OnDestroy()
    {
        if (IsOpen && overlay == null)
        {
            IsOpen = false;
        }
    }
}
