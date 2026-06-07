using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PlayerGameplayHud 프리팹 생성 및 씬 적용.
/// 메뉴: Tools &gt; UI &gt; Build Player Gameplay HUD Prefab
/// </summary>
public static class PlayerGameplayHudPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefab/UI/PlayerGameplayHud.prefab";

    private static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/Chapter/Chapter1.unity",
        "Assets/Scenes/ItemScene.unity",
    };

    [MenuItem("Tools/UI/Build Player Gameplay HUD Prefab")]
    public static void BuildPrefab()
    {
        EnsureFolder(Path.GetDirectoryName(PrefabPath));

        var root = new GameObject(
            "PlayerGameplayHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(PlayerStatusHudUI),
            typeof(QuickInventoryHudUI));

        root.layer = LayerMask.NameToLayer("UI");
        GameplayHudCanvasSetup.EnsureOverlayCanvas(root);

        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        ApplyDefaultHudSettings(root);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[PlayerGameplayHud] 프리팹 생성: {PrefabPath}");
    }

    [MenuItem("Tools/UI/Apply Player Gameplay HUD Prefab To Scenes")]
    public static void ApplyPrefabToScenes()
    {
        if (!File.Exists(PrefabPath))
        {
            BuildPrefab();
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerGameplayHud] 프리팹을 찾을 수 없습니다: {PrefabPath}");
            return;
        }

        string originalScene = SceneManager.GetActiveScene().path;

        foreach (string scenePath in TargetScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[PlayerGameplayHud] 씬 없음, 건너뜀: {scenePath}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!ReplaceSceneHud(prefab, scene))
            {
                Debug.LogWarning($"[PlayerGameplayHud] HUD 교체 실패: {scenePath}");
                continue;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PlayerGameplayHud] 씬 적용 완료: {scenePath}");
        }

        if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
        {
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
    }

    private static bool ReplaceSceneHud(GameObject prefab, Scene scene)
    {
        PlayerStatusHudUI existingStatus = Object.FindFirstObjectByType<PlayerStatusHudUI>();
        if (existingStatus == null)
        {
            return false;
        }

        GameObject oldRoot = existingStatus.GetComponent<Canvas>() != null
            ? existingStatus.gameObject
            : existingStatus.transform.root.gameObject;

        PlayerStatusHudUI oldStatus = oldRoot.GetComponent<PlayerStatusHudUI>();
        QuickInventoryHudUI oldInventory = oldRoot.GetComponent<QuickInventoryHudUI>();
        if (oldInventory == null)
        {
            oldInventory = oldRoot.GetComponentInChildren<QuickInventoryHudUI>(true);
        }

        Transform parent = oldRoot.transform.parent;
        int siblingIndex = oldRoot.transform.GetSiblingIndex();

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (instance == null)
        {
            return false;
        }

        instance.transform.SetParent(parent, false);
        instance.transform.SetSiblingIndex(siblingIndex);

        CopyComponentOverrides(oldStatus, instance.GetComponent<PlayerStatusHudUI>());
        CopyComponentOverrides(oldInventory, instance.GetComponent<QuickInventoryHudUI>());

        Object.DestroyImmediate(oldRoot);
        return true;
    }

    private static void ApplyDefaultHudSettings(GameObject root)
    {
        PlayerStatusHudUI statusHud = root.GetComponent<PlayerStatusHudUI>();
        SerializedObject statusSo = new SerializedObject(statusHud);
        statusSo.FindProperty("healthFontSize").intValue = 50;
        statusSo.FindProperty("staminaBarSize").vector2Value = new Vector2(1000f, 10f);
        statusSo.ApplyModifiedPropertiesWithoutUndo();

        QuickInventoryHudUI inventoryHud = root.GetComponent<QuickInventoryHudUI>();
        SerializedObject inventorySo = new SerializedObject(inventoryHud);
        inventorySo.FindProperty("slotSize").floatValue = 100f;
        inventorySo.FindProperty("slotSpacing").floatValue = 12f;
        inventorySo.FindProperty("slotNumberFontSize").intValue = 18;
        inventorySo.FindProperty("itemNameFontSize").intValue = 16;
        inventorySo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyComponentOverrides(MonoBehaviour source, MonoBehaviour destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        SerializedObject sourceSo = new SerializedObject(source);
        SerializedObject destSo = new SerializedObject(destination);
        SerializedProperty prop = sourceSo.GetIterator();

        if (!prop.NextVisible(true))
        {
            return;
        }

        do
        {
            if (prop.name == "m_Script")
            {
                continue;
            }

            SerializedProperty destProp = destSo.FindProperty(prop.propertyPath);
            if (destProp != null)
            {
                destSo.CopyFromSerializedProperty(prop);
            }
        }
        while (prop.NextVisible(false));

        destSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
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
