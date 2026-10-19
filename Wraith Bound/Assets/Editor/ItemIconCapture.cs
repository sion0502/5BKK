using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 아이템 데이터(Resources/ItemDatas)마다 모델 프리팹을 찍어 투명 배경 아이콘 PNG를 만들고 icon에 연결하는 에디터 도구.
/// 검은 배경/흰 배경으로 두 번 렌더링해 차이로 알파를 계산 — URP 후처리·카메라 알파 지원 여부와 무관하게 투명 배경이 나옴.
/// 모델 선택 순서: worldDropPrefab → World_Item 폴더의 itemPrefab → World_Item 폴더의 "World_{에셋이름}" → itemPrefab(뷰 프리팹).
/// 메뉴: Tools > Items > Capture Item Icons
/// </summary>
public static class ItemIconCapture
{
    private const string ItemDataDir = "Assets/Resources/ItemDatas";
    private const string WorldItemDir = "Assets/Prefab/Item/World_Item";
    private const string IconDir = "Assets/Sprites/Icons";
    private const int IconSize = 512;
    private const float Padding = 1.12f;

    // 평평한 물건(높이가 가로의 이 비율보다 작음)은 위에서 더 내려다봄
    private const float FlatRatio = 0.3f;
    private const float DefaultPitch = 25f;
    private const float FlatPitch = 60f;
    private const float DefaultYaw = -35f;

    // 모델 방향이 제각각이라 기본 각도가 어색한 아이템만 개별 지정 (에셋 이름 → pitch, yaw, roll)
    private static readonly Dictionary<string, Vector3> AngleOverrides = new Dictionary<string, Vector3>
    {
        { "GlowStick", new Vector3(DefaultPitch, DefaultYaw, -45f) },             // 세로로 가늘어 대각선으로 눕힘
        { "PassiveBag_ExtraSlots2", new Vector3(DefaultPitch, DefaultYaw + 180f, 0f) }, // 멜빵 반대쪽(앞면)
    };

    [MenuItem("Tools/Items/Capture Item Icons")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(IconDir);

        var done = new List<string>();
        var skipped = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemDataDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var so = new SerializedObject(item);
            SerializedProperty iconProp = so.FindProperty("icon");
            if (iconProp == null)
                continue;

            GameObject prefab = FindModelPrefab(so, item.name);
            if (prefab == null)
            {
                skipped.Add(item.name);
                continue;
            }

            string iconPath = $"{IconDir}/{item.name}.png";
            if (!CapturePrefab(prefab, item.name, iconPath))
            {
                skipped.Add(item.name);
                continue;
            }

            ImportAsSprite(iconPath);
            iconProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            done.Add($"{item.name} ← {prefab.name}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemIconCapture] 생성 {done.Count}개:\n{string.Join("\n", done)}\n" +
                  $"모델 없음/실패 {skipped.Count}개: {string.Join(", ", skipped)}");
    }

    private static GameObject FindModelPrefab(SerializedObject so, string itemName)
    {
        var drop = so.FindProperty("worldDropPrefab")?.objectReferenceValue as GameObject;
        if (drop != null)
            return drop;

        var itemPrefab = so.FindProperty("itemPrefab")?.objectReferenceValue as GameObject;
        if (itemPrefab != null && AssetDatabase.GetAssetPath(itemPrefab).StartsWith(WorldItemDir))
            return itemPrefab;

        foreach (string guid in AssetDatabase.FindAssets($"World_{itemName} t:Prefab", new[] { WorldItemDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == $"World_{itemName}")
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }

        return itemPrefab;
    }

    private static bool CapturePrefab(GameObject prefab, string itemName, string iconPath)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            StripNonVisuals(instance);

            if (!TryGetBounds(instance, out Bounds bounds))
                return false;

            float flatness = bounds.size.y / Mathf.Max(bounds.size.x, bounds.size.z);
            Vector3 angle = AngleOverrides.TryGetValue(itemName, out Vector3 o)
                ? o
                : new Vector3(flatness < FlatRatio ? FlatPitch : DefaultPitch, DefaultYaw, 0f);
            Quaternion viewRot = Quaternion.Euler(angle.x, angle.y, angle.z);

            AddLight(scene, Quaternion.Euler(40f, -30f, 0f), 1.4f);
            AddLight(scene, Quaternion.Euler(20f, 150f, 0f), 0.7f);
            AddLight(scene, Quaternion.Euler(-60f, 0f, 0f), 0.4f);

            var camGo = new GameObject("IconCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.cameraType = CameraType.Game;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.renderShadows = false;

            FrameBounds(cam, bounds, viewRot);

            Texture2D onBlack = Render(cam, Color.black);
            Texture2D onWhite = Render(cam, Color.white);
            Texture2D result = ComposeAlpha(onBlack, onWhite);

            File.WriteAllBytes(iconPath, result.EncodeToPNG());
            Object.DestroyImmediate(onBlack);
            Object.DestroyImmediate(onWhite);
            Object.DestroyImmediate(result);
            return true;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // 렌더에 끼어들면 안 되는 것들(라이트·카메라·UI·파티클) 끄기
    private static void StripNonVisuals(GameObject root)
    {
        foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
        foreach (var c in root.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        foreach (var p in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) p.enabled = false;
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer))
                continue;
            if (found) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; found = true; }
        }
        return found;
    }

    private static void AddLight(Scene scene, Quaternion rotation, float intensity)
    {
        var go = new GameObject("IconLight");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.rotation = rotation;
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    // 바운딩 박스 8꼭짓점을 카메라 방향으로 투영해 화면에 꽉 차게 맞춤
    private static void FrameBounds(Camera cam, Bounds b, Quaternion viewRot)
    {
        Quaternion inv = Quaternion.Inverse(viewRot);
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 local = inv * corner;
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        Vector3 centerLocal = (min + max) * 0.5f;
        float depth = max.z - min.z;
        cam.transform.rotation = viewRot;
        cam.transform.position = viewRot * new Vector3(centerLocal.x, centerLocal.y, min.z - depth - 1f);
        cam.orthographicSize = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f * Padding;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = depth * 2f + 2f;
    }

    private static Texture2D Render(Camera cam, Color background)
    {
        var rt = RenderTexture.GetTemporary(IconSize, IconSize, 24, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB, 8);
        cam.backgroundColor = background;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    // 검은/흰 배경 결과 차이 = (1 - 알파). 색은 검은 배경 결과를 알파로 나눠 복원
    private static Texture2D ComposeAlpha(Texture2D onBlack, Texture2D onWhite)
    {
        Color[] b = onBlack.GetPixels();
        Color[] w = onWhite.GetPixels();
        var output = new Color[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            float diff = ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f;
            float a = Mathf.Clamp01(1f - diff);
            output[i] = a > 0.001f
                ? new Color(Mathf.Clamp01(b[i].r / a), Mathf.Clamp01(b[i].g / a), Mathf.Clamp01(b[i].b / a), a)
                : new Color(0f, 0f, 0f, 0f);
        }

        var tex = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
        tex.SetPixels(output);
        tex.Apply();
        return tex;
    }

    private static void ImportAsSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
