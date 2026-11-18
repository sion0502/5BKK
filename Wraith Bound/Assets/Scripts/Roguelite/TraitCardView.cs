using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 목록 카드 하나 — 위는 그림, 아래는 이름. 잠긴 기는 빗금+자물쇠, 이름 자리에 해금 조건.
/// 고른 카드는 붉은 테두리, 착용 중이면 오른쪽 위 '착' 도장. 클릭하면 onClick(def).
/// </summary>
public class TraitCardView
{
    private static readonly Color PaperColor = new Color(0.2f, 0.19f, 0.18f);
    private static readonly Color PaperLocked = new Color(0.12f, 0.115f, 0.11f);
    private static readonly Color ImageBox = new Color(0.07f, 0.065f, 0.06f, 0.95f);

    public TraitDef Def { get; }

    private readonly Image paper;
    private readonly RawImage icon;
    private readonly TextMeshProUGUI nameLabel;
    private readonly TextMeshProUGUI conditionLabel;
    private readonly Image hatch;
    private readonly Image lockImage;
    private readonly Image selectFrame;
    private readonly GameObject stamp;

    public TraitCardView(Transform parent, TraitDef def, int index, Vector2 size, Action<TraitDef> onClick)
    {
        Def = def;
        // GridLayoutGroup이 위치·크기를 정함
        GameObject go = new GameObject(def.Name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform root = (RectTransform)go.transform;

        // 선택 테두리 (종이 뒤로 살짝 크게)
        RectTransform frameRect = TraitUIFactory.Centered(root, "SelectFrame", Vector2.zero, size + new Vector2(14f, 14f));
        selectFrame = TraitUIFactory.AddImage(frameRect, TraitAltarSkin.WavyPaper(100 + index), TraitUIFactory.Accent);

        paper = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Paper", Vector2.zero, size), TraitAltarSkin.WavyPaper(40 + index), PaperColor, raycast: true);
        Button button = paper.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.85f, 0.85f, 0.85f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1.3f;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(() => onClick?.Invoke(def));

        // 그림 칸 (위 62%)
        float imageH = size.y * 0.62f;
        Vector2 imageCenter = new Vector2(0f, size.y * 0.5f - 14f - imageH * 0.5f);
        RectTransform box = TraitUIFactory.Centered(root, "ImageBox", imageCenter, new Vector2(size.x - 28f, imageH));
        TraitUIFactory.AddImage(box, null, ImageBox);
        icon = TraitUIFactory.IconImage(root, "Icon", imageCenter, Mathf.Min(size.x - 60f, imageH - 30f));
        icon.texture = TraitUIFactory.Icon(def.Id);

        // 빗금 + 자물쇠 (잠긴 기)
        RectTransform hatchRect = TraitUIFactory.Centered(root, "Hatch", Vector2.zero, size - new Vector2(16f, 16f));
        hatch = TraitUIFactory.AddImage(hatchRect, TraitAltarSkin.Hatch(), new Color(0.6f, 0.58f, 0.55f, 0.22f), type: Image.Type.Tiled);
        lockImage = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Lock", imageCenter, new Vector2(96f, 96f)), TraitAltarSkin.Lock(),
            new Color(0.85f, 0.82f, 0.76f, 0.9f));

        // 구분선 + 이름 / 해금 조건 (아래 38%)
        float lowerTop = imageCenter.y - imageH * 0.5f;
        float lowerCenter = (lowerTop + (-size.y * 0.5f)) * 0.5f;
        RectTransform line = TraitUIFactory.Centered(root, "Divider", new Vector2(0f, lowerTop - 6f), new Vector2(size.x - 50f, 2f));
        TraitUIFactory.AddImage(line, MenuSkin.LineFade, new Color(0.8f, 0.76f, 0.7f, 0.35f));
        nameLabel = TraitUIFactory.Label(root, "Name", def.Name, UIFontConfig.UIBold, 32f, TraitUIFactory.Text,
            new Vector2(0f, lowerCenter), new Vector2(size.x - 30f, 50f));
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 20f;
        nameLabel.fontSizeMax = 32f;
        conditionLabel = TraitUIFactory.Label(root, "Condition", string.Empty, UIFontConfig.UI, 21f, TraitUIFactory.TextDim,
            new Vector2(0f, lowerCenter), new Vector2(size.x - 36f, size.y * 0.3f), TextAlignmentOptions.Center, wrap: true);
        conditionLabel.enableAutoSizing = true;
        conditionLabel.fontSizeMin = 15f;
        conditionLabel.fontSizeMax = 22f;

        // 착용 도장
        RectTransform stampRect = TraitUIFactory.Centered(root, "Stamp", new Vector2(size.x * 0.5f - 36f, size.y * 0.5f - 36f), new Vector2(58f, 58f));
        stampRect.localRotation = Quaternion.Euler(0f, 0f, -12f);
        TraitUIFactory.AddImage(stampRect, MenuSkin.Diamond, TraitUIFactory.Accent);
        TraitUIFactory.Label(stampRect, "Text", "착", UIFontConfig.UIBold, 26f, new Color(1f, 0.93f, 0.88f), Vector2.zero, new Vector2(58f, 58f));
        stamp = stampRect.gameObject;
    }

    public void Refresh(bool selected)
    {
        bool unlocked = TraitUIFactory.IsUnlocked(Def);
        bool equipped = TraitLoadout.IsEquipped(Def.Id);
        paper.color = unlocked ? PaperColor : PaperLocked;
        icon.enabled = icon.texture != null && Def.Implemented; // '추후 공개'는 자물쇠만
        icon.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.35f);
        hatch.enabled = !unlocked;
        lockImage.enabled = !unlocked;
        nameLabel.enabled = unlocked;
        conditionLabel.enabled = !unlocked;
        if (!unlocked)
        {
            conditionLabel.text = Def.Implemented ? "<size=80%><color=#8C877F>해금 조건</color></size>\n" + TraitUIFactory.UnlockLine(Def) : "추후 공개";
        }

        selectFrame.enabled = selected;
        stamp.SetActive(equipped);
    }
}
