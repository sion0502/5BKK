using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인트로 씬 배경: The_Horror_Hospital 데모 씬(HH_Demo)의 외부 맵(도로·검문소·주차장·병원 건물·차량)을 가져와
/// "전기가 끊긴 채 방치된 병원"으로 바꿉니다. IntroSceneBuilder가 호출.
/// - 데모 씬 오브젝트를 Intro 씬으로 옮김 (데모 씬 파일은 저장하지 않으므로 원본 그대로)
/// - 조명 전부 끔, 발광 재질은 꺼진 사본으로 교체 (붉은 비상등만 유지)
/// - URP에서 분홍색으로 깨지는 Abandoned 팩 풀 재질 교체
/// - 도로·주차장·인도 위에 풀·덤불을 깔아 오래된 느낌
/// </summary>
public static class IntroEnvironmentBuilder
{
    private const string DemoScenePath = "Assets/ExternalAssets/The_Horror_Hospital/Scenes/HH_Demo.unity";
    private const string PowerOffDir = "Assets/Story/Intro/PowerOff";

    // 데모 씬에서 가져오지 않을 루트 (카메라·조명·볼륨·구름 파티클은 인트로 쪽 설정을 씀)
    private static readonly HashSet<string> SkipRoots = new HashSet<string>
    {
        "Main Camera", "Directional Light", "Light", "Global Volume", "VFX",
    };

    // 꺼지지 않고 남겨 둘 발광 재질 (비상 전원 느낌)
    private static readonly HashSet<string> KeepEmissive = new HashSet<string>
    {
        "HH_Ceiling_Armstrong_Red",
    };

    public static Transform ImportHospitalDemo(Scene target)
    {
        Transform env = new GameObject("Environment_HH").transform;
        SceneManager.MoveGameObjectToScene(env.gameObject, target);

        Scene demo = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
        foreach (GameObject root in demo.GetRootGameObjects())
        {
            if (SkipRoots.Contains(root.name))
            {
                continue;
            }

            SceneManager.MoveGameObjectToScene(root, target);
            root.transform.SetParent(env, true);
        }

        EditorSceneManager.CloseScene(demo, true); // 저장 안 함 → 데모 씬 원본 유지
        SceneManager.SetActiveScene(target);
        return env;
    }

    public static void PowerOff(Transform env)
    {
        int lights = 0;
        foreach (Light light in env.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
            lights++;
        }

        EnsureFolder(PowerOffDir);
        Dictionary<Material, Material> remap = new Dictionary<Material, Material>();
        int swapped = 0;
        foreach (Renderer renderer in env.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material off = GetPowerOffMaterial(materials[i], remap);
                if (off != null)
                {
                    materials[i] = off;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                swapped++;
            }
        }

        Debug.Log($"[IntroEnvironmentBuilder] 조명 {lights}개 끔, 발광 재질 {remap.Count}종 교체(렌더러 {swapped}개)");
    }

    private static Material GetPowerOffMaterial(Material source, Dictionary<Material, Material> remap)
    {
        if (source == null || KeepEmissive.Contains(source.name) || !source.HasProperty("_EmissionColor"))
        {
            return null;
        }

        if (remap.TryGetValue(source, out Material cached))
        {
            return cached;
        }

        bool emissive = source.IsKeywordEnabled("_EMISSION") && source.GetColor("_EmissionColor").maxColorComponent > 0.01f;
        if (!emissive)
        {
            return null;
        }

        string path = $"{PowerOffDir}/{source.name}_Off.mat";
        Material off = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (off == null)
        {
            off = new Material(source);
            off.SetColor("_EmissionColor", Color.black);
            off.DisableKeyword("_EMISSION");
            off.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            AssetDatabase.CreateAsset(off, path);
        }

        remap[source] = off;
        return off;
    }

    /// <summary>
    /// Abandoned 팩 Leaves.shader(URP에서 분홍색) 재질을 대체 재질로 교체.
    /// 산울타리(HH_Boxwood)도 원본이 풀 재질이라 풀 텍스처를 입히면 풀이 2층으로 쌓인 것처럼 보임 → 덤불 재질.
    /// </summary>
    public static void FixBrokenFoliage(Transform env, Material grassReplacement, Material hedgeReplacement)
    {
        if (grassReplacement == null)
        {
            return;
        }

        foreach (Renderer renderer in env.GetComponentsInChildren<Renderer>(true))
        {
            Material replacement = hedgeReplacement != null && renderer.name.StartsWith("HH_Boxwood")
                ? hedgeReplacement
                : grassReplacement;
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].shader != null &&
                    AssetDatabase.GetAssetPath(materials[i].shader).Contains("Abandoned_Psychiatric_Hospitals"))
                {
                    materials[i] = replacement;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    /// <summary>
    /// 지면(높이 0.3m 이하에 닿는 곳)에만 풀·덤불을 흩뿌림. 카메라 경로 위는 작은 풀만.
    /// 건물 안·차 위 등은 위에서 쏜 레이가 높은 곳에 먼저 닿으므로 자동으로 제외됨.
    /// </summary>
    public static void ScatterOvergrowth(Transform parent, GameObject[] grass, GameObject[] bushes, Material grassMaterial,
        Rect area, int grassCount, int bushCount, System.Func<Vector3, float> distanceToPath)
    {
        Physics.SyncTransforms();
        Transform root = new GameObject("Overgrowth").transform;
        root.SetParent(parent, false);

        System.Random rng = new System.Random(11);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        int placedGrass = 0;
        for (int i = 0; i < grassCount * 3 && placedGrass < grassCount; i++)
        {
            // 덩어리지게: 가끔 이전 위치 근처에 연달아 심음
            Vector3 p = new Vector3(R(area.xMin, area.xMax), 0f, R(area.yMin, area.yMax));
            if (!TryGround(p, out Vector3 ground))
            {
                continue;
            }

            float d = distanceToPath(ground);
            if (d < 0.4f)
            {
                continue;
            }

            int cluster = rng.Next(1, 4);
            for (int c = 0; c < cluster; c++)
            {
                Vector3 q = ground + new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f));
                if (c > 0 && (!TryGround(q, out q) || distanceToPath(q) < 0.4f))
                {
                    continue;
                }

                float scale = d < 1.5f ? R(0.5f, 0.9f) : R(0.8f, 1.6f);
                Apply(Place(grass[rng.Next(grass.Length)], q, R(0f, 360f), scale, root), grassMaterial);
                placedGrass++;
            }
        }

        int placedBushes = 0;
        for (int i = 0; i < bushCount * 4 && placedBushes < bushCount; i++)
        {
            Vector3 p = new Vector3(R(area.xMin, area.xMax), 0f, R(area.yMin, area.yMax));
            if (!TryGround(p, out Vector3 ground) || distanceToPath(ground) < 2f)
            {
                continue;
            }

            Place(bushes[rng.Next(bushes.Length)], ground, R(0f, 360f), R(0.8f, 1.5f), root);
            placedBushes++;
        }

        Debug.Log($"[IntroEnvironmentBuilder] 풀 {placedGrass}개, 덤불 {placedBushes}개 배치");
    }

    private static bool TryGround(Vector3 p, out Vector3 ground)
    {
        ground = p;
        if (!Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.point.y > 0.3f || hit.normal.y < 0.9f)
        {
            return false;
        }

        ground = hit.point;
        return true;
    }

    private static GameObject Place(GameObject prefab, Vector3 position, float yaw, float scale, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static void Apply(GameObject go, Material material)
    {
        if (go == null || material == null)
        {
            return;
        }

        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
