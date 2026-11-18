using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 고르기 화면 왼쪽 상세 칸 — 고른 기의 큰 그림·이름·효과 + [장착]/[해제] 버튼.
/// 잠긴 기는 자물쇠와 해금 조건, 버튼은 흐리게(눌러도 아무 일 없음).
/// </summary>
public class TraitDetailPanel
{
    private readonly RawImage icon;
    private readonly Image lockImage;
    private readonly TextMeshProUGUI nameLabel;
    private readonly TextMeshProUGUI effectLabel;
    private readonly TextMeshProUGUI noteLabel;
    private readonly TextMeshProUGUI buttonLabel;
    private readonly MenuButtonStyle buttonStyle;
    private TraitDef current;

    /// <param name="onEquip">버튼 클릭 (현재 고른 기)</param>
    public TraitDetailPanel(Transform parent, Vector2 center, Vector2 size, Action<TraitDef> onEquip)
    {
        RectTransform root = TraitUIFactory.Centered(parent, "Detail", center, size);
        float top = size.y * 0.5f;

        // 그림 (종이 액자)
        float imageSize = 200f;
        Vector2 imageCenter = new Vector2(0f, top - 30f - imageSize * 0.5f);
        TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "ImageFrame", imageCenter, new Vector2(imageSize + 30f, imageSize + 30f)),
            TraitAltarSkin.WavyPaper(7), new Color(0.2f, 0.19f, 0.18f));
        TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "ImageBox", imageCenter, new Vector2(imageSize, imageSize)), null,
            new Color(0.07f, 0.065f, 0.06f, 0.95f));
        icon = TraitUIFactory.IconImage(root, "Icon", imageCenter, imageSize - 24f);
        lockImage = TraitUIFactory.AddImage(TraitUIFactory.Centered(root, "Lock", imageCenter, new Vector2(110f, 110f)), TraitAltarSkin.Lock(),
            new Color(0.85f, 0.82f, 0.76f, 0.9f));

        float y = imageCenter.y - imageSize * 0.5f - 40f;
        nameLabel = TraitUIFactory.Label(root, "Name", string.Empty, UIFontConfig.UIBold, 40f, TraitUIFactory.Text, new Vector2(0f, y), new Vector2(size.x - 30f, 54f));
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 26f;
        nameLabel.fontSizeMax = 40f;

        effectLabel = TraitUIFactory.Label(root, "Effect", string.Empty, UIFontConfig.UI, 22f, new Color(0.8f, 0.78f, 0.74f),
            new Vector2(0f, y - 84f), new Vector2(size.x - 40f, 110f), TextAlignmentOptions.Top, wrap: true);
        effectLabel.enableAutoSizing = true;
        effectLabel.fontSizeMin = 15f;
        effectLabel.fontSizeMax = 22f;
        effectLabel.lineSpacing = 8f;

        noteLabel = TraitUIFactory.Label(root, "Note", string.Empty, UIFontConfig.UI, 19f, TraitUIFactory.Accent,
            new Vector2(0f, y - 166f), new Vector2(size.x - 40f, 34f), TextAlignmentOptions.Center, wrap: true);
        noteLabel.enableAutoSizing = true;
        noteLabel.fontSizeMin = 14f;
        noteLabel.fontSizeMax = 19f;

        Button button = MenuUIUtility.CreateMenuButton(root, "장착", UIFontConfig.UI, new Vector2(0f, -top + 44f), new Vector2(240f, 56f),
            () => onEquip?.Invoke(current));
        buttonStyle = button.GetComponent<MenuButtonStyle>();
        buttonLabel = buttonStyle != null ? buttonStyle.label : button.GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Show(TraitDef def)
    {
        current = def;
        if (def == null)
        {
            return;
        }

        bool unlocked = TraitUIFactory.IsUnlocked(def);
        bool equipped = TraitLoadout.IsEquipped(def.Id);
        icon.texture = TraitUIFactory.Icon(def.Id);
        icon.enabled = icon.texture != null;
        icon.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.35f);
        lockImage.enabled = !unlocked;
        nameLabel.text = def.Name;
        effectLabel.text = def.Effect;

        if (!unlocked)
        {
            noteLabel.text = def.Implemented ? "해금 조건 · " + TraitUIFactory.UnlockLine(def) : "추후 공개";
            noteLabel.color = TraitUIFactory.TextDim;
            buttonLabel.text = "잠김";
        }
        else if (equipped)
        {
            noteLabel.text = "착용 중";
            noteLabel.color = TraitUIFactory.Accent;
            buttonLabel.text = "해제";
        }
        else
        {
            bool full = TraitLoadout.Equipped.Count >= TraitLoadout.MaxSlots;
            noteLabel.text = def.SingleSlot ? "착용하면 다른 기를 모두 뺍니다" : full ? "칸이 가득 차 먼저 낀 기와 바뀝니다" : string.Empty;
            noteLabel.color = TraitUIFactory.TextDim;
            buttonLabel.text = "장착";
        }

        if (buttonStyle != null)
        {
            buttonStyle.dim = unlocked ? 1f : 0.35f;
        }
    }
}
