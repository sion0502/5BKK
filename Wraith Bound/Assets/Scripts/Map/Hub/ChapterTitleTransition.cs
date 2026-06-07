using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public struct ChapterTitleTransitionConfig
{
    public string Title;
    public string SceneName;
    public float FadeDuration;
    public AnimationCurve FadeCurve;
    public float TitleFontSize;
    public float TitleFadeMinAlpha;
    public float TitleFadePeriod;
    public float TitleExitFadeDuration;
    public AudioClip TitleAppearSound;
    public float TitleAppearVolume;
    public bool SkipInitialFade;
}

public static class ChapterTitleTransition
{
    public static IEnumerator Run(Image fadeImage, ChapterTitleTransitionConfig config)
    {
        Image fade = ScreenFader.Prepare(fadeImage);
        GameObject fadeCanvasRoot = fade != null ? fade.GetComponentInParent<Canvas>()?.gameObject : null;
        if (fadeCanvasRoot != null)
        {
            fadeCanvasRoot.SetActive(false);
        }

        GameObject titleScreen = CreateTitleScreen(
            out TextMeshProUGUI titleLabel,
            out CanvasGroup titleCanvasGroup,
            config.TitleFontSize
        );
        titleLabel.text = config.Title;
        SetLabelAlpha(titleLabel, 1f);

        PlayTitleSound2D(config.TitleAppearSound, config.TitleAppearVolume);

        yield return WaitForContinueInput(
            titleLabel,
            config.TitleFadeMinAlpha,
            config.TitleFadePeriod
        );

        yield return FadeOutTitleScreen(titleCanvasGroup, titleLabel, config.TitleExitFadeDuration);

        if (titleScreen != null)
        {
            UnityEngine.Object.Destroy(titleScreen);
        }

        if (fadeCanvasRoot != null)
        {
            fadeCanvasRoot.SetActive(true);
        }

        ScreenFader.SetAlpha(fade, 1f);
        ScreenFader.PersistBlack(fade);
        SceneManager.LoadScene(config.SceneName);
    }

    private static void PlayTitleSound2D(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("ChapterTitleAudio_Runtime");
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = false;
        source.priority = 0;

        Camera listenerCamera = Camera.main;
        if (listenerCamera != null)
        {
            audioObject.transform.position = listenerCamera.transform.position;
        }

        source.Play();
        UnityEngine.Object.Destroy(audioObject, clip.length + 0.25f);
    }

    private static IEnumerator WaitForContinueInput(
        TextMeshProUGUI titleLabel,
        float titleFadeMinAlpha,
        float titleFadePeriod)
    {
        yield return null;
        yield return null;

        float titlePhase = 0f;

        while (!Input.anyKeyDown)
        {
            titlePhase += Time.unscaledDeltaTime;

            if (titleLabel != null)
            {
                float titleWave = (Mathf.Sin(titlePhase * Mathf.PI * 2f / titleFadePeriod) + 1f) * 0.5f;
                float titleAlpha = Mathf.Lerp(titleFadeMinAlpha, 1f, titleWave);
                SetLabelAlpha(titleLabel, titleAlpha);
            }

            yield return null;
        }
    }

    private static IEnumerator FadeOutTitleScreen(
        CanvasGroup canvasGroup,
        TextMeshProUGUI titleLabel,
        float duration)
    {
        if (duration <= 0f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (titleLabel != null)
            {
                SetLabelAlpha(titleLabel, 0f);
            }

            yield break;
        }

        float startGroupAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float startLabelAlpha = titleLabel != null ? titleLabel.color.a : 1f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startGroupAlpha, 0f, t);
            }

            if (titleLabel != null)
            {
                SetLabelAlpha(titleLabel, Mathf.Lerp(startLabelAlpha, 0f, t));
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (titleLabel != null)
        {
            SetLabelAlpha(titleLabel, 0f);
        }
    }

    private static void SetLabelAlpha(TextMeshProUGUI label, float alpha)
    {
        if (label == null)
        {
            return;
        }

        alpha = Mathf.Clamp01(alpha);
        Color color = label.color;
        color.r = 1f;
        color.g = 1f;
        color.b = 1f;
        color.a = alpha;
        label.color = color;

        Material material = label.fontMaterial;
        if (material != null && material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            Color faceColor = material.GetColor(ShaderUtilities.ID_FaceColor);
            faceColor.r = 1f;
            faceColor.g = 1f;
            faceColor.b = 1f;
            faceColor.a = alpha;
            material.SetColor(ShaderUtilities.ID_FaceColor, faceColor);
        }
    }

    private static GameObject CreateTitleScreen(
        out TextMeshProUGUI titleLabel,
        out CanvasGroup canvasGroup,
        float fontSize)
    {
        GameObject canvasObject = new GameObject("ChapterTitleScreen");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32768;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        Image background = backgroundObject.AddComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;
        StretchFullScreen(background.rectTransform);

        GameObject labelObject = new GameObject("ChapterTitle");
        labelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 48f);
        labelRect.sizeDelta = new Vector2(1600f, fontSize * 2.2f);

        titleLabel = labelObject.AddComponent<TextMeshProUGUI>();
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.fontSize = fontSize;
        titleLabel.fontStyle = FontStyles.Normal;
        titleLabel.color = Color.white;
        titleLabel.raycastTarget = false;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = UIFontConfig.PolHumanRights;
        if (font != null)
        {
            titleLabel.font = font;
        }

        DisableOutline(titleLabel);

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        return canvasObject;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void DisableOutline(TextMeshProUGUI label)
    {
        label.outlineWidth = 0f;
        label.outlineColor = new Color32(0, 0, 0, 0);

        Material material = label.fontMaterial;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_OutlineSoftness))
        {
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        }

        material.DisableKeyword("OUTLINE_ON");
    }
}
