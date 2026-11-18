using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기 고르기 화면 전용 절차적 스프라이트 (텍스처 파일 없이 실행 중 생성·캐시).
///  · WavyPaper: 가장자리가 물결치는 종이(부적·깃발 느낌) — 흰 바탕+섬유 얼룩+어두운 테두리, Image.color로 색을 입힘. seed마다 모양이 조금씩 다름
///  · Hatch: 잠긴 카드 빗금(타일)
///  · Lock: 자물쇠 아이콘
/// </summary>
public static class TraitAltarSkin
{
    private static readonly Dictionary<int, Sprite> Papers = new Dictionary<int, Sprite>();
    private static Sprite hatch;
    private static Sprite lockIcon;

    /// <summary>물결 종이 (가로 w × 세로 h 픽셀 비율 그대로 늘려 씀 — 9-slice 아님).</summary>
    public static Sprite WavyPaper(int seed)
    {
        if (Papers.TryGetValue(seed, out Sprite cached) && cached != null)
        {
            return cached;
        }

        const int w = 256, h = 320;
        const float margin = 10f, amp = 5f, rim = 7f;
        var rng = new System.Random(seed * 7919 + 13);
        float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f, p4 = (float)rng.NextDouble() * 6.28f;
        float f1 = 1.2f + (float)rng.NextDouble() * 0.8f, f2 = 1.2f + (float)rng.NextDouble() * 0.8f;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)h;
            float left = margin + amp * Mathf.Sin(v * Mathf.PI * f1 + p1) + amp * 0.4f * Mathf.Sin(v * 17f + p2);
            float right = w - margin + amp * Mathf.Sin(v * Mathf.PI * f2 + p3) * 0.8f;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float bottom = margin + amp * 0.8f * Mathf.Sin(u * Mathf.PI * 1.6f + p4);
                float top = h - margin + amp * Mathf.Sin(u * Mathf.PI * 1.3f + p2) * 0.9f;
                float d = Mathf.Min(Mathf.Min(x - left, right - x), Mathf.Min(y - bottom, top - y)); // 가장자리까지 거리(px)
                float a = Mathf.Clamp01(d + 0.5f); // 1px 안티에일리어싱
                float fiber = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.05f + seed, y * 0.012f) - 0.05f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f + seed);
                float edge = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(d / rim)); // 테두리는 어둡게
                byte c = (byte)Mathf.RoundToInt(Mathf.Clamp01(fiber * edge) * 255f);
                px[y * w + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        Sprite sprite = Make(px, w, h, "TraitPaper_" + seed, TextureWrapMode.Clamp);
        Papers[seed] = sprite;
        return sprite;
    }

    private static readonly Dictionary<int, Sprite> AgedPapers = new Dictionary<int, Sprite>();

    /// <summary>
    /// 낡은 부적 (색이 들어간 스프라이트 — Image.color는 흰색/어둡게만): 바랜 누런 종이 + 얼룩 + 가장자리가 그을려 검게 타고
    /// 불규칙하게 뜯긴 테두리, 맨 가장자리엔 아주 옅은 불씨 빛.
    /// </summary>
    public static Sprite AgedTalisman(int seed)
    {
        if (AgedPapers.TryGetValue(seed, out Sprite cached) && cached != null)
        {
            return cached;
        }

        const int w = 256, h = 320;
        const float margin = 12f, amp = 5f, burn = 16f;
        var rng = new System.Random(seed * 4241 + 7);
        float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
        float ox = (float)rng.NextDouble() * 50f, oy = (float)rng.NextDouble() * 50f;
        Color paper = new Color(0.6f, 0.5f, 0.31f);
        Color stain = new Color(0.36f, 0.27f, 0.15f);
        Color char0 = new Color(0.07f, 0.045f, 0.03f);
        Color ember = new Color(0.55f, 0.16f, 0.05f);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)h;
            float left = margin + amp * Mathf.Sin(v * 4.1f + p1);
            float right = w - margin + amp * Mathf.Sin(v * 3.3f + p2);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float bottom = margin + amp * Mathf.Sin(u * 4.7f + p3);
                float top = h - margin + amp * Mathf.Sin(u * 3.9f + p1);
                float d = Mathf.Min(Mathf.Min(x - left, right - x), Mathf.Min(y - bottom, top - y));
                // 가장자리를 노이즈로 갉아 먹어 그을려 뜯긴 모양
                float bite = Mathf.PerlinNoise(x * 0.045f + ox, y * 0.045f + oy) * 14f + Mathf.PerlinNoise(x * 0.18f + oy, y * 0.18f + ox) * 4f;
                float dn = d - bite + 6f;
                float a = Mathf.Clamp01(dn + 0.5f);

                float fiber = 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.04f + seed, y * 0.01f) - 0.06f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f + seed);
                float blot = Mathf.Clamp01((Mathf.PerlinNoise(x * 0.012f + oy, y * 0.012f + ox) - 0.45f) * 2.2f);
                Color c = Color.Lerp(paper, stain, blot * 0.75f) * fiber;
                float scorch = 1f - Mathf.Clamp01(dn / burn); // 가장자리일수록 1
                c = Color.Lerp(c, char0, Mathf.Pow(scorch, 1.6f));
                c = Color.Lerp(c, ember, Mathf.Clamp01(1f - dn / 2.5f) * 0.6f * a); // 맨 끝 불씨
                c.a = a;
                px[y * w + x] = c;
            }
        }

        Sprite sprite = Make(px, w, h, "TraitAgedTalisman_" + seed, TextureWrapMode.Clamp);
        AgedPapers[seed] = sprite;
        return sprite;
    }

    /// <summary>빗금 타일 (Image.type = Tiled 로 씀).</summary>
    public static Sprite Hatch()
    {
        if (hatch != null)
        {
            return hatch;
        }

        const int s = 48;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float k = Mathf.Repeat(x + y, 16f); // 45° 줄, 16px 간격
                float a = Mathf.Clamp01(1.6f - Mathf.Abs(k - 8f) * 0.9f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        hatch = Make(px, s, s, "TraitHatch", TextureWrapMode.Repeat);
        return hatch;
    }

    /// <summary>자물쇠: 고리(반원 띠+다리) + 몸통(둥근 사각) + 열쇠 구멍.</summary>
    public static Sprite Lock()
    {
        if (lockIcon != null)
        {
            return lockIcon;
        }

        const int s = 128;
        var px = new Color32[s * s];
        Vector2 ringC = new Vector2(64f, 76f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float a = 0f;
                // 몸통 (x 26~102, y 10~70, 모서리 반경 10)
                float bx = Mathf.Max(Mathf.Abs(p.x - 64f) - 28f, 0f), by = Mathf.Max(Mathf.Abs(p.y - 40f) - 20f, 0f);
                float body = 10f - Mathf.Sqrt(bx * bx + by * by);
                a = Mathf.Max(a, Mathf.Clamp01(body + 0.5f));
                // 고리: 반지름 26, 두께 10 — 위 반원 + 몸통까지 내려오는 다리
                float r = Vector2.Distance(p, ringC);
                float ring = 5f - Mathf.Abs(r - 26f);
                if (p.y >= ringC.y)
                {
                    a = Mathf.Max(a, Mathf.Clamp01(ring + 0.5f));
                }
                else if (p.y > 60f)
                {
                    float leg = 5f - Mathf.Min(Mathf.Abs(p.x - 38f), Mathf.Abs(p.x - 90f));
                    a = Mathf.Max(a, Mathf.Clamp01(leg + 0.5f));
                }

                // 열쇠 구멍 (둥근 머리 + 아래로 좁은 홈)
                float hole = Mathf.Min(Vector2.Distance(p, new Vector2(64f, 46f)) - 7f,
                    Mathf.Max(Mathf.Abs(p.x - 64f) - 3f, Mathf.Abs(p.y - 32f) - 10f));
                a *= Mathf.Clamp01(hole + 0.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        lockIcon = Make(px, s, s, "TraitLock", TextureWrapMode.Clamp);
        return lockIcon;
    }

    private static Sprite Make(Color32[] px, int w, int h, string name, TextureWrapMode wrap)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }
}
