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
}

public static class ChapterTitleTransition
{
    public static IEnumerator Run(Image fadeImage, ChapterTitleTransitionConfig config)
    {
        Image fade = ScreenFader.Prepare(fadeImage);
        ScreenFader.SetAlpha(fade, fade.color.a);

        yield return ScreenFader.FadeToBlack(fade, config.FadeDuration, config.FadeCurve);

        GameObject titleRoot = CreateTitleRoot(fade);
        TextMeshProUGUI titleLabel = CreateLabel(
            titleRoot.transform,
            "ChapterTitle",
            new Vector2(0f, 48f),
            config.TitleFontSize,
            Color.white
        );
        titleLabel.text = config.Title;

        PlayTitleSound2D(config.TitleAppearSound, config.TitleAppearVolume);

        yield return WaitForContinueInput(
            titleLabel,
            config.TitleFadeMinAlpha,
            config.TitleFadePeriod
        );

        yield return FadeOutTitle(titleLabel, config.TitleExitFadeDuration);

        if (titleRoot != null)
        {
            UnityEngine.Object.Destroy(titleRoot);
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

    private static IEnumerator FadeOutTitle(TextMeshProUGUI titleLabel, float duration)
    {
        if (titleLabel == null)
        {
            yield break;
        }

        float startAlpha = titleLabel.color.a;
        if (duration <= 0f)
        {
            SetLabelAlpha(titleLabel, 0f);
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            SetLabelAlpha(titleLabel, Mathf.Lerp(startAlpha, 0f, t));
            yield return null;
        }

        SetLabelAlpha(titleLabel, 0f);
    }

    private static void SetLabelAlpha(TextMeshProUGUI label, float alpha)
    {
        Color color = label.color;
        color.a = Mathf.Clamp01(alpha);
        label.color = color;
    }

    private static GameObject CreateTitleRoot(Image fade)
    {
        Canvas canvas = fade.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : fade.transform;

        GameObject root = new GameObject("ChapterTitleRoot");
        root.transform.SetParent(parent, false);

        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return root;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        float fontSize,
        Color color)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(1600f, fontSize * 2.2f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        TMP_FontAsset font = UIFontConfig.PolHumanRights;
        if (font != null)
        {
            label.font = font;
        }

        DisableOutline(label);
        return label;
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
