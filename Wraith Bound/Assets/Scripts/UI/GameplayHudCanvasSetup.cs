using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerGameplayHud 프리팹·씬 Canvas 공통 설정.
/// </summary>
public static class GameplayHudCanvasSetup
{
    public static void EnsureOverlayCanvas(GameObject host)
    {
        if (host == null)
        {
            return;
        }

        Canvas canvas = host.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = host.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = host.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (host.GetComponent<GraphicRaycaster>() == null)
        {
            host.AddComponent<GraphicRaycaster>();
        }

        RectTransform rect = host.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
