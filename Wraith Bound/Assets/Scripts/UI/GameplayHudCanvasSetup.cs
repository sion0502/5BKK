using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerGameplayHud 프리팹·씬 Canvas 공통 설정.
/// </summary>
public static class GameplayHudCanvasSetup
{
    /// <summary>
    /// 자기 GameObject에 Canvas가 없는 HUD가 붙을 Canvas를 찾습니다.
    /// 이미 어떤 Canvas 밑에 있으면 그 Canvas를 그대로 씀 — 예전엔 FindFirstObjectByType&lt;Canvas&gt;()로 아무 Canvas나 골라
    /// 컷씬 검은 화면(CutsceneBlackScreen) 밑으로 옮겨 가는 바람에, 검은 화면이 걷히면(알파 0) HUD도 같이 투명해졌음.
    /// 부모 Canvas가 없을 때도 컷씬 오버레이·건너뛰기 안내 같은 연출용 Canvas는 고르지 않음.
    /// </summary>
    public static Canvas FindHostCanvas(Transform hud)
    {
        if (hud == null)
        {
            return null;
        }

        Transform parent = hud.parent;
        Canvas parentCanvas = parent != null ? parent.GetComponentInParent<Canvas>(true) : null;
        if (parentCanvas != null)
        {
            return parentCanvas;
        }

        Canvas fallback = null;
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || IsCutsceneCanvas(canvas))
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvas;
            }

            if (fallback == null)
            {
                fallback = canvas;
            }
        }

        return fallback;
    }

    private static bool IsCutsceneCanvas(Canvas canvas)
    {
        return canvas.GetComponent<CutsceneOverlayBase>() != null || canvas.GetComponent<CutsceneSkipHint>() != null;
    }

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
