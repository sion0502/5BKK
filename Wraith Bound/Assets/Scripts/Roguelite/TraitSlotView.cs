using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 고르기 화면 위쪽 착용 칸 하나 — 노란 부적 종이. 비어 있으면 '+', 끼면 기 아이콘(붉은 먹)과 이름.
/// '기 1개만' 기를 끼면 남는 칸은 봉인됨(어둡게 ×). 클릭하면 onClick(착용 중인 기 or null).
/// </summary>
public class TraitSlotView
{
    private static readonly Color PaperColor = new Color(0.78f, 0.66f, 0.4f);
    private static readonly Color PaperSealed = new Color(0.22f, 0.2f, 0.17f);

    private readonly Image paper;
    private readonly TextMeshProUGUI plus;
    private readonly RawImage icon;
    private readonly TextMeshProUGUI nameLabel;
    private readonly Image highlight;
    private TraitDef current;

    public TraitSlotView(Transform parent, int index, Vector2 pos, Vector2 size, Action<TraitDef> onClick)
    {
        RectTransform root = TraitUIFactory.Centered(parent, "Slot_" + (index + 1), pos, size);
        root.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? 1.5f : -1.2f); // 손으로 붙인 듯 살짝 비뚤게

        // 선택 표시 (종이 뒤 붉은 테)
        RectTransform hl = TraitUIFactory.Centered(root, "Highlight", Vector2.zero, size + new Vector2(16f, 16f));
        highlight = TraitUIFactory.AddImage(hl, TraitAltarSkin.WavyPaper(20 + index), TraitUIFactory.Accent);

        paper = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Paper", Vector2.zero, size), TraitAltarSkin.WavyPaper(10 + index), PaperColor, raycast: true);
        Button button = paper.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(() => onClick?.Invoke(current));

        plus = TraitUIFactory.Label(root, "Plus", "+", UIFontConfig.UI, 96f, new Color(0.3f, 0.22f, 0.12f, 0.75f), new Vector2(0f, 6f), size);
        icon = TraitUIFactory.IconImage(root, "Icon", new Vector2(0f, 26f), size.x * 0.66f);
        icon.color = TraitUIFactory.Ink;
        nameLabel = TraitUIFactory.Label(root, "Name", string.Empty, UIFontConfig.UIBold, 26f, TraitUIFactory.Ink,
            new Vector2(0f, -size.y * 0.5f + 34f), new Vector2(size.x - 24f, 40f));
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 16f;
        nameLabel.fontSizeMax = 26f;
    }

    /// <param name="def">착용 중인 기 (없으면 null)</param>
    /// <param name="sealedSlot">'기 1개만' 때문에 못 쓰는 칸</param>
    public void Show(TraitDef def, bool sealedSlot, bool selected)
    {
        current = def;
        bool has = def != null;
        paper.color = sealedSlot ? PaperSealed : PaperColor;
        plus.text = sealedSlot ? "×" : "+";
        plus.color = sealedSlot ? new Color(0.5f, 0.12f, 0.1f, 0.8f) : new Color(0.3f, 0.22f, 0.12f, 0.75f);
        plus.enabled = !has;
        icon.enabled = has && icon.texture != null;
        if (has)
        {
            icon.texture = TraitUIFactory.Icon(def.Id);
            icon.enabled = icon.texture != null;
            nameLabel.text = def.Name;
        }
        else
        {
            nameLabel.text = sealedSlot ? "봉인됨" : string.Empty;
        }

        nameLabel.color = sealedSlot ? TraitUIFactory.TextDim : TraitUIFactory.Ink;
        highlight.enabled = selected && has;
    }
}
