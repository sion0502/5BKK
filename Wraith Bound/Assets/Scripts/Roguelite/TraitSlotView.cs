using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 고르기 화면 위쪽 착용 칸 하나 — 낡은 부적(바랜 누런 종이·그을린 가장자리·붉은 인주 테두리).
/// 비어 있으면 흐린 '+', 끼면 기 아이콘과 이름이 붉은 먹으로. '기 1개만' 기를 끼면 남는 칸은 봉인됨(어둡게 ×).
/// 클릭하면 onClick(착용 중인 기 or null).
/// </summary>
public class TraitSlotView
{
    private static readonly Color PaperNormal = Color.white;              // 스프라이트 자체 색 그대로
    private static readonly Color PaperSealed = new Color(0.38f, 0.36f, 0.34f);
    private static readonly Color InkColor = new Color(0.45f, 0.06f, 0.04f);
    private static readonly Color BorderColor = new Color(0.5f, 0.07f, 0.05f, 0.7f);
    private static readonly Color PlusColor = new Color(0.22f, 0.12f, 0.06f, 0.55f);

    private readonly Image paper;
    private readonly Image border;
    private readonly TextMeshProUGUI plus;
    private readonly RawImage icon;
    private readonly TextMeshProUGUI nameLabel;
    private readonly Image highlight;
    private TraitDef current;

    public TraitSlotView(Transform parent, int index, Vector2 pos, Vector2 size, Action<TraitDef> onClick)
    {
        RectTransform root = TraitUIFactory.Centered(parent, "Slot_" + (index + 1), pos, size);
        root.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? 2f : -1.6f); // 손으로 붙인 듯 살짝 비뚤게

        // 그림자 (어두운 바탕에서 종이가 떠 보이게)
        TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Shadow", new Vector2(7f, -9f), size), TraitAltarSkin.WavyPaper(30 + index),
            new Color(0f, 0f, 0f, 0.55f));

        // 고른 칸 표시 (종이 뒤로 붉게 번지는 테)
        highlight = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Highlight", Vector2.zero, size + new Vector2(18f, 18f)),
            TraitAltarSkin.WavyPaper(20 + index), new Color(0.75f, 0.12f, 0.08f, 0.8f));

        paper = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Paper", Vector2.zero, size), TraitAltarSkin.AgedTalisman(index), PaperNormal, raycast: true);
        Button button = paper.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.86f, 0.86f, 0.86f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1.15f;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(() => onClick?.Invoke(current));

        // 붉은 인주 테두리 (손으로 그은 듯한 메뉴 테두리 스프라이트를 안쪽으로)
        RectTransform borderRect = TraitUIFactory.Centered(root, "SealBorder", Vector2.zero, size - new Vector2(40f, 44f));
        border = TraitUIFactory.AddImage(borderRect, MenuSkin.Frame, BorderColor, type: Image.Type.Sliced);
        border.gameObject.SetActive(MenuSkin.Frame != null); // 테두리 그림이 없으면 단색 사각형이 되므로 생략

        plus = TraitUIFactory.Label(root, "Plus", "+", UIFontConfig.UI, 84f, PlusColor, new Vector2(0f, 4f), size);
        icon = TraitUIFactory.IconImage(root, "Icon", new Vector2(0f, 22f), size.x * 0.56f);
        icon.color = InkColor;
        nameLabel = TraitUIFactory.Label(root, "Name", string.Empty, UIFontConfig.UIBold, 24f, InkColor,
            new Vector2(0f, -size.y * 0.5f + 46f), new Vector2(size.x - 56f, 36f));
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 15f;
        nameLabel.fontSizeMax = 24f;
    }

    /// <param name="def">착용 중인 기 (없으면 null)</param>
    /// <param name="sealedSlot">'기 1개만' 때문에 못 쓰는 칸</param>
    public void Show(TraitDef def, bool sealedSlot, bool selected)
    {
        current = def;
        bool has = def != null;
        paper.color = sealedSlot ? PaperSealed : PaperNormal;
        border.color = sealedSlot ? new Color(0.25f, 0.05f, 0.04f, 0.5f) : BorderColor;
        plus.text = sealedSlot ? "×" : "+";
        plus.color = sealedSlot ? new Color(0.4f, 0.07f, 0.05f, 0.75f) : PlusColor;
        plus.enabled = !has;
        if (has)
        {
            icon.texture = TraitUIFactory.Icon(def.Id);
            nameLabel.text = def.Name;
        }
        else
        {
            nameLabel.text = sealedSlot ? "봉인됨" : string.Empty;
        }

        icon.enabled = has && icon.texture != null;
        nameLabel.color = sealedSlot ? new Color(0.3f, 0.22f, 0.18f) : InkColor;
        highlight.enabled = selected && has;
    }
}
