using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 캠코더(나이트비전) 아이템 에셋을 한 번에 생성하는 에디터 도구.
/// World_Camcorder / View_Camcorder 프리팹과 Equipment 에셋을 Unity API로 안전하게 만듭니다.
/// 메뉴: Tools > Camcorder > Build Camcorder Item
/// </summary>
public static class CamcorderItemBuilder
{
    private const string ModelPrefabPath =
        "Assets/Model/Item/Camcorder Video Camera/Prefabs/Camcorder_Black Dirty.prefab";

    private const string UiResourceDir =
        "Assets/Model/Item/Camcorder Video Camera/Demo/Demo_Scenes/Render_Texture_Example/Display_UI_Resources";

    private const string ViewPrefabPath = "Assets/Prefab/Item/View_Item/Equip/View_Camcorder.prefab";
    private const string WorldPrefabPath = "Assets/Prefab/Item/World_Item/Equip/World_Camcorder.prefab";
    private const string EquipmentAssetPath = "Assets/Resources/ItemDatas/Equipment/Camcorder.asset";

    private const int InteractableLayer = 3; // TagManager 기준

    [MenuItem("Tools/Camcorder/Build Camcorder Item")]
    public static void BuildAll()
    {
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (modelPrefab == null)
        {
            Debug.LogError($"[Camcorder] 모델 프리팹을 찾을 수 없습니다: {ModelPrefabPath}");
            return;
        }

        EnsureFolders();

        GameObject viewPrefab = BuildViewPrefab(modelPrefab);
        Equipment equipment = BuildEquipmentAsset(viewPrefab);
        BuildWorldPrefab(modelPrefab, equipment);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Camcorder] 생성 완료: " +
            $"{ViewPrefabPath} / {WorldPrefabPath} / {EquipmentAssetPath}");

        Selection.activeObject = equipment;
    }

    private static GameObject BuildViewPrefab(GameObject modelPrefab)
    {
        // 루트(빈) → RaisePivot → 모델
        GameObject root = new GameObject("View_Camcorder");
        GameObject raisePivot = new GameObject("RaisePivot");
        raisePivot.transform.SetParent(root.transform, false);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        model.transform.SetParent(raisePivot.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        CamcorderController controller = root.AddComponent<CamcorderController>();

        // private 직렬화 필드 연결
        Sprite frame = LoadSprite("Center_Frame.png");
        Sprite recDot = LoadSprite("Recording_Dot.png");
        Sprite battery = LoadSprite("Battery_4.png");

        SerializedObject so = new SerializedObject(controller);
        SetObjectRef(so, "raisePivot", raisePivot.transform);
        SetObjectRef(so, "frameSprite", frame);
        SetObjectRef(so, "recordingDotSprite", recDot);
        SetObjectRef(so, "batterySprite", battery);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static Equipment BuildEquipmentAsset(GameObject viewPrefab)
    {
        Equipment equipment = AssetDatabase.LoadAssetAtPath<Equipment>(EquipmentAssetPath);
        bool isNew = equipment == null;
        if (isNew)
        {
            equipment = ScriptableObject.CreateInstance<Equipment>();
        }

        equipment.id = 2003;
        equipment.itemName = "캠코더";
        equipment.type = ItemType.Equip;
        equipment.description = "야간투시 기능이 있는 캠코더.\n좌클릭으로 뷰파인더를 펼쳐 어둠 속을 볼 수 있다.";
        equipment.maxCount = 1;
        equipment.destroyOnUse = false;
        equipment.breakageChance = 0f;
        equipment.showInHand = true;
        equipment.useMode = EquipmentUseMode.ToggleOnClick;
        equipment.itemPrefab = viewPrefab;
        equipment.maxEnergy = 100f;
        equipment.consumeRate = 0.2f;
        equipment.range = 18f;

        if (isNew)
        {
            AssetDatabase.CreateAsset(equipment, EquipmentAssetPath);
        }
        else
        {
            EditorUtility.SetDirty(equipment);
        }
        return equipment;
    }

    private static void BuildWorldPrefab(GameObject modelPrefab, Equipment equipment)
    {
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        root.name = "World_Camcorder";
        SetLayerRecursively(root, InteractableLayer);

        BoxCollider box = root.AddComponent<BoxCollider>();
        if (TryGetLocalBounds(root, out Bounds bounds))
        {
            box.center = bounds.center;
            box.size = bounds.size;
        }

        ItemObject itemObject = root.AddComponent<ItemObject>();
        itemObject.itemData = equipment;

        PrefabUtility.SaveAsPrefabAsset(root, WorldPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefab/Item/View_Item/Equip");
        EnsureFolder("Assets/Prefab/Item/World_Item/Equip");
        EnsureFolder("Assets/Resources/ItemDatas/Equipment");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = UiResourceDir + "/" + fileName;
        if (!File.Exists(path))
            return null;

        // PNG를 Single Sprite로 강제 (textureType만 Sprite이고 spriteMode가 None이면 Sprite 서브에셋이 없어 로드 실패)
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null
            && (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static bool TryGetLocalBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = new Bounds(root.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Bounds wb = renderer.bounds;
            // 월드 바운드를 루트 로컬로 근사 변환
            Vector3 localCenter = root.transform.InverseTransformPoint(wb.center);
            Vector3 localSize = root.transform.InverseTransformVector(wb.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            bounds.Encapsulate(new Bounds(localCenter, localSize));
        }
        return true;
    }
}
