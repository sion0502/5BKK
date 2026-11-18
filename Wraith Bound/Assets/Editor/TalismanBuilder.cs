using UnityEditor;
using UnityEngine;

/// <summary>
/// 불탈 수 있는 부적 오브젝트 생성 (허브 봉인문 부적과 같은 모양: 노란 종이 + 붓글씨 急急如律令).
///  구조: 루트(TalismanBurnEffect [+ DemoTalisman·트리거]) ─ Paper(쿼드, 알파 클립 Lit) / Ember(쿼드, 뒤 3mm, 주황 Unlit) / Glyphs(TMP) [/ Glow]
///  루트 크기 = 월드 배율(1 = 종이 0.42×1.15m). 쿼드 앞면 = 로컬 −z — 바닥에 눕히려면 Euler(90, y, 0).
/// 재질은 알파 클립을 켠 에셋으로 만들어 둠(셰이더 변형이 빌드에 포함되게) — 실행 중엔 인스턴스로 복제해 컷오프만 움직임.
/// </summary>
public static class TalismanBuilder
{
    private const string PaperMatPath = "Assets/Story/Demo/M_TalismanBurnPaper.mat";
    private const string EmberMatPath = "Assets/Story/Demo/M_TalismanEmber.mat";

    internal static TalismanBurnEffect Create(Transform parent, string name, Vector3 position, Quaternion rotation, float scale, bool glow)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one * scale;

        MeshRenderer paper = Quad(root.transform, "Paper", new Vector3(0f, 0f, 0f), PaperMaterial());
        MeshRenderer ember = Quad(root.transform, "Ember", new Vector3(0f, 0f, 0.003f), EmberMaterial());
        ember.enabled = false;

        GameObject textGo = new GameObject("Glyphs");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        var tmp = textGo.AddComponent<TMPro.TextMeshPro>();
        tmp.text = "急\n急\n如\n律\n令"; // 제목 붓글씨 폰트에 門·勅이 없어서 (허브 봉인 부적과 같음)
        tmp.fontSize = 1.8f;
        tmp.lineSpacing = -25f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.7f, 0.06f, 0.05f);
        tmp.rectTransform.sizeDelta = new Vector2(0.4f, 1.1f);
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        textGo.AddComponent<TitleFontText>();

        Light idle = null;
        if (glow)
        {
            // 어두운 맵에서 찾을 수 있게 은은한 빛 (종이 앞면 쪽으로 띄움)
            idle = new GameObject("Glow").AddComponent<Light>();
            idle.transform.SetParent(root.transform, false);
            idle.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            idle.type = LightType.Point;
            idle.color = new Color(1f, 0.8f, 0.45f);
            idle.intensity = 0.9f;
            idle.range = 1.8f;
            idle.shadows = LightShadows.None;
        }

        TalismanBurnEffect burn = root.AddComponent<TalismanBurnEffect>();
        var so = new SerializedObject(burn);
        so.FindProperty("paper").objectReferenceValue = paper;
        so.FindProperty("ember").objectReferenceValue = ember;
        so.FindProperty("glyphs").objectReferenceValue = tmp;
        so.FindProperty("idleGlow").objectReferenceValue = idle;
        so.ApplyModifiedPropertiesWithoutUndo();
        return burn;
    }

    /// <summary>E로 태우는 부적 (조준 판정용 트리거 + DemoTalisman).</summary>
    internal static DemoTalisman CreateInteractable(Transform parent, int index, Vector3 position, Quaternion rotation, float scale)
    {
        TalismanBurnEffect burn = Create(parent, "Talisman_" + (index + 1), position, rotation, scale, true);
        BoxCollider box = burn.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 0f, -0.1f);
        box.size = new Vector3(0.6f, 1.3f, 0.35f);
        DemoTalisman talisman = burn.gameObject.AddComponent<DemoTalisman>();
        var so = new SerializedObject(talisman);
        so.FindProperty("index").intValue = index;
        so.ApplyModifiedPropertiesWithoutUndo();
        return talisman;
    }

    private static MeshRenderer Quad(Transform parent, string name, Vector3 localPos, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localScale = new Vector3(0.42f, 1.15f, 1f);
        MeshRenderer r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    private static Material PaperMaterial()
    {
        Material mat = Ensure(PaperMatPath, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", new Color(0.93f, 0.79f, 0.36f));
        mat.SetFloat("_Smoothness", 0.1f);
        mat.SetFloat("_Metallic", 0f);
        AlphaClip(mat);
        return mat;
    }

    private static Material EmberMaterial()
    {
        Material mat = Ensure(EmberMatPath, "Universal Render Pipeline/Unlit");
        mat.SetColor("_BaseColor", new Color(4f, 1.5f, 0.3f)); // HDR — 블룸으로 번지는 불씨
        AlphaClip(mat);
        return mat;
    }

    private static Material Ensure(string path, string shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }

        return mat;
    }

    private static void AlphaClip(Material mat)
    {
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.001f);
        mat.SetFloat("_Cull", 0f); // 양면
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
    }
}
