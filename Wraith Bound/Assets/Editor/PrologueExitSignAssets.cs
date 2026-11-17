using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프롤로그 비상구 표시등 재질 (Meshingun SM_ExitPannel 메쉬용). 원본 재질은 이 프로젝트(URP)에서 분홍으로 깨지는
/// 셰이더 그래프(S_MasterMaterial)라, 같은 텍스처로 URP Lit 재질을 새로 만듦. 발광 마스크는 ORME 텍스처의 알파(EXIT 글자)를
/// 따로 떼어 낸 흑백 PNG. 초록(평소)·빨강(마지막 컷씬)·꺼짐 3종 — `Assets/Story/Prologue/ExitSign/`.
/// </summary>
public static class PrologueExitSignAssets
{
    private const string Folder = "Assets/Story/Prologue/ExitSign";
    private const string TexFolder = "Assets/ExternalAssets/MeshingunStudio/ApocalypticHospital/Art/Textures/";
    private const string EmissionPath = Folder + "/T_ExitSign_Emission.png";

    public static readonly Color Green = new Color(0.25f, 1f, 0.45f) * 4f;
    public static readonly Color Red = new Color(1f, 0.06f, 0.03f) * 5f;

    public struct Set
    {
        public Material green;
        public Material red;
        public Material off;
    }

    public static Set Ensure()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Story/Prologue", "ExitSign");
        }

        Texture2D emission = EnsureEmissionMask();
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "T_ExitPannel_D.PNG");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "T_ExitPannel_N.png");
        return new Set
        {
            green = MakeMaterial("M_ExitSign_Green", albedo, normal, emission, Green),
            red = MakeMaterial("M_ExitSign_Red", albedo, normal, emission, Red),
            off = MakeMaterial("M_ExitSign_Off", albedo, normal, null, Color.black),
        };
    }

    private static Texture2D EnsureEmissionMask()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        if (existing != null)
        {
            return existing;
        }

        // ORME의 알파 = 발광 마스크 — 임포트 설정과 무관하게 파일을 직접 읽음
        var orme = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        orme.LoadImage(File.ReadAllBytes(TexFolder + "T_ExitPannel_ORME.PNG"));
        Color32[] src = orme.GetPixels32();
        var mask = new Texture2D(orme.width, orme.height, TextureFormat.RGB24, false);
        var dst = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            byte a = src[i].a;
            dst[i] = new Color32(a, a, a, 255);
        }

        mask.SetPixels32(dst);
        File.WriteAllBytes(EmissionPath, mask.EncodeToPNG());
        Object.DestroyImmediate(orme);
        Object.DestroyImmediate(mask);
        AssetDatabase.ImportAsset(EmissionPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
    }

    private static Material MakeMaterial(string name, Texture2D albedo, Texture2D normal, Texture2D emission, Color emissionColor)
    {
        string path = Folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", normal);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", 0.35f);
        m.SetFloat("_Metallic", 0f);
        if (emission != null)
        {
            m.SetTexture("_EmissionMap", emission);
            m.SetColor("_EmissionColor", emissionColor);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetTexture("_EmissionMap", null);
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        EditorUtility.SetDirty(m);
        return m;
    }
}
