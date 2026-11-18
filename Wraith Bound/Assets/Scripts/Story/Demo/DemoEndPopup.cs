using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 챕터 1 클리어 후 허브로 돌아오면 '데모 버전의 시연은 여기까지' 팝업 (2026-11-19 사용자 요청). 클릭하면 사라짐.
/// 클리어 판정 = 팀원 ExitEventCube가 허브로 넘어가기 직전에 켜는 ScreenFader.ShouldWakeUpInHub(탈출구로 나갈 때만 켜짐 — 죽어서 허브로 가면 안 켜짐).
/// 허브의 HubSceneWakeUp이 Start에서 그 값을 끄므로, 그보다 먼저 오는 sceneLoaded에서 읽어 둠. 깨어나는 페이드가 끝나고 조작이 돌아온 뒤에 띄움.
/// 떠 있는 동안 조작 잠금(PlayerControlLock), 좌클릭·Space·Enter로 닫음.
/// </summary>
public class DemoEndPopup : MonoBehaviour
{
    private const string HubScene = "Hub";
    private const string Title = "데모 버전의 시연은 여기까지입니다";
    private const string Body = "끝까지 플레이해 주셔서 감사합니다.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || scene.name != HubScene || !ScreenFader.ShouldWakeUpInHub)
        {
            return;
        }

        var go = new GameObject("DemoEndPopup");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<DemoEndPopup>();
    }

    private CanvasGroup group;

    private IEnumerator Start()
    {
        // 허브 깨어나기(페이드 인) 동안 조작이 꺼져 있음 → 다시 켜질 때까지 기다림
        GameObject player = GameObject.FindWithTag("Player");
        CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;
        yield return new WaitForSeconds(1f);
        float limit = Time.unscaledTime + 10f;
        while (cc != null && !cc.enabled && Time.unscaledTime < limit)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        PlayerControlLock controlLock = player != null ? PlayerControlLock.Lock(player) : null;
        Build();
        yield return Fade(0f, 1f, 0.6f);
        yield return new WaitForSecondsRealtime(0.5f); // 바로 넘겨 버리지 않게

        while (!(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            yield return null;
        }

        yield return Fade(1f, 0f, 0.4f);
        controlLock?.Release();
        Destroy(gameObject);
    }

    private void Build()
    {
        Canvas canvas = MenuUIUtility.CreateOverlayCanvas("DemoEndCanvas", 900, transform);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>();
        dim.transform.SetParent(canvas.transform, false);
        RectTransform dr = dim.rectTransform;
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.offsetMin = dr.offsetMax = Vector2.zero;
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = false;

        // 바랜 종이 패널 + 붉은 인주 테두리(기 고르기 화면과 같은 결)
        var panel = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        panel.transform.SetParent(canvas.transform, false);
        panel.rectTransform.sizeDelta = new Vector2(920f, 300f);
        panel.color = new Color(0.16f, 0.13f, 0.11f, 0.96f);
        panel.raycastTarget = false;
        var border = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(0.62f, 0.12f, 0.1f, 0.9f);
        border.effectDistance = new Vector2(3f, -3f);

        Color ink = new Color(0.93f, 0.86f, 0.74f);
        TraitUIFactory.Label(panel.transform, "Title", Title, UIFontConfig.StoryBold, 44f, ink, new Vector2(0f, 60f), new Vector2(860f, 70f));
        TraitUIFactory.Label(panel.transform, "Body", Body, UIFontConfig.Story, 28f, new Color(ink.r, ink.g, ink.b, 0.85f), new Vector2(0f, -20f), new Vector2(820f, 60f), TextAlignmentOptions.Center, true);
        TextMeshProUGUI hint = TraitUIFactory.Label(panel.transform, "Hint", "클릭하여 닫기", UIFontConfig.UI, 20f, new Color(ink.r, ink.g, ink.b, 0.5f), new Vector2(0f, -105f), new Vector2(400f, 30f));
        hint.gameObject.AddComponent<DemoEndPopupBlink>();
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        group.alpha = to;
    }
}
