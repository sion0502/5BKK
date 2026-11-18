using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 고르기 화면 (허브 기 제단에서 E). 코드로 만든 오버레이 — 2026-11-18 사용자 스케치대로 개편:
///  · 위: 제목 '기' + 착용 칸(노란 부적 종이, 비면 '+') — 칸 클릭 = 해제 (TraitSlotView)
///  · 아래 패널 왼쪽: 고른 기 상세(큰 그림·이름·효과) + [장착]/[해제] (TraitDetailPanel)
///  · 아래 패널 오른쪽: 기 카드 목록 3열, 세로 스크롤(다음 줄이 살짝 보임) + 오른쪽 스크롤바 (TraitCardView)
///    카드 클릭 = 고르기(붉은 테두리), 잠긴 기는 빗금+자물쇠+해금 조건
/// E·ESC로 닫음. 열려 있는 동안 조작 잠금·커서 표시·일시정지 메뉴 막음.
/// </summary>
public class TraitAltarUI : MonoBehaviour
{
    private const int SortingOrder = 820;
    private const int Columns = 3;
    private static readonly Vector2 CardSize = new Vector2(300f, 380f);
    private static readonly Vector2 CardGap = new Vector2(44f, 40f);
    private static readonly Vector2 SlotSize = new Vector2(170f, 220f);
    private static readonly Vector2 PanelSize = new Vector2(1660f, 600f);
    private const float PanelCenterY = -192f;
    private const float DetailWidth = 480f;

    public static bool IsOpen { get; private set; }

    private PlayerControlLock controlLock;
    private int openedFrame;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLock;

    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private readonly List<TraitSlotView> slots = new List<TraitSlotView>();
    private readonly List<TraitCardView> cards = new List<TraitCardView>();
    private TraitDetailPanel detail;
    private TraitDef selected;

    public static void Open()
    {
        if (IsOpen)
        {
            return;
        }

        new GameObject("TraitAltarUI").AddComponent<TraitAltarUI>();
    }

    private void Awake()
    {
        IsOpen = true;
        openedFrame = Time.frameCount;
        controlLock = PlayerControlLock.Lock(GameObject.FindWithTag("Player"));
        PauseMenuSuppressor.Suppress();
        cursorWasVisible = Cursor.visible;
        cursorWasLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        selected = TraitLoadout.Equipped.Count > 0 ? TraitCatalog.Get(TraitLoadout.Equipped[0]) : TraitCatalog.All[0];
        HideOtherOverlays();
        Build();
        Refresh();
    }

    private void Update()
    {
        if (Time.frameCount == openedFrame)
        {
            return; // 연 E가 같은 프레임에 닫지 않게
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        IsOpen = false;
        controlLock?.Release();
        PauseMenuSuppressor.Release();
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorWasLock;
        foreach (Canvas c in hiddenCanvases)
        {
            if (c != null)
            {
                c.enabled = true;
            }
        }
    }

    // HUD·튜토리얼 안내 등 다른 화면 오버레이가 이 화면 위에 그려지던 것 — 열려 있는 동안만 끔 (안내 문구·자막은 그대로)
    private void HideOtherOverlays()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay
                && c.sortingOrder < 600 && !c.transform.IsChildOf(transform))
            {
                c.enabled = false;
                hiddenCanvases.Add(c);
            }
        }

        foreach (TutorialPromptUI prompt in FindObjectsByType<TutorialPromptUI>(FindObjectsSortMode.None))
        {
            foreach (Canvas c in prompt.GetComponentsInParent<Canvas>())
            {
                if (c.enabled && c.isRootCanvas)
                {
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
            }
        }
    }

    // ── 화면 구성 ───────────────────────────────────

    private void Build()
    {
        Canvas canvas = MenuUIUtility.CreateOverlayCanvas("Canvas", SortingOrder, transform);
        Transform root = canvas.transform;
        MenuUIUtility.CreateFullScreenPanel(root, new Color(0f, 0f, 0f, 0.88f), "Backdrop");

        BuildTitle(root);

        // 착용 칸 (기본 2칸) — 낡은 부적
        for (int i = 0; i < TraitLoadout.DefaultSlots; i++)
        {
            float x = (i - (TraitLoadout.DefaultSlots - 1) * 0.5f) * (SlotSize.x + 80f);
            slots.Add(new TraitSlotView(root, i, new Vector2(x, 236f), SlotSize, OnSlotClicked));
        }

        // 아래 패널
        RectTransform panel = MenuUIUtility.CreateCenteredPanel(root, "Panel", new Vector2(0f, PanelCenterY), PanelSize, new Color(0.08f, 0.075f, 0.07f, 0.96f));

        float left = -PanelSize.x * 0.5f;
        detail = new TraitDetailPanel(panel, new Vector2(left + DetailWidth * 0.5f, 0f), new Vector2(DetailWidth - 20f, PanelSize.y - 50f), OnEquipClicked);

        // 세로 구분선
        RectTransform divider = TraitUIFactory.Centered(panel, "Divider", new Vector2(left + DetailWidth, 0f), new Vector2(2f, PanelSize.y - 60f));
        TraitUIFactory.AddImage(divider, null, new Color(0.8f, 0.76f, 0.7f, 0.25f));

        BuildList(panel, left + DetailWidth + 20f);

        TraitUIFactory.Label(root, "Hint", "카드 클릭: 고르기      장착 칸 클릭: 해제      E · ESC: 닫기", UIFontConfig.UI, 21f, TraitUIFactory.TextDim,
            new Vector2(0f, -518f), new Vector2(1400f, 36f));
    }

    // 제목: 메인메뉴 鬼行과 같은 붉은 붓글씨 '气' + 아래 작게 '기'.
    // 붓글씨 폰트엔 氣(정자)가 없고 간체 气만 있어서 气를 씀. 폰트를 못 읽으면 예전처럼 '기'만.
    private static void BuildTitle(Transform root)
    {
        TMP_FontAsset brush = TitleFontLoader.Load();
        bool hasBrush = brush != null && brush.TryAddCharacters("气", out string missing) && string.IsNullOrEmpty(missing);
        if (hasBrush)
        {
            TextMeshProUGUI glyph = TraitUIFactory.Label(root, "TitleBrush", "气", brush, 118f, TitleFontLoader.TitleRed,
                new Vector2(0f, 452f), new Vector2(300f, 150f));
            glyph.characterSpacing = 0f;
            TextMeshProUGUI sub = TraitUIFactory.Label(root, "TitleSub", "기", UIFontConfig.UI, 22f, TraitUIFactory.TextDim,
                new Vector2(0f, 372f), new Vector2(300f, 30f));
            sub.characterSpacing = 30f;
        }
        else
        {
            TextMeshProUGUI title = MenuUIUtility.CreateHeader(root, "기", UIFontConfig.UIBold, new Vector2(0f, 462f), 360f);
            title.fontSize = 50f;
        }
    }

    // 오른쪽 카드 목록: ScrollRect(세로) + 3열 GridLayoutGroup + 오른쪽 스크롤바
    private void BuildList(RectTransform panel, float listLeft)
    {
        const float scrollbarWidth = 14f;
        float right = PanelSize.x * 0.5f - 24f;
        float listWidth = right - scrollbarWidth - 16f - listLeft;
        float listHeight = PanelSize.y - 40f;
        RectTransform viewport = TraitUIFactory.Centered(panel, "Viewport", new Vector2(listLeft + listWidth * 0.5f, 0f), new Vector2(listWidth, listHeight));
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>(); // 빈 곳에서도 휠 스크롤이 먹게
        hit.color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = TraitUIFactory.Rect(viewport, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(listWidth, 0f));
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = CardSize;
        grid.spacing = CardGap;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 26, 26);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < TraitCatalog.All.Count; i++)
        {
            cards.Add(new TraitCardView(content, TraitCatalog.All[i], i, CardSize, OnCardClicked));
        }

        // 스크롤바 (가는 홈 + 손잡이)
        RectTransform barRect = TraitUIFactory.Centered(panel, "Scrollbar", new Vector2(right - scrollbarWidth * 0.5f, 0f), new Vector2(scrollbarWidth, listHeight - 20f));
        TraitUIFactory.AddImage(barRect, null, new Color(1f, 1f, 1f, 0.07f), raycast: true);
        RectTransform sliding = TraitUIFactory.Rect(barRect, "SlidingArea", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        sliding.anchorMin = Vector2.zero;
        sliding.anchorMax = Vector2.one;
        sliding.offsetMin = sliding.offsetMax = Vector2.zero;
        RectTransform handle = TraitUIFactory.Rect(sliding, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        Image handleImage = TraitUIFactory.AddImage(handle, null, new Color(0.82f, 0.78f, 0.72f, 0.55f), raycast: true);
        Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        Navigation nav = scrollbar.navigation;
        nav.mode = Navigation.Mode.None;
        scrollbar.navigation = nav;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.inertia = true;
        scroll.decelerationRate = 0.08f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 1f;
    }

    // ── 입력 ────────────────────────────────────────

    private void OnCardClicked(TraitDef def)
    {
        selected = def;
        Refresh();
    }

    private void OnSlotClicked(TraitDef equipped)
    {
        if (equipped == null)
        {
            return;
        }

        TraitLoadout.Toggle(equipped.Id); // 착용 칸 클릭 = 해제
        selected = equipped;
        Refresh();
    }

    private void OnEquipClicked(TraitDef def)
    {
        if (def == null || !TraitUIFactory.IsUnlocked(def))
        {
            return;
        }

        TraitLoadout.Toggle(def.Id); // 착용 중이면 해제, 아니면 장착(칸이 차면 먼저 낀 것과 교체)
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<TraitId> equipped = TraitLoadout.Equipped;
        int max = TraitLoadout.MaxSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            TraitDef def = i < equipped.Count ? TraitCatalog.Get(equipped[i]) : null;
            slots[i].Show(def, i >= max, def != null && def == selected);
        }

        foreach (TraitCardView card in cards)
        {
            card.Refresh(card.Def == selected);
        }

        detail.Show(selected);
    }
}
