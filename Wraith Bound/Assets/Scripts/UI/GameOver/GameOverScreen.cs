using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임오버 화면 — "바닥에 떨어져 깨진 캠코더 뷰파인더" 컨셉.
/// 점프스케어 암전 위에서 시작: 노이즈 → 떨어진 캠코더 화면(나이트비전, 괴물을 올려다봄, 깨진 액정)이 켜짐 → REC가 STOP으로 바뀜
/// → SIGNAL LOST → 제목 → 선택지(처음으로 / 허브로 돌아가기). 뷰파인더 화면은 배경으로 계속 남음.
/// UI는 코드로 생성. 시간 정지(timeScale=0) 상태라 연출은 unscaled 시간 기준.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    private const int SortingOrder = 5000; // 일시정지 메뉴·HUD보다 위
    private const float InputDelay = 0.7f; // 선택지가 보인 뒤 실수로 눌리지 않게 막는 시간

    private static readonly Color RecRed = new Color(0.92f, 0.12f, 0.1f, 1f);
    private static readonly Color HudWhite = new Color(0.93f, 0.93f, 0.9f, 1f);
    private static readonly Color DimGray = new Color(0.72f, 0.72f, 0.72f, 1f);

    private float survivalSeconds;
    private TMP_FontAsset font;
    private Sprite circleSprite;
    private GameOverTapeEffects effects;
    private GameOverDroppedCameraView droppedView;

    private CanvasGroup feedGroup;
    private CanvasGroup backdropGroup;
    private CanvasGroup hudGroup;
    private Image batteryIcon;
    private Texture2D backdropTexture;
    private Image recDot;
    private Image stopSquare;
    private TextMeshProUGUI recLabel;
    private TextMeshProUGUI timecode;
    private TextMeshProUGUI signalLost;
    private TextMeshProUGUI title;
    private TextMeshProUGUI subtitle;
    private CanvasGroup menuGroup;
    private CanvasGroup fadeGroup;
    private GameOverButton restartButton;
    private GameOverButton hubButton;
    private bool stopped;
    private bool leaving;

    public static GameOverScreen Show(float survivalSeconds)
    {
        var go = new GameObject("GameOverScreen");
        var screen = go.AddComponent<GameOverScreen>();
        screen.survivalSeconds = survivalSeconds;
        return screen;
    }

    private void Start()
    {
        SuspendConflictingBehaviours();
        AudioListener.pause = true; // 게임 소리는 끊고 테이프 잡음만 (잡음은 ignoreListenerPause)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureEventSystem();

        font = MenuUIUtility.LoadPolFont();
        circleSprite = CreateCircleSprite();
        Build();
        StartCoroutine(Sequence());
    }

    // 일시정지 메뉴(ESC)와 점프스케어의 OnGUI 암전은 이 화면 위에 그려지거나 timeScale을 바꾸므로 끈다.
    // 해당 파일들은 수정하지 않음. 씬 전환 시 함께 사라짐.
    private static void SuspendConflictingBehaviours()
    {
        foreach (var pause in FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            pause.enabled = false;
        // 점프스케어는 즉시 제거 — OnDestroy가 카메라와 괴물을 연출 전 위치로 되돌림
        // (연출 중 괴물은 얼굴 높이에 맞추느라 몸이 바닥에 파묻혀 있어 떨어진 캠코더 화면에 이상하게 찍힘)
        foreach (var scare in FindObjectsByType<EnemyJumpscare>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroyImmediate(scare.gameObject);

        // 씬 시작 페이드(ScreenFader, sortingOrder 32767)가 도중에 멈춰 있으면 이 화면을 가리므로 투명하게
        foreach (string name in new[] { "FadeImage", "ScreenFadeOverlay" })
        {
            GameObject fadeRoot = GameObject.Find(name);
            if (fadeRoot != null)
                ScreenFader.SetAlpha(fadeRoot.GetComponentInChildren<Image>(true), 0f);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ───────────────────────────── 화면 구성 ─────────────────────────────

    private void Build()
    {
        Canvas canvas = MenuUIUtility.CreateOverlayCanvas("GameOverCanvas", SortingOrder, transform);
        var root = (RectTransform)canvas.transform;

        MenuUIUtility.CreateFullScreenPanel(root, Color.black, "Background");

        RectTransform content = CreateStretch("Content", root);
        BuildFeed(content);
        BuildHud(content);
        BuildCenter(content);
        BuildMenu(content);

        RectTransform overlay = CreateStretch("TapeOverlay", root);
        effects = gameObject.AddComponent<GameOverTapeEffects>();
        effects.Build(overlay, content);

        RectTransform fade = CreateStretch("Fade", root);
        var fadeImage = fade.gameObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    // 떨어진 캠코더 화면 + 깨진 액정. 카메라를 못 만들면(메인 카메라 없음) 검은 배경 그대로
    private void BuildFeed(RectTransform parent)
    {
        RectTransform feed = CreateStretch("DroppedCamcorderFeed", parent);
        feedGroup = feed.gameObject.AddComponent<CanvasGroup>();
        feedGroup.alpha = 0f;
        feedGroup.blocksRaycasts = false;

        droppedView = GameOverDroppedCameraView.Create(transform);
        if (droppedView == null)
            return;

        RectTransform image = CreateStretch("Feed", feed);
        var raw = image.gameObject.AddComponent<RawImage>();
        raw.texture = droppedView.Texture;
        raw.raycastTarget = false;

        feed.gameObject.AddComponent<GameOverCrackedScreen>().Build(feed, UnityEngine.Random.Range(0, int.MaxValue));
    }

    private void BuildHud(RectTransform parent)
    {
        RectTransform hud = CreateStretch("CamcorderHud", parent);
        hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
        hudGroup.alpha = 0f;

        // 실제 캠코더 뷰파인더 스프라이트(View_Camcorder.prefab)가 있으면 그대로, 없으면 모서리 괄호를 그림
        CamcorderController camcorder = droppedView != null ? droppedView.Camcorder : null;
        if (camcorder != null && camcorder.FrameSprite != null)
        {
            Image frame = CreateImage("Frame", hud, camcorder.FrameSprite, HudWhite);
            MakeStretch(frame.rectTransform);
        }
        else
        {
            const float inset = 60f, arm = 70f, thick = 4f;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var dir = new Vector2(corner.x == 0 ? 1 : -1, corner.y == 0 ? 1 : -1);
                Vector2 pos = new Vector2(dir.x * inset, dir.y * inset);
                CreateBar(hud, corner, pos, new Vector2(arm, thick), new Vector2(corner.x, 0.5f));
                CreateBar(hud, corner, pos, new Vector2(thick, arm), new Vector2(0.5f, corner.y));
            }
        }

        // 좌상단 REC/STOP
        Sprite dotSprite = camcorder != null && camcorder.RecordingDotSprite != null ? camcorder.RecordingDotSprite : circleSprite;
        recDot = CreateImage("RecDot", hud, dotSprite, RecRed);
        Place(recDot.rectTransform, new Vector2(0f, 1f), new Vector2(130f, -120f), new Vector2(30f, 30f));
        stopSquare = CreateImage("StopSquare", hud, null, HudWhite);
        Place(stopSquare.rectTransform, new Vector2(0f, 1f), new Vector2(130f, -120f), new Vector2(26f, 26f));
        stopSquare.enabled = false;
        recLabel = CreateText("RecLabel", hud, "REC", 40f, RecRed, TextAlignmentOptions.Left);
        Place(recLabel.rectTransform, new Vector2(0f, 1f), new Vector2(260f, -120f), new Vector2(200f, 50f));

        // 우상단 배터리(거의 방전) + 타임코드 = 생존 시간
        if (camcorder != null && camcorder.LowestBatterySprite != null)
        {
            batteryIcon = CreateImage("Battery", hud, camcorder.LowestBatterySprite, HudWhite);
            batteryIcon.preserveAspect = true;
            Place(batteryIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-150f, -120f), new Vector2(100f, 46f));
        }
        timecode = CreateText("Timecode", hud, FormatTime(survivalSeconds), 40f, HudWhite, TextAlignmentOptions.Right);
        Place(timecode.rectTransform, new Vector2(1f, 1f), new Vector2(-370f, -120f), new Vector2(300f, 50f));
        var sp = CreateText("TapeSpeed", hud, "SP", 28f, HudWhite, TextAlignmentOptions.Right);
        Place(sp.rectTransform, new Vector2(1f, 1f), new Vector2(-370f, -168f), new Vector2(300f, 40f));

        // 하단 날짜/모드 (실제 시스템 시각 — 파운드 푸티지 느낌)
        var date = CreateText("Date", hud, DateTime.Now.ToString("yyyy.MM.dd  HH:mm"), 30f, HudWhite, TextAlignmentOptions.Right);
        Place(date.rectTransform, new Vector2(1f, 0f), new Vector2(-300f, 120f), new Vector2(400f, 44f));
        var mode = CreateText("Mode", hud, "NIGHT SHOT", 30f, HudWhite, TextAlignmentOptions.Left);
        Place(mode.rectTransform, new Vector2(0f, 0f), new Vector2(270f, 120f), new Vector2(300f, 44f));
    }

    private void BuildCenter(RectTransform parent)
    {
        // 뷰파인더 화면 위에서도 글자가 읽히도록 가운데 어두운 띠
        RectTransform backdrop = CreateStretch("TextBackdrop", parent);
        backdrop.anchorMin = new Vector2(0f, 0.5f);
        backdrop.anchorMax = new Vector2(1f, 0.5f);
        backdrop.sizeDelta = new Vector2(0f, 640f);
        backdrop.anchoredPosition = new Vector2(0f, -40f);
        var band = backdrop.gameObject.AddComponent<RawImage>();
        backdropTexture = CreateBandTexture();
        band.texture = backdropTexture;
        band.raycastTarget = false;
        backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
        backdropGroup.alpha = 0f;
        backdropGroup.blocksRaycasts = false;

        signalLost = CreateText("SignalLost", parent, "SIGNAL LOST", 34f, DimGray, TextAlignmentOptions.Center);
        signalLost.characterSpacing = 30f;
        Place(signalLost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 50f));
        signalLost.maxVisibleCharacters = 0;

        // 메인메뉴와 같은 "鬼行" 제목 (같은 폰트·빨간색)
        title = CreateText("Title", parent, TitleFontLoader.TitleText, 170f, TitleFontLoader.TitleRed, TextAlignmentOptions.Center);
        TMP_FontAsset titleFont = TitleFontLoader.Load();
        if (titleFont != null)
            title.font = titleFont;
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 220f));
        title.alpha = 0f;

        subtitle = CreateText("Subtitle", parent, $"생존 시간  {FormatTime(survivalSeconds)}", 34f, DimGray, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(900f, 50f));
        subtitle.alpha = 0f;
    }

    private void BuildMenu(RectTransform parent)
    {
        RectTransform menu = CreateStretch("Menu", parent);
        menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
        menuGroup.alpha = 0f;
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;

        restartButton = CreateButton(menu, "처음으로", new Vector2(-260f, -230f), () => Leave(GameOverNavigation.RestartChapter));
        hubButton = CreateButton(menu, "허브로 돌아가기", new Vector2(260f, -230f), () => Leave(GameOverNavigation.GoToHub));

        // 좌우 방향키로 이동
        var left = restartButton.Button.navigation;
        left.mode = Navigation.Mode.Explicit;
        left.selectOnRight = hubButton.Button;
        restartButton.Button.navigation = left;
        var right = hubButton.Button.navigation;
        right.mode = Navigation.Mode.Explicit;
        right.selectOnLeft = restartButton.Button;
        hubButton.Button.navigation = right;
    }

    private GameOverButton CreateButton(RectTransform parent, string text, Vector2 position, Action onClick)
    {
        var go = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), position, new Vector2(440f, 90f));
        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); // 클릭 영역용 투명 이미지
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        var label = CreateText("Label", (RectTransform)go.transform, text, 46f, DimGray, TextAlignmentOptions.Center);
        Place(label.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 70f));

        float halfWidth = label.GetPreferredValues(text).x * 0.5f;
        var marker = CreateImage("Marker", (RectTransform)go.transform, null, HudWhite);
        Place(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-halfWidth - 30f, 0f), new Vector2(14f, 14f));
        var underline = CreateImage("Underline", (RectTransform)go.transform, null, HudWhite);
        Place(underline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(halfWidth * 2f + 20f, 3f));

        var gameOverButton = go.AddComponent<GameOverButton>();
        gameOverButton.Init(label, marker, underline);
        return gameOverButton;
    }

    // ───────────────────────────── 연출 순서 ─────────────────────────────

    private IEnumerator Sequence()
    {
        effects.SetBaseStrength(1f);
        effects.Burst(1f, 0.5f);
        yield return Wait(0.4f);

        // 떨어진 캠코더 화면이 접촉 불량처럼 몇 번 깜빡이다 켜짐
        float[] flicker = { 1f, 0f, 1f, 0.3f, 1f };
        foreach (float a in flicker)
        {
            feedGroup.alpha = a;
            effects.Burst(UnityEngine.Random.Range(0.6f, 0.9f), 0.06f);
            yield return Wait(UnityEngine.Random.Range(0.05f, 0.12f));
        }
        feedGroup.alpha = 1f;
        yield return Fade(a => hudGroup.alpha = a, 0f, 1f, 0.2f);
        yield return Fade(effects.SetBaseStrength, 1f, 0.22f, 0.5f);

        // 녹화 중인 화면을 잠깐 보여줌 (괴물이 그대로 서 있음)
        yield return Wait(1.6f);

        // 끊김: REC → STOP
        effects.Burst(1f, 0.25f);
        stopped = true;
        recDot.enabled = false;
        stopSquare.enabled = true;
        recLabel.text = "STOP";
        recLabel.color = HudWhite;
        yield return Fade(effects.SetBaseStrength, 0.6f, 0.15f, 0.5f);

        yield return Fade(a => backdropGroup.alpha = a, 0f, 1f, 0.4f);
        yield return Typewriter(signalLost, 0.045f);
        yield return Wait(0.3f);

        effects.Burst(0.7f, 0.15f);
        yield return Fade(a => title.alpha = a, 0f, 1f, 0.8f);
        yield return Fade(a => subtitle.alpha = a, 0f, 1f, 0.5f);

        effects.EnterIdle();
        yield return Fade(a => menuGroup.alpha = a, 0f, 1f, 0.5f);
        yield return Wait(InputDelay);

        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
    }

    private void Update()
    {
        if (stopped && timecode != null)
        {
            // 정지된 캠코더처럼 타임코드가 천천히 깜빡임
            timecode.alpha = Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f ? 1f : 0.15f;
        }
        else if (recDot != null)
        {
            recDot.enabled = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.3f;
        }

        // 배터리 거의 방전 깜빡임
        if (batteryIcon != null)
            batteryIcon.enabled = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.55f;
    }

    private void Leave(Action load)
    {
        if (leaving)
            return;
        leaving = true;
        menuGroup.interactable = false;
        StartCoroutine(LeaveRoutine(load));
    }

    private IEnumerator LeaveRoutine(Action load)
    {
        effects.SetBaseStrength(1f);
        effects.Burst(1f, 0.6f);
        yield return Fade(a => fadeGroup.alpha = a, 0f, 1f, 0.45f);
        load();
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private static IEnumerator Wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
    }

    private static IEnumerator Fade(Action<float> apply, float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            apply(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        apply(to);
    }

    private static IEnumerator Typewriter(TextMeshProUGUI text, float perChar)
    {
        text.ForceMeshUpdate();
        int count = text.textInfo.characterCount;
        for (int i = 1; i <= count; i++)
        {
            text.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(perChar);
        }
    }

    private static string FormatTime(float seconds)
    {
        var t = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
    }

    private static RectTransform CreateStretch(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void MakeStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 세로 그라데이션 띠: 가운데 진하고 위아래로 사라짐
    private static Texture2D CreateBandTexture()
    {
        const int height = 64;
        var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < height; y++)
        {
            float t = 1f - Mathf.Abs(y / (height - 1f) * 2f - 1f);
            // 가운데 넓게 평평하고 위아래 끝에서만 사라지게
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, 0.82f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.5f))));
        }
        tex.Apply();
        return tex;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void CreateBar(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        Image bar = CreateImage("CornerBar", parent, null, HudWhite);
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (circleSprite != null)
        {
            Destroy(circleSprite.texture);
            Destroy(circleSprite);
        }
        if (backdropTexture != null)
            Destroy(backdropTexture);
    }
}
