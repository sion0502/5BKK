using UnityEditor;
using UnityEngine;

/// <summary>
/// 프롤로그 맵 조립 공용 도구 (HH 모듈 배치 규칙). PrologueMapBuilder·PrologueWingBuilder가 사용.
/// - 바닥 타일 피벗은 +x,+z 모서리(−4~0 범위를 덮음), 천장은 Z 180° 뒤집어 높이 4m
/// - 벽 모듈은 피벗에서 로컬 −Z로 뻗고 두께는 로컬 −X → Along(시작점, 진행 방향)으로 놓으면
///   두께가 진행 방향 기준 오른쪽(cross(up, dir))으로 감. 방 안쪽이 오른쪽에 오도록 진행 방향을 고를 것.
/// </summary>
public static class PrologueMapKit
{
    public const string HH = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/";
    public const string Interior = HH + "Hospital_Modules/Module_Interior/";
    public const string Modules = HH + "Hospital_Modules/";
    public const string Env = HH + "Hospital_Environment/";

    public const float CeilingHeight = 4f;

    // 아치·문 모듈(두께 0.22)과 같은 두께의 칸막이 벽 — 두께 0.5 InteriorWall과 맞붙으면 뚫린 끝면이 틈처럼 보임
    public static GameObject Wall4 => Load(Interior + "HH_Hospital_PartitionWall_4x4m.prefab");
    public static GameObject Archway => Load(Interior + "HH_Hospital_Archway.prefab");
    public static GameObject DoorWall => Load(Interior + "HH_Hospital_PartitionWall_Door_02.prefab");

    public static void FloorTile(GameObject prefab, float x, float z, Transform parent)
    {
        GameObject tile = Place(prefab, new Vector3(x + 4f, 0f, z + 4f), Quaternion.identity, parent);

        // 일부 바닥 타일(Concrete_4x4m_02 등)은 콜라이더가 없음 → 밟을 수도, NavMesh도 안 생김
        if (tile != null && tile.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = tile.AddComponent<BoxCollider>();
            box.center = new Vector3(-2f, -0.05f, -2f);
            box.size = new Vector3(4f, 0.1f, 4f);
        }
    }

    public static void CeilingTile(GameObject prefab, float x, float z, Transform parent)
    {
        Place(prefab, new Vector3(x + 4f, CeilingHeight, z + 4f), Quaternion.Euler(0f, 0f, 180f), parent);
    }

    public static GameObject Along(GameObject prefab, Vector3 start, Vector3 dir, Transform parent)
    {
        return Place(prefab, start, Quaternion.LookRotation(-dir, Vector3.up), parent);
    }

    public static GameObject Prop(string name, Vector3 position, float yaw, Transform parent)
    {
        return Place(Load(Env + name + ".prefab"), position, Quaternion.Euler(0f, yaw, 0f), parent);
    }

    public static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[PrologueMap] 프리팹 없음: " + path);
        }

        return prefab;
    }

    public static GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    public static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
