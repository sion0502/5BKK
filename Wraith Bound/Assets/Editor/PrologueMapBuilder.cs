using UnityEditor;
using UnityEngine;

/// <summary>
/// 프롤로그 맵 뼈대를 The_Horror_Hospital 모듈로 조립합니다. PrologueSceneBuilder가 호출.
/// 좌표(바닥 y=0, 1칸 4m):
///   로비      x 0~12, z 0~16  (남쪽 벽 z=0 가운데 x 4~8이 정문 = 유리 없는 실내 문 모듈, 문짝 x 5.33~6.67·경첩 x 6.67)
///   북쪽 복도 x 4~8,  z 16~24 (로비와 아치로 연결, 끝은 닫힌 문 — 첫 사망 동선은 다음에 확장)
///   정문 바깥 x 0~12, z −8~0  (포치 바닥만)
/// 세부 소품 꾸미기는 팀원 몫 — 여기선 벽·바닥·천장·문과 핵심 소품(접수대·대기 의자·자판기 등)만.
/// </summary>
public static class PrologueMapBuilder
{
    private const string HH = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/";
    private const string Interior = HH + "Hospital_Modules/Module_Interior/";
    private const string Modules = HH + "Hospital_Modules/";
    private const string Env = HH + "Hospital_Environment/";

    public const float CeilingHeight = 4f;

    /// <summary>정문 문짝(경첩 피벗). 컷씬에서 쾅 닫히고 이후 절대 안 열림 — 빛이 안 비치는 불투명 문.</summary>
    public static Transform FrontDoor { get; private set; }

    /// <summary>접수대 위 손전등 놓을 자리 근처 (레이캐스트 시작점 x,z).</summary>
    public static readonly Vector3 FlashlightSpot = new Vector3(3.0f, 0f, 8.4f);

    public static Transform Build()
    {
        Transform root = new GameObject("Prologue_Map").transform;
        Transform shell = Child(root, "Shell");
        Transform props = Child(root, "Props");

        BuildFloors(Child(shell, "Floor"));
        BuildCeilings(Child(shell, "Ceiling"));
        BuildWalls(Child(shell, "Walls"));
        BuildProps(props);

        // 에셋 기본 문 스크립트(AE_Door)는 E로 아무 때나 열리고 OnGUI 글자를 띄움 → 전부 제거 (문은 우리 스크립트로만)
        foreach (Art_Equilibrium.AE_Door aeDoor in root.GetComponentsInChildren<Art_Equilibrium.AE_Door>(true))
        {
            Object.DestroyImmediate(aeDoor);
        }

        return root;
    }

    // ───────────────────────────── 바닥·천장 ─────────────────────────────

    private static void BuildFloors(Transform parent)
    {
        GameObject a = Load(Interior + "HH_Flooring_Concrete_4x4m_01.prefab");
        GameObject b = Load(Interior + "HH_Flooring_Concrete_4x4m_03.prefab");
        GameObject outside = Load(Interior + "HH_Flooring_Concrete_4x4m_06.prefab");

        for (int x = 0; x < 12; x += 4)
        for (int z = 0; z < 16; z += 4)
        {
            FloorTile((x + z) % 8 == 0 ? a : b, x, z, parent);
        }

        FloorTile(a, 4, 16, parent);
        FloorTile(b, 4, 20, parent);

        for (int x = 0; x < 12; x += 4)
        for (int z = -8; z < 0; z += 4)
        {
            FloorTile(outside, x, z, parent);
        }
    }

    // 바닥 타일 피벗은 +x,+z 모서리 (−4~0 범위를 덮음)
    private static void FloorTile(GameObject prefab, float x, float z, Transform parent)
    {
        Place(prefab, new Vector3(x + 4f, 0f, z + 4f), Quaternion.identity, parent);
    }

    private static void BuildCeilings(Transform parent)
    {
        GameObject plain = Load(Interior + "HH_Ceiling_Armstrong_4х4m_01.prefab");
        GameObject plain2 = Load(Interior + "HH_Ceiling_Armstrong_4х4m_03.prefab");
        GameObject broken = Load(Interior + "HH_Ceiling_Armstrong_4х4m_Broken.prefab");
        GameObject lamp = Load(Interior + "HH_Ceiling_Armstrong_Lamp_4х4m_01.prefab");

        for (int x = 0; x < 12; x += 4)
        for (int z = 0; z < 16; z += 4)
        {
            GameObject prefab = plain;
            if (x == 4 && (z == 4 || z == 12)) prefab = lamp;      // 꺼진 형광등
            else if (x == 8 && z == 8) prefab = broken;            // 천장 내려앉음
            else if ((x + z) % 8 == 4) prefab = plain2;
            CeilingTile(prefab, x, z, parent);
        }

        CeilingTile(lamp, 4, 16, parent);
        CeilingTile(broken, 4, 20, parent);
    }

    // 천장은 HH 데모처럼 Z 180° 뒤집어 높이 4m에 (바닥과 같은 −4~0 범위)
    private static void CeilingTile(GameObject prefab, float x, float z, Transform parent)
    {
        Place(prefab, new Vector3(x + 4f, CeilingHeight, z + 4f), Quaternion.Euler(0f, 0f, 180f), parent);
    }

    // ───────────────────────────── 벽 ─────────────────────────────
    // 벽 모듈은 피벗에서 로컬 −Z로 뻗고 두께는 로컬 −X. Along(시작점, 진행 방향)으로 놓으면
    // 두께는 진행 방향 기준 오른쪽(cross(up, dir))으로 감 → 외곽은 시계 방향으로 돌아 두께가 안쪽으로 오게.

    private static void BuildWalls(Transform parent)
    {
        GameObject wall4 = Load(Interior + "HH_Hospital_InteriorWall_4x4m.prefab");
        GameObject wall2 = Load(Interior + "HH_Hospital_InteriorWall_2x4m.prefab");
        GameObject wall1 = Load(Interior + "HH_Hospital_InteriorWall_1x4m.prefab");
        GameObject window = Load(Modules + "HH_Hospital_Windows_4m_01.prefab");
        GameObject entrance = Load(Interior + "HH_Hospital_PartitionWall_Door_02.prefab"); // 유리 없는 문
        GameObject archway = Load(Interior + "HH_Hospital_Archway.prefab");
        GameObject endDoor = Load(Interior + "HH_Hospital_PartitionWall_Door_01.prefab");

        // 남쪽 (z=0): x 12→0, 가운데 정문. 문 모듈(두께 0.22)과 두께를 맞춰야 이음새에 두꺼운 벽의 뚫린 끝면이 안 보임
        GameObject partition = Load(Interior + "HH_Hospital_PartitionWall_4x4m.prefab");
        Along(partition, new Vector3(12f, 0f, 0f), Vector3.left, parent);
        GameObject door = Along(entrance, new Vector3(8f, 0f, 0f), Vector3.left, parent);
        Along(partition, new Vector3(4f, 0f, 0f), Vector3.left, parent);
        FrontDoor = door != null ? FindDeep(door.transform, "HH_Interior_Doors_02_LOD0") : null;

        // 서쪽 (x=0): z 0→16, 가운데 창문 두 칸 (달빛)
        Along(wall4, new Vector3(0f, 0f, 0f), Vector3.forward, parent);
        Along(window, new Vector3(0f, 0f, 4f), Vector3.forward, parent);
        Along(window, new Vector3(0f, 0f, 8f), Vector3.forward, parent);
        Along(wall4, new Vector3(0f, 0f, 12f), Vector3.forward, parent);

        // 북쪽 (z=16): x 0→12, 가운데 복도 아치
        Along(wall4, new Vector3(0f, 0f, 16f), Vector3.right, parent);
        Along(archway, new Vector3(4f, 0f, 16f), Vector3.right, parent);
        Along(wall4, new Vector3(8f, 0f, 16f), Vector3.right, parent);

        // 동쪽 (x=12): z 16→0
        for (float z = 16f; z > 0f; z -= 4f)
        {
            Along(wall4, new Vector3(12f, 0f, z), Vector3.back, parent);
        }

        // 복도 (x 4~8, z 16~24): 서쪽 벽 z 16→24, 동쪽 벽 z 24→16, 끝(z=24)은 닫힌 문
        Along(wall4, new Vector3(4f, 0f, 16f), Vector3.forward, parent);
        Along(wall4, new Vector3(4f, 0f, 20f), Vector3.forward, parent);
        Along(wall4, new Vector3(8f, 0f, 24f), Vector3.back, parent);
        Along(wall4, new Vector3(8f, 0f, 20f), Vector3.back, parent);
        Along(endDoor, new Vector3(4f, 0f, 24f), Vector3.right, parent);
    }

    private static GameObject Along(GameObject prefab, Vector3 start, Vector3 dir, Transform parent)
    {
        return Place(prefab, start, Quaternion.LookRotation(-dir, Vector3.up), parent);
    }

    // ───────────────────────────── 소품 ─────────────────────────────

    private static void BuildProps(Transform parent)
    {
        // 접수대 (서쪽, 입구에서 보면 왼쪽 앞)
        Prop("HH_Information_Desk", new Vector3(3.5f, 0f, 6f), 0f, parent);
        Prop("HH_Information_Desk_Sign", new Vector3(2f, 3.95f, 9f), 0f, parent);
        Prop("HH_Office_Chair_Rolling", new Vector3(1.3f, 0f, 8.2f), 70f, parent);
        Prop("HH_Telephone_02", new Vector3(2.9f, 1.05f, 10.6f), 250f, parent);

        // 대기 의자 (동쪽)
        Prop("HH_Public_Seating_01", new Vector3(8.2f, 0f, 7f), 0f, parent);
        Prop("HH_Public_Seating_01", new Vector3(10.2f, 0f, 7f), 180f, parent);
        Prop("HH_Public_Seating_02", new Vector3(9.2f, 0f, 12.2f), 90f, parent);

        // 자판기 (동쪽 벽, 입구 가까이)
        Prop("HH_Vending_Machine_01", new Vector3(11.47f, 0f, 2.2f), 180f, parent);
        Prop("HH_Vending_Machine_02", new Vector3(11.47f, 0f, 3.5f), 180f, parent);

        // 방치된 흔적
        Prop("HH_Wheelchair", new Vector3(7.4f, 0f, 10.4f), 140f, parent);
        Prop("HH_Wet_Floor", new Vector3(6.6f, 0f, 14.6f), 20f, parent);
        Prop("Plants/HH_Potted_Plant_01", new Vector3(0.9f, 0f, 0.9f), 0f, parent);
        Prop("Plants/HH_Potted_Plant_03", new Vector3(11.1f, 0f, 15.1f), 0f, parent);
        Prop("HH_Cardboard_Box_02", new Vector3(10.8f, 0f, 14.2f), 25f, parent);
        Prop("HH_Cardboard_Box_01", new Vector3(10.9f, 0f, 13.4f), 80f, parent);
        Prop("HH_Hospital_Journal_03", new Vector3(5.6f, 0f, 9.3f), 30f, parent);
        Prop("HH_Hospital_Journal_04", new Vector3(8.4f, 0f, 4.2f), 300f, parent);
        Prop("HH_Paper_Tablet", new Vector3(4.9f, 0f, 11.8f), 200f, parent);

        // 표지판·벽 장식
        Prop("HH_Exit_01", new Vector3(6f, 3.6f, 15.5f), 0f, parent);
        Prop("HH_Navigation_Sign_Surgery", new Vector3(6f, 3.1f, 16.1f), 0f, parent);
        Prop("HH_Wall_Clock", new Vector3(11.5f, 2.6f, 10f), 270f, parent);
        Prop("HH_Medical_Posters_03", new Vector3(11.5f, 1.6f, 12.5f), 270f, parent);
        Prop("HH_Hospital_Floor_Plan", new Vector3(8.6f, 1.6f, 15.5f), 0f, parent);
    }

    private static void Prop(string name, Vector3 position, float yaw, Transform parent)
    {
        Place(Load(Env + name + ".prefab"), position, Quaternion.Euler(0f, yaw, 0f), parent);
    }

    // ───────────────────────────── 유틸 ─────────────────────────────

    private static Transform FindDeep(Transform root, string name)
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

    private static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[PrologueMapBuilder] 프리팹 없음: " + path);
        }

        return prefab;
    }

    private static GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }
}
