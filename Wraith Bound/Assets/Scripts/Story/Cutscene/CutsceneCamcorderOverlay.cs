using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 컷씬 화면을 "캠코더로 찍는 중"처럼 보이게 하는 전체 화면 뷰파인더 오버레이.
/// 실제 플레이 캠코더(CamcorderController.BuildViewfinderCanvas)와 똑같이 보이도록 같은 960×720 배치·스프라이트·
/// OSD(CamcorderViewfinderDetails: [HD]/AUTO/F2.4, 기본 TMP 폰트)를 씁니다.
/// 플레이 중 LCD 캔버스는 화면 가로를 꽉 채우고 위아래가 잘려 보이므로(측정값: 뷰포트 x 0.002~0.997, y −0.157~1.139)
/// 같은 영역에 960×720 캔버스를 맞춰 그립니다. 플레이 캠코더 UI를 바꾸면 여기도 같이 맞출 것.
/// </summary>
public class CutsceneCamcorderOverlay : CutsceneOverlayBase
{
    private const float RefWidth = 960f;
    private const float RefHeight = 720f;

    // 플레이 중 뷰파인더 캔버스가 화면에서 차지하는 영역 (뷰포트 비율, 1920×1080에서 측정)
    private static readonly Vector2 ViewportMin = new Vector2(0.002f, -0.157f);
    private static readonly Vector2 ViewportMax = new Vector2(0.997f, 1.139f);

    [Header("Sprites (View_Camcorder.prefab과 같은 것)")]
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite recordingDotSprite;
    [SerializeField] private Sprite batterySprite;

    [Header("OSD")]
    [SerializeField] private float minFov = 20f;
    [SerializeField] private float maxFov = 60f;

    [Header("Lower (캠코더 내리기)")]
    [Tooltip("0보다 크면 투명해질수록 화면이 그만큼(1920×1080 기준 px) 아래로 내려감 — 프롤로그에서 캠코더를 내리며 조작으로 넘어갈 때")]
    [SerializeField] private float lowerOffsetOnFade;

    private RectTransform area;
    private RectTransform lcd;
    private Image recDot;
    private Vector2 lastAreaSize;

    protected override void BuildContent(RectTransform root)
    {
        area = CreateRect("ViewfinderArea", root);
        area.anchorMin = ViewportMin;
        area.anchorMax = ViewportMax;
        area.offsetMin = Vector2.zero;
        area.offsetMax = Vector2.zero;

        // 플레이 캠코더와 같은 960×720 좌표계. 크기는 영역에 맞춰 스케일
        lcd = CreateRect("Viewfinder960x720", area);
        lcd.anchorMin = lcd.anchorMax = lcd.pivot = new Vector2(0.5f, 0.5f);
        lcd.anchoredPosition = Vector2.zero;
        lcd.sizeDelta = new Vector2(RefWidth, RefHeight);

        if (frameSprite != null)
        {
            Image frame = CreateStretched("Frame", lcd).gameObject.AddComponent<Image>();
            frame.sprite = frameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.raycastTarget = false;
        }

        if (recordingDotSprite != null)
        {
            recDot = CreateAnchored("RecDot", lcd, new Vector2(0f, 1f), new Vector2(60f, -110f), new Vector2(24f, 24f))
                .gameObject.AddComponent<Image>();
            recDot.sprite = recordingDotSprite;
            recDot.color = new Color(1f, 0.15f, 0.15f, 1f);
            recDot.raycastTarget = false;
        }

        if (batterySprite != null)
        {
            Image battery = CreateAnchored("Battery", lcd, new Vector2(1f, 1f), new Vector2(-70f, -110f), new Vector2(80f, 36f))
                .gameObject.AddComponent<Image>();
            battery.sprite = batterySprite;
            battery.preserveAspect = true;
            battery.raycastTarget = false;
        }

        // [HD]/AUTO/F2.4 + 줌 바(숨김) — 플레이 캠코더와 같은 클래스
        new CamcorderViewfinderDetails(lcd, null, minFov, maxFov);

        FitToArea();
    }

    private void Update()
    {
        FitToArea();

        if (area != null && lowerOffsetOnFade > 0f)
        {
            float lowered = Mathf.SmoothStep(0f, 1f, 1f - Alpha) * lowerOffsetOnFade;
            area.anchoredPosition = new Vector2(0f, -lowered);
        }

        if (recDot == null || Alpha <= 0f)
        {
            return;
        }

        // 플레이 캠코더와 같은 깜빡임 (PingPong 페이드)
        Color c = recDot.color;
        c.a = Mathf.PingPong(Time.unscaledTime * 1.5f, 1f);
        recDot.color = c;
    }

    private void FitToArea()
    {
        if (area == null || lcd == null)
        {
            return;
        }

        Vector2 size = area.rect.size;
        if (size == lastAreaSize || size.x <= 0f || size.y <= 0f)
        {
            return;
        }

        lastAreaSize = size;
        lcd.localScale = new Vector3(size.x / RefWidth, size.y / RefHeight, 1f);
    }
}
