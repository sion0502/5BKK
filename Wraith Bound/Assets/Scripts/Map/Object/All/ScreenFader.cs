using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class ScreenFader
{
    private static Sprite _solidSprite;
    private static Image _persistedOverlay;
    private static GameObject _persistedRoot;

    public static bool ShouldWakeUpInHub { get; set; }

    public static Image Prepare(Image preferred = null)
    {
        if (preferred != null && Configure(preferred))
        {
            return preferred;
        }

        GameObject fadeRoot = GameObject.Find("FadeImage");
        if (fadeRoot != null)
        {
            Image sceneImage = fadeRoot.GetComponentInChildren<Image>(true);
            if (sceneImage != null && Configure(sceneImage))
            {
                return sceneImage;
            }
        }

        return CreateRuntimeOverlay(false);
    }

    public static Image GetPersistedOverlay()
    {
        return _persistedOverlay;
    }

    public static void PersistBlack(Image preferred = null)
    {
        Image overlay = Prepare(preferred);
        SetAlpha(overlay, 1f);

        _persistedOverlay = overlay;
        _persistedRoot = overlay.GetComponentInParent<Canvas>()?.gameObject;
        if (_persistedRoot != null)
        {
            Object.DontDestroyOnLoad(_persistedRoot);
        }
    }

    public static void ClearPersisted()
    {
        if (_persistedRoot != null)
        {
            Object.Destroy(_persistedRoot);
        }

        _persistedRoot = null;
        _persistedOverlay = null;
    }

    public static void SetAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Configure(image);
        image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    public static IEnumerator FadeToBlack(Image image, float duration, AnimationCurve curve)
    {
        if (image == null)
        {
            yield break;
        }

        Configure(image);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            float eased = curve != null ? curve.Evaluate(t) : t;
            SetAlpha(image, eased);
            yield return null;
        }

        SetAlpha(image, 1f);
    }

    public static IEnumerator FadeFromBlack(Image image, float duration, AnimationCurve curve)
    {
        if (image == null)
        {
            yield break;
        }

        Configure(image);
        SetAlpha(image, 1f);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            float eased = curve != null ? curve.Evaluate(t) : t;
            SetAlpha(image, 1f - eased);
            yield return null;
        }

        SetAlpha(image, 0f);
    }

    private static Image CreateRuntimeOverlay(bool persist)
    {
        GameObject canvasObject = new GameObject("ScreenFadeOverlay");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        StretchFullScreen(canvasRect);

        GameObject imageObject = new GameObject("Fade");
        imageObject.transform.SetParent(canvasObject.transform, false);

        Image image = imageObject.AddComponent<Image>();
        Configure(image);

        if (persist)
        {
            _persistedOverlay = image;
            _persistedRoot = canvasObject;
            Object.DontDestroyOnLoad(canvasObject);
        }

        return image;
    }

    private static bool Configure(Image image)
    {
        if (image == null)
        {
            return false;
        }

        image.sprite = GetSolidSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = new Color(0f, 0f, 0f, image.color.a);

        RectTransform imageRect = image.rectTransform;
        imageRect.localScale = Vector3.one;
        StretchFullScreen(imageRect);

        Canvas canvas = image.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                StretchFullScreen(canvasRect);
            }
        }

        image.gameObject.SetActive(true);
        return true;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = new Vector2(-200f, -200f);
        rect.offsetMax = new Vector2(200f, 200f);
    }

    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null)
        {
            return _solidSprite;
        }

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        _solidSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return _solidSprite;
    }
}
