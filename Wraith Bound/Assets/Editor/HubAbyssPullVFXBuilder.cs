using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hub Chapter1 In 구멍 주변 '빨려 들어감' 파티클 VFX를 생성·배치합니다. (런타임 스크립트 없음)
/// 메뉴: Tools > Hub > Build Abyss Pull VFX
/// </summary>
public static class HubAbyssPullVFXBuilder
{
    private const string PrefabPath = "Assets/Prefab/Object/All/HubAbyssPullVFX.prefab";
    private const string ShaftObjectName = "Chapter1 In";
    private const string VfxRootName = "AbyssPullVFX";

    [MenuItem("Tools/Hub/Build Abyss Pull VFX")]
    public static void BuildAndPlace()
    {
        GameObject shaft = GameObject.Find(ShaftObjectName);
        if (shaft == null)
        {
            Debug.LogError($"[HubAbyssPullVFX] '{ShaftObjectName}' 오브젝트를 찾을 수 없습니다. Hub 씬을 열어주세요.");
            return;
        }

        Transform existing = shaft.transform.Find(VfxRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = BuildVfxRoot();
        root.transform.SetParent(shaft.transform, false);
        root.transform.localPosition = ResolveFloorLocalPosition(shaft);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        EditorUtility.SetDirty(shaft);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log($"[HubAbyssPullVFX] '{VfxRootName}' 생성 완료. 프리팹: {PrefabPath}");
    }

    public static void BuildFromBatchMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Chapter/Hub.unity");
        BuildAndPlace();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }

    private static Vector3 ResolveFloorLocalPosition(GameObject shaft)
    {
        const float defaultFloorLocalY = 102.2f;
        Renderer renderer = shaft.GetComponent<Renderer>();
        if (renderer == null)
        {
            return new Vector3(0f, defaultFloorLocalY, 0f);
        }

        Bounds bounds = renderer.bounds;
        float floorY = 30.13f;
        foreach (GameObject candidate in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!candidate.name.Contains("Floor"))
            {
                continue;
            }

            foreach (Renderer floorRenderer in candidate.GetComponentsInChildren<Renderer>(true))
            {
                if (floorRenderer.bounds.max.y > floorY)
                {
                    floorY = floorRenderer.bounds.max.y;
                }
            }
        }

        float worldY = floorY + 0.08f;
        Vector3 local = shaft.transform.InverseTransformPoint(
            new Vector3(bounds.center.x, worldY, bounds.center.z)
        );
        local.x = 0f;
        local.z = 0f;
        return local;
    }

    private static GameObject BuildVfxRoot()
    {
        GameObject root = new GameObject(VfxRootName);

        CreatePullParticles(root.transform, "OuterMist", 6.5f, 26f, 0.75f, 0.24f, 2.6f);
        CreatePullParticles(root.transform, "MidPull", 4.2f, 38f, 1.5f, 0.17f, 1.9f);
        CreatePullParticles(root.transform, "CoreSink", 2.0f, 52f, 2.3f, 0.11f, 1.3f);

        return root;
    }

    private static void CreatePullParticles(
        Transform parent,
        string name,
        float radius,
        float rate,
        float speed,
        float size,
        float lifetime)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = new Color(0.07f, 0.07f, 0.09f, 0.6f);
        main.gravityModifier = 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(-2.8f, -1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-2.2f, -0.8f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 4.5f;
        limit.dampen = 0.2f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.32f;
        noise.scrollSpeed = 0.45f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.14f, 0.14f, 0.17f), 0f),
                new GradientColorKey(Color.black, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.7f, 0.12f),
                new GradientAlphaKey(0.25f, 0.82f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.EaseInOut(0f, 1.25f, 1f, 0.15f)
        );

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        renderer.sortingOrder = 5;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
