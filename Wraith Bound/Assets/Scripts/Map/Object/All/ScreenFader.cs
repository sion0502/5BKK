using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class ScreenFader
{
    private static Sprite _whiteSprite;

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

        return CreateRuntimeOverlay();
    }

    public static IEnumerator FadeToBlack(Image image, float duration, AnimationCurve curve, System.Action<float> onProgress = null)
    {
        if (image == null)
        {
            yield break;
        }

        Configure(image);

        Color color = new Color(0f, 0f, 0f, 0f);
        image.color = color;
        image.gameObject.SetActive(true);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            float eased = curve != null ? curve.Evaluate(t) : t;

            color.a = eased;
            image.color = color;
            onProgress?.Invoke(eased);

            yield return null;
        }

        image.color = new Color(0f, 0f, 0f, 1f);
    }

    private static Image CreateRuntimeOverlay()
    {
        GameObject canvasObject = new GameObject("RuntimeFadeOverlay");

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
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject imageObject = new GameObject("Fade");
        imageObject.transform.SetParent(canvasObject.transform, false);

        Image image = imageObject.AddComponent<Image>();
        Configure(image);
        return image;
    }

    private static bool Configure(Image image)
    {
        if (image == null)
        {
            return false;
        }

        if (image.sprite == null)
        {
            image.sprite = GetWhiteSprite();
        }

        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = new Color(0f, 0f, 0f, image.color.a);

        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        imageRect.localScale = Vector3.one;

        Canvas canvas = image.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }
        }

        image.gameObject.SetActive(true);
        return true;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        _whiteSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (_whiteSprite == null)
        {
            _whiteSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        }

        return _whiteSprite;
    }
}
