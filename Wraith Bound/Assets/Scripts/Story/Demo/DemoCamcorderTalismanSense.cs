using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캠코더로 부적 찾기 (2026-11-19 사용자 결정 — 부적 찾기 난이도 완화, 연출 루트 순서대로).
/// 뷰파인더를 보는 동안 연출 루트상 다음 부적(다 모았으면 출구)이 벽 너머로도 금빛 기운으로 보임.
/// 화면 밖이면 화면 가장자리의 화살표가 그 방향을 가리킴. 목표는 DemoGuideRoute.NextCamcorderTarget.
/// 렌즈 카메라 화면 ≈ 게임 화면(뷰파인더 LCD가 화면을 거의 채움, CutsceneCamcorderOverlay 실측 y −0.157~1.139)으로 투영.
/// FixedDemo_Story에 붙음(빌더) — 없으면 아무 일 없음. unscaled 시간.
/// </summary>
public class DemoCamcorderTalismanSense : MonoBehaviour
{
    [SerializeField] private float maxDistance = 400f;
    [SerializeField] private Color glowColor = new Color(1f, 0.78f, 0.32f, 1f);
    [Tooltip("챕터 시작 후 이 시간 뒤 '캠코더로 비추면 부적의 기운이 보인다' 안내 (0 이하면 안 함)")]
    [SerializeField] private float startHintDelay = 25f;

    // 뷰파인더 LCD가 화면에서 차지하는 세로 범위 (렌즈 뷰포트 y 0~1 → 화면 y)
    private const float LcdBottom = -0.157f;
    private const float LcdTop = 1.139f;

    private Canvas canvas;
    private RectTransform glow;
    private Image glowImage;
    private RectTransform arrow;
    private Image arrowImage;
    private CamcorderController camcorder;
    private Camera lensCamera;
    private bool hintShown;
    private float startTime;

    private static readonly System.Reflection.FieldInfo SequenceField =
        typeof(CamcorderController).GetField("sequenceRoutine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private static readonly System.Reflection.FieldInfo LensField =
        typeof(CamcorderController).GetField("lensCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private void Start()
    {
        startTime = Time.time;
        BuildUI();
    }

    private void BuildUI()
    {
        var go = new GameObject("CamcorderTalismanSense", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450; // 캠코더 화면(카메라 렌더) 위, 일반 HUD·안내(500~) 아래

        glow = NewImage("Glow", go.transform, MakeGlowTexture(), out glowImage);
        arrow = NewImage("Arrow", go.transform, MakeArrowTexture(), out arrowImage);
        glow.gameObject.SetActive(false);
        arrow.gameObject.SetActive(false);
    }

    private static RectTransform NewImage(string name, Transform parent, Texture2D tex, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        img = go.AddComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        img.raycastTarget = false;
        return r;
    }

    private void Update()
    {
        if (!hintShown && startHintDelay > 0f && Time.time - startTime > startHintDelay && DemoSealProgress.Count == 0)
        {
            hintShown = true;
            ScreenMessageUI.Show("캠코더 화면으로 비추면 부적의 기운이 보인다", 3.5f);
        }
    }

    private void LateUpdate()
    {
        if (canvas == null)
        {
            return;
        }

        if (camcorder == null)
        {
            camcorder = FindFirstObjectByType<CamcorderController>();
        }

        if (camcorder != null && lensCamera == null && LensField != null)
        {
            lensCamera = LensField.GetValue(camcorder) as Camera;
        }

        Transform target = DemoGuideRoute.NextCamcorderTarget();
        // 뷰파인더를 완전히 올린 뒤부터 (들어 올리는 동작 중엔 안 보임 — 2026-11-19 사용자 요청)
        bool raised = camcorder != null && (SequenceField == null || SequenceField.GetValue(camcorder) == null);
        bool viewing = camcorder != null && camcorder.IsViewfinderActive && raised && lensCamera != null && target != null
            && !PlayerDeathDebug.IsDying && !PlayerDeathDebug.IsDead;
        if (!viewing || Vector3.Distance(lensCamera.transform.position, target.position) > maxDistance)
        {
            glow.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            return;
        }

        Vector3 world = target.position + Vector3.up * 0.3f;
        Vector3 vp = lensCamera.WorldToViewportPoint(world);
        float dist = Vector3.Distance(lensCamera.transform.position, world);
        float w = Screen.width;
        float h = Screen.height;
        float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.3f);

        bool onScreen = vp.z > 0f && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;
        if (onScreen)
        {
            arrow.gameObject.SetActive(false);
            glow.gameObject.SetActive(true);
            float sy = Mathf.Lerp(LcdBottom, LcdTop, vp.y);
            glow.anchoredPosition = new Vector2(vp.x * w, sy * h);
            float size = Mathf.Clamp(1800f / Mathf.Max(dist, 1f), 110f, 280f) * (h / 1080f); // 멀어도 눈에 띄게 (145m에서 너무 작았음)
            float breathe = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.4f);
            glow.sizeDelta = new Vector2(size, size * 1.35f) * breathe;
            Color c = glowColor;
            c.a = Mathf.Clamp01(0.55f + 0.45f * Mathf.InverseLerp(120f, 10f, dist)) * flicker;
            glowImage.color = c;
            return;
        }

        // 화면 밖: 가장자리 화살표 (카메라 뒤면 방향을 뒤집음)
        glow.gameObject.SetActive(false);
        arrow.gameObject.SetActive(true);
        Vector3 local = lensCamera.transform.InverseTransformPoint(world);
        Vector2 dir = new Vector2(local.x, local.y);
        if (local.z < 0f && dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.down;
        }

        dir.Normalize();
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float margin = 90f * (h / 1080f);
        float sx = (w * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.x), 0.0001f);
        float syy = (h * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.y), 0.0001f);
        arrow.anchoredPosition = center + dir * Mathf.Min(sx, syy);
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        float a = 72f * (h / 1080f);
        arrow.sizeDelta = new Vector2(a, a);
        Color ac = glowColor;
        ac.a = 0.85f * flicker;
        arrowImage.color = ac;
    }

    private static Texture2D MakeGlowTexture()
    {
        const int W = 64, H = 86;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_TalismanSenseGlow" };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float dx = (x + 0.5f) / W * 2f - 1f;
                float dy = (y + 0.5f) / H * 2f - 1f;
                float halo = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)); // 둥근 기운
                halo *= halo;
                bool paper = Mathf.Abs(dx) < 0.22f && Mathf.Abs(dy) < 0.42f; // 가운데 부적 모양
                float a = Mathf.Max(halo * 0.75f, paper ? 0.95f : 0f);
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeArrowTexture()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_TalismanSenseArrow" };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                // 위를 가리키는 삼각형 (회전으로 방향)
                float u = (x + 0.5f) / S;
                float v = (y + 0.5f) / S;
                float half = (1f - v) * 0.5f;
                bool inside = v > 0.12f && v < 0.92f && Mathf.Abs(u - 0.5f) < half * 0.9f;
                px[y * S + x] = new Color(1f, 1f, 1f, inside ? 1f : 0f);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
