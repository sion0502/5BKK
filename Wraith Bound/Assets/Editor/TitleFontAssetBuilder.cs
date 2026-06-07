using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 메인 메뉴 타이틀(鬼行)용 TMP Font Asset 생성.
/// 메뉴: Tools > UI > Build Main Menu Title Font
/// </summary>
public static class TitleFontAssetBuilder
{
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2.ttf";
    private const string FontAssetPath = "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2 SDF.asset";
    private const string TitleCharacters = "鬼行";

    [MenuItem("Tools/UI/Build Main Menu Title Font")]
    public static void BuildTitleFontAsset()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[TitleFont] Source font not found: {SourceFontPath}");
            return;
        }

        TMP_FontAsset existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existingAsset != null)
        {
            AssetDatabase.DeleteAsset(FontAssetPath);
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            120,
            8,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic);

        fontAsset.name = "ZiKuXingQiuFeiYangTi-2 SDF";

        if (!fontAsset.TryAddCharacters(TitleCharacters, out string missingCharacters))
        {
            Debug.LogWarning($"[TitleFont] Missing characters after build: {missingCharacters}");
        }

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TitleFont] Created font asset at {FontAssetPath}");
    }
}
