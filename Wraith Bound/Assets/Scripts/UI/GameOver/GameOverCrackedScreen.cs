using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 바닥에 떨어져 깨진 캠코더 LCD 오버레이. 리소스 없이 절차적으로 그림.
///  • 충격 지점에서 사방으로 뻗는 금(밝은 유리 선 + 그림자) + 동심원으로 이어진 금 + 충격점의 잘게 부서진 부분
///  • 액정이 번진 검은 얼룩(가장자리 보라/청록 번짐)
///  • 죽은 픽셀 세로줄·가로줄 (가끔 깜빡임)
/// </summary>
public class GameOverCrackedScreen : MonoBehaviour
{
    private const int Width = 1280;
    private const int Height = 720;

    private Texture2D crackTexture;
    private Texture2D columnTexture;
    private Texture2D rowTexture;
    private CanvasGroup deadLines;
    private float nextFlicker;

    private float[] light;
    private float[] shadow;
    private System.Random rng;

    public void Build(RectTransform parent, int seed)
    {
        rng = new System.Random(seed);

        crackTexture = BuildCrackTexture();
        CreateRawImage("Cracks", parent, crackTexture);

        var linesGo = new GameObject("DeadPixelLines", typeof(RectTransform), typeof(CanvasGroup));
        linesGo.transform.SetParent(parent, false);
        Stretch((RectTransform)linesGo.transform);
        deadLines = linesGo.GetComponent<CanvasGroup>();
        deadLines.blocksRaycasts = false;
        columnTexture = BuildDeadColumns();
        rowTexture = BuildDeadRows();
        CreateRawImage("Columns", (RectTransform)linesGo.transform, columnTexture);
        CreateRawImage("Rows", (RectTransform)linesGo.transform, rowTexture);

        light = null;
        shadow = null;
    }

    private void Update()
    {
        // 죽은 줄은 접촉 불량처럼 가끔 꺼졌다 켜짐
        if (deadLines == null || Time.unscaledTime < nextFlicker)
            return;

        bool off = Random.value < 0.25f;
        deadLines.alpha = off ? Random.Range(0f, 0.3f) : 1f;
        nextFlicker = Time.unscaledTime + (off ? Random.Range(0.04f, 0.12f) : Random.Range(0.3f, 2.5f));
    }

    // ───────────────────────────── 금 ─────────────────────────────

    private Texture2D BuildCrackTexture()
    {
        light = new float[Width * Height];
        shadow = new float[Width * Height];

        // 충격점은 오른쪽 아래 구석 — 가운데 문구·선택지를 가리지 않게
        var impact = new Vector2(Width * Range(0.8f, 0.88f), Height * Range(0.12f, 0.2f));

        int radialCount = rng.Next(10, 14);
        var radialPoints = new Vector2[radialCount][];
        for (int i = 0; i < radialCount; i++)
        {
            float angle = (i + Range(-0.3f, 0.3f)) / radialCount * Mathf.PI * 2f;
            float length = Range(0.35f, 1.1f) * Width;
            radialPoints[i] = DrawCrack(impact, angle, length, Range(1.4f, 2.2f), 2);
        }

        // 금 사이를 잇는 동심원 조각
        foreach (float radius in new[] { 34f, 70f, 120f, 190f, 290f })
        {
            for (int i = 0; i < radialCount; i++)
            {
                if (rng.NextDouble() > 0.65)
                    continue;
                Vector2? a = PointAt(radialPoints[i], impact, radius);
                Vector2? b = PointAt(radialPoints[(i + 1) % radialCount], impact, radius * Range(0.85f, 1.15f));
                if (a.HasValue && b.HasValue)
                    DrawJagged(a.Value, b.Value, Mathf.Lerp(1.3f, 0.7f, radius / 290f));
            }
        }

        // 충격점: 잘게 부서진 유리
        for (int i = 0; i < 70; i++)
        {
            Vector2 p = impact + RandomInCircle(26f);
            DrawLine(p, p + RandomInCircle(14f), 0.8f, 0.9f);
        }
        Splat(impact, 22f, 0.35f);

        var pixels = new Color32[Width * Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            float l = Mathf.Clamp01(light[i]);
            float s = Mathf.Clamp01(shadow[i]) * (1f - l);
            float a = Mathf.Clamp01(l + s);
            float c = a > 0f ? l / a : 0f; // 밝은 선은 흰색, 그림자는 검정
            byte v = (byte)(c * 235f);
            pixels[i] = new Color32(v, v, (byte)Mathf.Min(255, v + 10), (byte)(a * 255f));
        }

        DrawInkBleed(pixels, impact + new Vector2(Range(40f, 90f), Range(90f, 150f)), Range(55f, 80f));

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>들쭉날쭉하게 뻗는 금 하나. 가끔 가지를 침. 지나간 점 목록 반환(동심원 연결용).</summary>
    private Vector2[] DrawCrack(Vector2 start, float angle, float length, float thickness, int depth)
    {
        var points = new System.Collections.Generic.List<Vector2> { start };
        Vector2 p = start;
        float travelled = 0f;
        while (travelled < length)
        {
            float step = Range(25f, 60f);
            angle += Range(-0.09f, 0.09f); // 유리 금은 거의 직선으로 뻗다 살짝 꺾임
            Vector2 next = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
            float t = travelled / length;
            DrawLine(p, next, Mathf.Lerp(thickness, 0.6f, t), Mathf.Lerp(0.85f, 0.35f, t));
            p = next;
            points.Add(p);
            travelled += step;

            if (depth > 0 && rng.NextDouble() < 0.07)
                DrawCrack(p, angle + Range(0.3f, 0.6f) * (rng.NextDouble() < 0.5 ? -1f : 1f),
                    (length - travelled) * Range(0.2f, 0.5f), thickness * 0.6f, depth - 1);

            if (p.x < -50 || p.x > Width + 50 || p.y < -50 || p.y > Height + 50)
                break;
        }
        return points.ToArray();
    }

    private static Vector2? PointAt(Vector2[] path, Vector2 origin, float radius)
    {
        foreach (Vector2 p in path)
        {
            if ((p - origin).magnitude >= radius)
                return p;
        }
        return null;
    }

    private void DrawJagged(Vector2 a, Vector2 b, float thickness)
    {
        Vector2 mid = (a + b) * 0.5f + RandomInCircle(Vector2.Distance(a, b) * 0.15f);
        DrawLine(a, mid, thickness, 0.6f);
        DrawLine(mid, b, thickness, 0.6f);
    }

    // 밝은 유리 선 + 오른쪽 아래로 1.5px 어긋난 그림자 → 금이 패인 것처럼 보임
    private void DrawLine(Vector2 a, Vector2 b, float thickness, float intensity)
    {
        float length = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length * 1.5f));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            Stamp(light, p, thickness, intensity);
            Stamp(shadow, p + new Vector2(1.5f, -1.5f), thickness + 0.6f, intensity * 0.8f);
        }
    }

    private void Splat(Vector2 center, float radius, float intensity)
    {
        Stamp(light, center, radius, intensity);
    }

    private static void Stamp(float[] buffer, Vector2 center, float radius, float intensity)
    {
        int r = Mathf.CeilToInt(radius + 1f);
        int cx = Mathf.RoundToInt(center.x), cy = Mathf.RoundToInt(center.y);
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= Height) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= Width) continue;
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(radius + 0.5f - d) * intensity;
                int i = y * Width + x;
                if (a > buffer[i]) buffer[i] = a;
            }
        }
    }

    // ───────────────────────────── 액정 번짐 ─────────────────────────────

    private void DrawInkBleed(Color32[] pixels, Vector2 center, float radius)
    {
        // 원 여러 개를 겹친 울퉁불퉁한 덩어리
        var blobs = new Vector3[9];
        for (int i = 0; i < blobs.Length; i++)
        {
            Vector2 offset = RandomInCircle(radius * 0.7f);
            blobs[i] = new Vector3(center.x + offset.x, center.y + offset.y, radius * Range(0.35f, 0.7f));
        }

        int r = Mathf.CeilToInt(radius * 1.6f);
        for (int y = (int)center.y - r; y <= (int)center.y + r; y++)
        {
            if (y < 0 || y >= Height) continue;
            for (int x = (int)center.x - r; x <= (int)center.x + r; x++)
            {
                if (x < 0 || x >= Width) continue;
                float field = 0f;
                foreach (Vector3 b in blobs)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(b.x, b.y)) / b.z;
                    field += Mathf.Max(0f, 1f - d * d);
                }

                if (field < 0.25f) continue;
                int i = y * Width + x;
                Color32 baseColor = pixels[i];
                Color32 ink;
                if (field < 0.45f)
                {
                    // 가장자리 무지갯빛 번짐
                    float t = Mathf.InverseLerp(0.25f, 0.45f, field);
                    ink = Color32.Lerp(new Color32(120, 40, 160, 0), new Color32(30, 140, 150, 200), t);
                }
                else
                {
                    ink = new Color32(4, 4, 6, 250);
                }
                pixels[i] = ink.a > baseColor.a ? ink : baseColor;
            }
        }
    }

    // ───────────────────────────── 죽은 픽셀 줄 ─────────────────────────────

    private Texture2D BuildDeadColumns()
    {
        var pixels = new Color32[Width];
        int count = rng.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(Width / 3, Width - 20);
            int w = rng.Next(1, 4);
            Color32 c = DeadPixelColor();
            for (int k = 0; k < w && x + k < Width; k++)
                pixels[x + k] = c;
        }
        var tex = new Texture2D(Width, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private Texture2D BuildDeadRows()
    {
        var pixels = new Color32[Height];
        int y = rng.Next(Height / 5, Height / 2);
        int h = rng.Next(2, 5);
        Color32 c = DeadPixelColor();
        c.a = 150;
        for (int k = 0; k < h; k++)
            pixels[y + k] = c;
        var tex = new Texture2D(1, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private Color32 DeadPixelColor()
    {
        switch (rng.Next(4))
        {
            case 0: return new Color32(230, 40, 220, 210); // 마젠타
            case 1: return new Color32(60, 255, 120, 200); // 초록
            case 2: return new Color32(240, 240, 255, 190); // 흰색
            default: return new Color32(0, 0, 0, 230);       // 검정
        }
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private Vector2 RandomInCircle(float radius)
    {
        float a = Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    private static void CreateRawImage(string name, RectTransform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (crackTexture != null) Destroy(crackTexture);
        if (columnTexture != null) Destroy(columnTexture);
        if (rowTexture != null) Destroy(rowTexture);
    }
}
