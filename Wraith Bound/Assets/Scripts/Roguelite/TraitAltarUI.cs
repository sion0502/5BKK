using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 고르기 화면 (허브 기 제단에서 E). 코드로 만든 오버레이 — 기 13종 카드(이름·효과·해금 조건과 진행)와 착용 칸.
/// 카드 클릭 = 착용/해제(TraitLoadout), 잠긴 기는 해금 조건만 보여 줌. E·ESC로 닫음. 열려 있는 동안 조작 잠금·커서 표시·일시정지 메뉴 막음.
/// </summary>
public class TraitAltarUI : MonoBehaviour
{
    private const int SortingOrder = 820;
    private static readonly Color CardColor = new Color(0.1f, 0.1f, 0.11f, 0.95f);
    private static readonly Color EquippedColor = new Color(0.42f, 0.07f, 0.06f, 0.95f);
    private static readonly Color LockedColor = new Color(0.06f, 0.06f, 0.06f, 0.92f);
    private static readonly Color TextColor = new Color(0.92f, 0.9f, 0.86f);
    private static readonly Color DimText = new Color(0.55f, 0.53f, 0.5f);
    private static readonly Color AccentRed = new Color(0.9f, 0.25f, 0.22f);

    public static bool IsOpen { get; private set; }

    private PlayerControlLock controlLock;
    private int openedFrame;
    private TMP_FontAsset font;
    private TextMeshProUGUI slotsLabel;
    private readonly List<(TraitDef def, Image bg, TextMeshProUGUI status)> cards = new List<(TraitDef, Image, TextMeshProUGUI)>();
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLock;

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
        font = MenuUIUtility.LoadPolFont();
        controlLock = PlayerControlLock.Lock(GameObject.FindWithTag("Player"));
        PauseMenuSuppressor.Suppress();
        cursorWasVisible = Cursor.visible;
        cursorWasLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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
    }

    private void Build()
    {
        Canvas canvas = MenuUIUtility.CreateOverlayCanvas("Canvas", SortingOrder, transform);
        Transform root = canvas.transform;
        MenuUIUtility.CreateFullScreenPanel(root, new Color(0f, 0f, 0f, 0.86f), "Backdrop");

        Label(root, "Title", "기", 64, new Vector2(0f, -70f), new Vector2(600f, 80f), TextAlignmentOptions.Center, AccentRed);
        slotsLabel = Label(root, "Slots", string.Empty, 26, new Vector2(0f, -140f), new Vector2(1600f, 44f), TextAlignmentOptions.Center, TextColor);

        const int columns = 5;
        const float w = 320f, h = 212f, gap = 18f;
        float left = -(columns * w + (columns - 1) * gap) * 0.5f + w * 0.5f;
        for (int i = 0; i < TraitCatalog.All.Count; i++)
        {
            TraitDef def = TraitCatalog.All[i];
            int row = i / columns, col = i % columns;
            Vector2 pos = new Vector2(left + col * (w + gap), -260f - row * (h + gap) - h * 0.5f);
            BuildCard(root, def, pos, new Vector2(w, h));
        }

        Label(root, "Hint", "클릭: 착용 / 해제      E · ESC: 닫기", 22, new Vector2(0f, 60f), new Vector2(1200f, 40f),
            TextAlignmentOptions.Center, DimText, bottom: true);
    }

    private void BuildCard(Transform parent, TraitDef def, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(def.Name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image bg = go.GetComponent<Image>();
        go.GetComponent<Button>().onClick.AddListener(() =>
        {
            TraitLoadout.Toggle(def.Id);
            Refresh();
        });

        Label(go.transform, "Name", def.Name, 30, new Vector2(0f, -22f), new Vector2(size.x - 28f, 40f), TextAlignmentOptions.TopLeft, TextColor);
        TextMeshProUGUI effect = Label(go.transform, "Effect", def.Effect, 18, new Vector2(0f, -66f), new Vector2(size.x - 28f, 100f), TextAlignmentOptions.TopLeft, DimText);
        effect.textWrappingMode = TextWrappingModes.Normal;
        effect.enableAutoSizing = true;
        effect.fontSizeMin = 13f;
        effect.fontSizeMax = 18f;
        TextMeshProUGUI status = Label(go.transform, "Status", string.Empty, 17, new Vector2(0f, 14f), new Vector2(size.x - 28f, 44f), TextAlignmentOptions.BottomLeft, AccentRed, bottom: true);
        status.textWrappingMode = TextWrappingModes.Normal;
        cards.Add((def, bg, status));
    }

    private void Refresh()
    {
        var names = new List<string>();
        foreach (TraitId id in TraitLoadout.Equipped)
        {
            names.Add(TraitCatalog.Get(id).Name);
        }

        int max = TraitLoadout.MaxSlots;
        while (names.Count < max)
        {
            names.Add("— 비어 있음 —");
        }

        slotsLabel.text = "착용 (" + TraitLoadout.Equipped.Count + "/" + max + ")    " + string.Join("     ", names);

        foreach (var (def, bg, status) in cards)
        {
            bool unlocked = def.Implemented && TraitProgress.IsUnlocked(def.Id);
            bool equipped = TraitLoadout.IsEquipped(def.Id);
            bg.color = equipped ? EquippedColor : unlocked ? CardColor : LockedColor;
            if (!def.Implemented)
            {
                status.text = "추후 공개";
                status.color = DimText;
            }
            else if (!unlocked)
            {
                int cur = Mathf.Min(TraitProgress.Get(def.Counter), def.Goal);
                status.text = "잠김 · " + def.UnlockText + (def.Goal > 1 ? " (" + cur + "/" + def.Goal + ")" : string.Empty);
                status.color = DimText;
            }
            else
            {
                status.text = equipped ? "착용 중" : def.SingleSlot ? "클릭해서 착용 · 기 1개만" : "클릭해서 착용";
                status.color = equipped ? TextColor : AccentRed;
            }
        }
    }

    private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box,
        TextAlignmentOptions align, Color color, bool bottom = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = box;
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
}
