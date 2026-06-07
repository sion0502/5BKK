using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 등유 램프 패시브 아이템 SO, 월드 프리팹, 플레이어 조명 컴포넌트, ItemScene 배치를 한 번에 구성합니다.
/// 메뉴: Tools &gt; Items &gt; Build Kerosene Lamp Passive Item
/// </summary>
public static class KeroseneLampPassiveItemBuilder
{
    private const string SourceLampPrefabPath =
        "Assets/ExternalAssets/Survival Items Pack/Prefabs/Kerosene Lamp.prefab";

    private const string PassiveDataPath =
        "Assets/Resources/ItemDatas/Passive/PassiveKeroseneLamp.asset";

    private const string WorldPrefabPath =
        "Assets/Prefab/Item/World_Item/Passive/World_KeroseneLamp.prefab";

    private static readonly string[] PlayerScenePaths =
    {
        "Assets/Scenes/ItemScene.unity",
        "Assets/Scenes/Chapter/Chapter1.unity",
    };

    [MenuItem("Tools/Items/Build Kerosene Lamp Passive Item")]
    public static void BuildAll()
    {
        PassiveItem passiveData = EnsurePassiveData();
        GameObject worldPrefab = EnsureWorldPrefab(passiveData);
        passiveData.worldDropPrefab = worldPrefab;
        EditorUtility.SetDirty(passiveData);

        EnsurePlayerLanternControllerInScenes();

        string originalScene = SceneManager.GetActiveScene().path;
        ReplaceItemScenePickup(worldPrefab);

        if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
        {
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = worldPrefab;
        EditorGUIUtility.PingObject(worldPrefab);
        Debug.Log("[KeroseneLamp] 등유 램프 패시브 아이템 구성 완료.");
    }

    private static PassiveItem EnsurePassiveData()
    {
        EnsureFolder(Path.GetDirectoryName(PassiveDataPath));

        PassiveItem passive = AssetDatabase.LoadAssetAtPath<PassiveItem>(PassiveDataPath);
        if (passive == null)
        {
            passive = ScriptableObject.CreateInstance<PassiveItem>();
            AssetDatabase.CreateAsset(passive, PassiveDataPath);
        }

        passive.id = 3002;
        passive.itemName = "등유 램프";
        passive.type = ItemType.Passive;
        passive.description = "획득 시 주변 약 2m를 따뜻한 불빛으로 밝혀 줍니다.";
        passive.maxCount = 1;
        passive.destroyOnUse = false;
        passive.breakageChance = 0f;
        passive.showInHand = false;
        passive.statModifier = 0f;
        passive.extraSlots = 0;
        passive.providesAmbientLight = true;
        passive.ambientLightRange = 2f;
        passive.ambientLightIntensity = 2.5f;
        passive.ambientLightColor = new Color(1f, 0.72f, 0.38f, 1f);
        passive.flickerLight = true;

        EditorUtility.SetDirty(passive);
        return passive;
    }

    private static GameObject EnsureWorldPrefab(PassiveItem passiveData)
    {
        EnsureFolder(Path.GetDirectoryName(WorldPrefabPath));

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceLampPrefabPath);
        if (source == null)
        {
            Debug.LogError($"[KeroseneLamp] 소스 프리팹을 찾을 수 없습니다: {SourceLampPrefabPath}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            Debug.LogError("[KeroseneLamp] Kerosene Lamp 인스턴스 생성 실패.");
            return null;
        }

        instance.name = "World_KeroseneLamp";
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
        {
            interactableLayer = 3;
        }

        SetLayerRecursively(instance, interactableLayer);
        instance.isStatic = false;

        EnsureCollider(instance);
        EnsureInteractable(instance, passiveData);
        EnsureOutlineComponents(instance);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, WorldPrefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static void EnsureCollider(GameObject root)
    {
        Collider existing = root.GetComponent<Collider>();
        if (existing != null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.2f, 0.35f, 0.2f);
            box.center = new Vector3(0f, 0.18f, 0f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var collider = root.AddComponent<BoxCollider>();
        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = bounds.size;
        Transform rootTransform = root.transform;
        localSize.x /= Mathf.Max(rootTransform.lossyScale.x, 0.0001f);
        localSize.y /= Mathf.Max(rootTransform.lossyScale.y, 0.0001f);
        localSize.z /= Mathf.Max(rootTransform.lossyScale.z, 0.0001f);
        collider.center = localCenter;
        collider.size = localSize;
    }

    private static void EnsureInteractable(GameObject root, PassiveItem passiveData)
    {
        InteractableItem interactable = root.GetComponent<InteractableItem>();
        if (interactable == null)
        {
            interactable = root.AddComponent<InteractableItem>();
        }

        SerializedObject so = new SerializedObject(interactable);
        so.FindProperty("item").objectReferenceValue = passiveData;
        so.FindProperty("amount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureOutlineComponents(GameObject root)
    {
        if (root.GetComponent<Outline>() == null)
        {
            root.AddComponent<Outline>();
        }

        if (root.GetComponent<SpotOutline>() == null)
        {
            root.AddComponent<SpotOutline>();
        }
    }

    private static void EnsurePlayerLanternControllerInScenes()
    {
        foreach (string scenePath in PlayerScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning($"[KeroseneLamp] Player를 찾을 수 없어 PassiveLanternController를 건너뜀: {scenePath}");
                continue;
            }

            if (player.GetComponent<PassiveLanternController>() == null)
            {
                player.AddComponent<PassiveLanternController>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void ReplaceItemScenePickup(GameObject worldPrefab)
    {
        const string itemScenePath = "Assets/Scenes/ItemScene.unity";
        if (!File.Exists(itemScenePath) || worldPrefab == null)
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(itemScenePath, OpenSceneMode.Single);

        GameObject existing = GameObject.Find("Kerosene Lamp");
        Vector3 position = new Vector3(2.5f, 0.2f, 2.5f);
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;

        if (existing != null)
        {
            position = existing.transform.position;
            rotation = existing.transform.rotation;
            scale = existing.transform.localScale;
            Object.DestroyImmediate(existing);
        }

        GameObject placed = PrefabUtility.InstantiatePrefab(worldPrefab, scene) as GameObject;
        if (placed != null)
        {
            placed.transform.SetPositionAndRotation(position, rotation);
            placed.transform.localScale = scale;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = layer;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        folderPath = folderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
