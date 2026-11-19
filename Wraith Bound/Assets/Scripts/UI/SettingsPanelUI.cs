using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 패널(배경음 · 환경음 · 마우스 감도 · 전체 화면). 메인 메뉴와 일시정지 메뉴가 같이 씀.
/// 값은 GameSettings에 바로 반영되고, 저장(PlayerPrefs.Save)은 호출한 쪽이 패널을 닫을 때 한다.
/// </summary>
public sealed class SettingsPanelUI
{
    private TextMeshProUGUI bgmValueLabel;
    private TextMeshProUGUI envValueLabel;
    private TextMeshProUGUI sensitivityValueLabel;
    private TextMeshProUGUI fullscreenLabel;

    /// <param name="parent">패널을 올릴 부모(보통 CanvasGroup이 붙은 전체 화면 루트)</param>
    /// <param name="onBack">'뒤로' 버튼</param>
    public static SettingsPanelUI Build(RectTransform parent, TMP_FontAsset font, Vector2 backButtonSize, Action onBack)
    {
        SettingsPanelUI ui = new SettingsPanelUI();
        ui.BuildInternal(parent, font, backButtonSize, onBack);
        return ui;
    }

    private void BuildInternal(RectTransform parent, TMP_FontAsset font, Vector2 backButtonSize, Action onBack)
    {
        RectTransform panel = MenuUIUtility.CreateCenteredPanel(parent, "Background", new Vector2(0f, -60f),
            new Vector2(760f, 580f), new Color(0.05f, 0.05f, 0.06f, 0.88f));

        Color text = MenuSkin.Text;
        MenuUIUtility.CreateHeader(panel, "설정", font, new Vector2(0f, 225f), 560f);

        float labelX = -220f;
        float sliderX = 60f;
        float valueX = 290f;
        Vector2 labelSize = new Vector2(220f, 50f);
        Vector2 sliderSize = new Vector2(320f, 36f);
        Vector2 valueSize = new Vector2(110f, 50f);

        // 배경음
        float y = 130f;
        MenuUIUtility.CreateLabel(panel, "배경음", font, new Vector2(labelX, y), labelSize, 28f, TextAlignmentOptions.Left, text);
        MenuUIUtility.CreateSlider(panel, new Vector2(sliderX, y), sliderSize, 0f, 1f, GameSettings.BgmVolume, v =>
        {
            GameSettings.BgmVolume = v;
            Refresh();
        });
        bgmValueLabel = MenuUIUtility.CreateLabel(panel, "", font, new Vector2(valueX, y), valueSize, 26f, TextAlignmentOptions.Right, text);

        // 환경음
        y = 50f;
        MenuUIUtility.CreateLabel(panel, "환경음", font, new Vector2(labelX, y), labelSize, 28f, TextAlignmentOptions.Left, text);
        MenuUIUtility.CreateSlider(panel, new Vector2(sliderX, y), sliderSize, 0f, 1f, GameSettings.EnvVolume, v =>
        {
            GameSettings.EnvVolume = v;
            Refresh();
        });
        envValueLabel = MenuUIUtility.CreateLabel(panel, "", font, new Vector2(valueX, y), valueSize, 26f, TextAlignmentOptions.Right, text);

        // 마우스 감도
        y = -30f;
        MenuUIUtility.CreateLabel(panel, "마우스 감도", font, new Vector2(labelX, y), labelSize, 28f, TextAlignmentOptions.Left, text);
        MenuUIUtility.CreateSlider(panel, new Vector2(sliderX, y), sliderSize, GameSettings.MinSensitivity,
            GameSettings.MaxSensitivity, GameSettings.MouseSensitivity, v =>
            {
                GameSettings.MouseSensitivity = v;
                Refresh();
            });
        sensitivityValueLabel = MenuUIUtility.CreateLabel(panel, "", font, new Vector2(valueX, y), valueSize, 26f, TextAlignmentOptions.Right, text);

        // 전체 화면
        y = -110f;
        MenuUIUtility.CreateLabel(panel, "전체 화면", font, new Vector2(labelX, y), labelSize, 28f, TextAlignmentOptions.Left, text);
        Button fullscreenButton = MenuUIUtility.CreateMenuButton(panel, "", font, new Vector2(sliderX, y),
            new Vector2(180f, 48f), () =>
            {
                GameSettings.Fullscreen = !GameSettings.Fullscreen;
                Refresh(!Screen.fullScreen); // Screen.fullScreen은 다음 프레임에 바뀜
            });
        fullscreenLabel = fullscreenButton.GetComponentInChildren<TextMeshProUGUI>(true);

        Button back = MenuUIUtility.CreateMenuButton(panel, "뒤로", font, new Vector2(0f, -215f), backButtonSize, () => onBack?.Invoke());
        if (back != null && back.GetComponent<MenuHoverSound>() == null)
        {
            back.gameObject.AddComponent<MenuHoverSound>();
        }

        Refresh();
    }

    public void Refresh()
    {
        Refresh(Screen.fullScreen);
    }

    private void Refresh(bool fullscreen)
    {
        if (bgmValueLabel != null)
        {
            bgmValueLabel.text = Mathf.RoundToInt(GameSettings.BgmVolume * 100f) + "%";
        }

        if (envValueLabel != null)
        {
            envValueLabel.text = Mathf.RoundToInt(GameSettings.EnvVolume * 100f) + "%";
        }

        if (sensitivityValueLabel != null)
        {
            sensitivityValueLabel.text = GameSettings.MouseSensitivity.ToString("0.0") + "x";
        }

        if (fullscreenLabel != null)
        {
            fullscreenLabel.text = fullscreen ? "켜짐" : "꺼짐";
        }
    }
}
