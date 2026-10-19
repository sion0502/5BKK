using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 캠코더 뷰파인더의 실제 캠코더 같은 표시 요소 (CamcorderController가 소유).
///  • 왼쪽 하단: [HD] 배지 / AUTO / F2.4 (세 줄)
///  • 하단 중앙 줌 바: W ━━━ T + 배율. 휠 줌 시 나타났다가 잠시 뒤 사라짐
/// 뷰파인더 캔버스(960×720 기준) 아래에 생성. LCD 쿼드보다 항상 앞에 그려지도록 ZTest Always.
/// </summary>
public class CamcorderViewfinderDetails
{
    // 하단 요소 높이 (캔버스 아래에서). LCD 화면은 위아래가 잘려 보이므로 위쪽 REC 점(위에서 110)과 비슷한 여백이 되게
    private const float BottomY = 155f;
    private const float ZoomTrackWidth = 210f;
    private const float ZoomVisibleTime = 1.2f;   // 마지막 줌 입력 후 유지 시간
    private const float ZoomFadeTime = 0.3f;
    private const int ZoomTicks = 10;

    private static readonly Color OsdWhite = new Color(0.95f, 0.95f, 0.92f, 0.95f);
    private static readonly Color OsdDim = new Color(0.95f, 0.95f, 0.92f, 0.35f);

    private readonly float minFov;
    private readonly float maxFov;
    private readonly Material imageMaterial;
    private readonly int layer;

    private CanvasGroup zoomGroup;
    private RectTransform zoomFill;
    private TextMeshProUGUI zoomRatio;
    private float zoomShownUntil = -1f;
    private float displayedZoom;

    /// <param name="canvas">뷰파인더 캔버스 Transform</param>
    /// <param name="hudMaterial">Image용 ZTest Always 머티리얼 (null이면 기본)</param>
    /// <param name="minFov">최대 줌(망원) FOV</param>
    /// <param name="maxFov">최소 줌(광각) FOV</param>
    public CamcorderViewfinderDetails(Transform canvas, Material hudMaterial, float minFov, float maxFov)
    {
        this.minFov = minFov;
        this.maxFov = maxFov;
        imageMaterial = hudMaterial;
        layer = canvas.gameObject.layer;

        BuildInfo(canvas);
        BuildZoomBar(canvas);
    }

    /// <summary>휠로 줌 목표가 바뀌었을 때 호출 → 줌 바 표시</summary>
    public void OnZoomChanged()
    {
        zoomShownUntil = Time.time + ZoomVisibleTime;
    }

    /// <summary>매 프레임 호출. fov = 현재 줌 목표 FOV</summary>
    public void Tick(float deltaTime, float fov)
    {
        float target = Mathf.InverseLerp(maxFov, minFov, fov);
        displayedZoom = Mathf.Lerp(displayedZoom, target, deltaTime * 12f);
        zoomFill.sizeDelta = new Vector2(ZoomTrackWidth * displayedZoom, zoomFill.sizeDelta.y);

        float ratio = Mathf.Tan(maxFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        zoomRatio.text = $"x{ratio:0.0}";

        float remaining = zoomShownUntil - Time.time;
        zoomGroup.alpha = remaining > 0f ? 1f : Mathf.Clamp01(1f + remaining / ZoomFadeTime);
    }

    /// <summary>뷰파인더를 닫을 때 줌 바 즉시 숨김</summary>
    public void HideZoomBar()
    {
        zoomShownUntil = -1f;
        if (zoomGroup != null)
            zoomGroup.alpha = 0f;
    }

    // ───────────────────────────── 왼쪽 하단 정보 ─────────────────────────────

    private void BuildInfo(Transform canvas)
    {
        // 세 줄로 쌓음 (위에서부터 HD / AUTO / F2.4). 맨 아랫줄이 BottomY에 오도록
        const float lineHeight = 32f;
        RectTransform info = CreateRect("OsdInfo", canvas, new Vector2(0f, 0f), new Vector2(60f, BottomY - lineHeight * 0.5f), new Vector2(120f, lineHeight * 3f), new Vector2(0f, 0f));

        // [HD] 테두리 배지
        RectTransform badge = CreateRect("HdBadge", info, new Vector2(0f, 0f), new Vector2(0f, lineHeight * 2f + 3f), new Vector2(46f, 26f), new Vector2(0f, 0f));
        const float border = 2f;
        CreateImage("Top", badge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, border));
        CreateImage("Bottom", badge, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, border));
        CreateImage("Left", badge, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(border, 0f));
        CreateImage("Right", badge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(border, 0f));
        TextMeshProUGUI hd = CreateText("HD", badge, "HD", 18f, TextAlignmentOptions.Center);
        Stretch(hd.rectTransform);
        hd.fontStyle = FontStyles.Bold;

        TextMeshProUGUI auto = CreateText("Auto", info, "AUTO", 20f, TextAlignmentOptions.Left);
        Place(auto.rectTransform, new Vector2(0f, 0f), new Vector2(0f, lineHeight), new Vector2(100f, lineHeight), new Vector2(0f, 0f));

        TextMeshProUGUI aperture = CreateText("Aperture", info, "F2.4", 20f, TextAlignmentOptions.Left);
        Place(aperture.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(100f, lineHeight), new Vector2(0f, 0f));
    }

    // ───────────────────────────── 하단 중앙 줌 바 ─────────────────────────────

    private void BuildZoomBar(Transform canvas)
    {
        RectTransform root = CreateRect("ZoomBar", canvas, new Vector2(0.5f, 0f), new Vector2(0f, BottomY), new Vector2(ZoomTrackWidth + 90f, 60f), new Vector2(0.5f, 0.5f));
        zoomGroup = root.gameObject.AddComponent<CanvasGroup>();
        zoomGroup.alpha = 0f;
        zoomGroup.blocksRaycasts = false;

        TextMeshProUGUI w = CreateText("W", root, "W", 20f, TextAlignmentOptions.Center);
        Place(w.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-ZoomTrackWidth * 0.5f - 22f, -6f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));
        TextMeshProUGUI t = CreateText("T", root, "T", 20f, TextAlignmentOptions.Center);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ZoomTrackWidth * 0.5f + 22f, -6f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));

        RectTransform track = CreateRect("Track", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(ZoomTrackWidth, 9f), new Vector2(0.5f, 0.5f));
        Image trackBg = AddImage(track.gameObject, OsdDim);
        trackBg.raycastTarget = false;

        // 눈금
        for (int i = 1; i < ZoomTicks; i++)
        {
            RectTransform tick = CreateRect("Tick", track, new Vector2(i / (float)ZoomTicks, 0.5f), Vector2.zero, new Vector2(2f, 15f), new Vector2(0.5f, 0.5f));
            AddImage(tick.gameObject, OsdDim);
        }

        zoomFill = CreateRect("Fill", track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 9f), new Vector2(0f, 0.5f));
        AddImage(zoomFill.gameObject, OsdWhite);

        zoomRatio = CreateText("Ratio", root, "x1.0", 18f, TextAlignmentOptions.Center);
        Place(zoomRatio.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(90f, 26f), new Vector2(0.5f, 0.5f));
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Place(rect, anchor, position, size, pivot);
        return rect;
    }

    private void CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        AddImage(go, OsdWhite);
    }

    private Image AddImage(GameObject go, Color color)
    {
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (imageMaterial != null)
            image.material = imageMaterial;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = layer };
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = OsdWhite;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        // LCD 쿼드보다 항상 앞에 (TMP 셰이더의 ZTest 프로퍼티)
        tmp.fontMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        return tmp;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
