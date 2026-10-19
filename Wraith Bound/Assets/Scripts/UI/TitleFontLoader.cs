using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 메인메뉴 제목 "鬼行" 폰트(ZiKuXingQiuFeiYangTi-2)를 런타임에 TMP 폰트로 만들어 캐시.
/// MainMenuController와 같은 방식(Resources의 ttf → 동적 SDF). 게임오버 화면 등에서 사용.
/// </summary>
public static class TitleFontLoader
{
    public const string TitleText = "鬼行";
    public static readonly Color TitleRed = new Color(0.85f, 0.12f, 0.12f, 1f); // 메인메뉴 제목 색

    private const string ResourcesPath = "Fonts/Title/ZiKuXingQiuFeiYangTi-2";

    private static TMP_FontAsset cached;

    /// <summary>제목 폰트. 소스 폰트를 못 찾으면 null.</summary>
    public static TMP_FontAsset Load()
    {
        if (cached != null)
            return cached;

        Font source = Resources.Load<Font>(ResourcesPath);
        if (source == null)
            return null;

        cached = TMP_FontAsset.CreateFontAsset(source, 160, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        cached.TryAddCharacters(TitleText, out _);
        return cached;
    }
}
