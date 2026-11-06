using UnityEditor;
using UnityEngine;
using static PrologueMapKit;

/// <summary>
/// 프롤로그 맵을 The_Horror_Hospital 모듈로 조립합니다. PrologueSceneBuilder가 호출. 좌표는 PrologueLayout 참고.
/// 여기선 ① 로비(정문·접수대)와 정문 바깥 포치, 나머지 구역(복도 A·진료실·병실·긴 복도 B)은 PrologueWingBuilder.
/// 세부 소품 꾸미기는 팀원 몫 — 벽·바닥·천장·문과 동선에 필요한 핵심 소품만.
/// </summary>
public static class PrologueMapBuilder
{
    private const string EntranceGlassMaterialPath = "Assets/Story/Prologue/Prologue_EntranceGlass.mat";

    /// <summary>병원 정문 문짝(경첩 피벗, x4.24~6.0). 컷씬에서 쾅 닫히고 이후 절대 안 열림.</summary>
    public static Transform FrontDoor { get; private set; }

    public static Transform Build()
    {
        Transform root = new GameObject("Prologue_Map").transform;
        Transform lobby = Child(root, "Lobby");

        BuildLobbyShell(Child(lobby, "Shell"));
        BuildLobbyProps(Child(lobby, "Props"));
        PrologueWingBuilder.Build(Child(root, "Wing"));

        // 에셋 기본 문 스크립트(AE_Door)는 E로 아무 때나 열리고 OnGUI 글자를 띄움 → 전부 제거 (문은 우리 스크립트로만)
        foreach (Art_Equilibrium.AE_Door aeDoor in root.GetComponentsInChildren<Art_Equilibrium.AE_Door>(true))
        {
            Object.DestroyImmediate(aeDoor);
        }

        return root;
    }

    private static void BuildLobbyShell(Transform parent)
    {
        GameObject floorA = Load(Interior + "HH_Flooring_Concrete_4x4m_01.prefab");
        GameObject floorB = Load(Interior + "HH_Flooring_Concrete_4x4m_03.prefab");
        GameObject outside = Load(Interior + "HH_Flooring_Concrete_4x4m_06.prefab");
        GameObject ceil = Load(Interior + "HH_Ceiling_Armstrong_4х4m_01.prefab");
        GameObject ceil2 = Load(Interior + "HH_Ceiling_Armstrong_4х4m_03.prefab");
        GameObject broken = Load(Interior + "HH_Ceiling_Armstrong_4х4m_Broken.prefab");
        GameObject lamp = Load(Interior + "HH_Ceiling_Armstrong_Lamp_4х4m_01.prefab");

        for (int x = 0; x < 12; x += 4)
        for (int z = 0; z < 16; z += 4)
        {
            FloorTile((x + z) % 8 == 0 ? floorA : floorB, x, z, parent);

            GameObject c = ceil;
            if (x == 4 && (z == 4 || z == 12)) c = lamp;          // 꺼진 형광등
            else if (x == 8 && z == 8) c = broken;                // 천장 내려앉음
            else if ((x + z) % 8 == 4) c = ceil2;
            CeilingTile(c, x, z, parent);
        }

        for (int x = 0; x < 12; x += 4)
        for (int z = -8; z < 0; z += 4)
        {
            FloorTile(outside, x, z, parent);
        }

        GameObject window = Load(Modules + "HH_Hospital_Windows_4m_01.prefab");
        GameObject thick2 = Load(Interior + "HH_Hospital_InteriorWall_2x4m.prefab");
        GameObject thick1 = Load(Interior + "HH_Hospital_InteriorWall_1x4m.prefab");

        // 남쪽 (z=0): 가운데 x3~9가 병원 정문(인트로에서 본 HH 정문 모듈, 두께 0.52 — 양옆도 같은 두께 0.5 벽)
        Along(thick2, new Vector3(12f, 0f, 0f), Vector3.left, parent);
        Along(thick1, new Vector3(10f, 0f, 0f), Vector3.left, parent);
        GameObject entrance = Along(Load(Interior + "HH_Main_Entrance.prefab"), new Vector3(9f, 0f, 0f), Vector3.left, parent);
        Along(thick1, new Vector3(3f, 0f, 0f), Vector3.left, parent);
        Along(thick2, new Vector3(2f, 0f, 0f), Vector3.left, parent);
        FrontDoor = entrance != null ? entrance.transform.Find("HH_Front_Door") : null;
        if (entrance != null)
        {
            MakeEntranceGlassOpaque(entrance.transform);
        }

        // 서쪽 (x=0): 가운데 창문 두 칸 (달빛)
        Along(Wall4, new Vector3(0f, 0f, 0f), Vector3.forward, parent);
        Along(window, new Vector3(0f, 0f, 4f), Vector3.forward, parent);
        Along(window, new Vector3(0f, 0f, 8f), Vector3.forward, parent);
        Along(Wall4, new Vector3(0f, 0f, 12f), Vector3.forward, parent);

        // 북쪽 (z=16): 가운데 복도 A로 가는 아치
        Along(Wall4, new Vector3(0f, 0f, 16f), Vector3.right, parent);
        Along(Archway, new Vector3(4f, 0f, 16f), Vector3.right, parent);
        Along(Wall4, new Vector3(8f, 0f, 16f), Vector3.right, parent);

        // 동쪽 (x=12)
        for (float z = 16f; z > 0f; z -= 4f)
        {
            Along(Wall4, new Vector3(12f, 0f, z), Vector3.back, parent);
        }
    }

    // 정문 유리(문짝·양옆 고정창)를 빛이 통하지 않는 어두운 불투명 유리로 — 밖이 비쳐 보이지도, 달빛이 들어오지도 않게
    private static void MakeEntranceGlassOpaque(Transform entrance)
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(EntranceGlassMaterialPath);
        if (glass == null)
        {
            glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Prologue_EntranceGlass" };
            glass.SetColor("_BaseColor", new Color(0.035f, 0.04f, 0.045f));
            glass.SetFloat("_Metallic", 0.2f);
            glass.SetFloat("_Smoothness", 0.82f);
            AssetDatabase.CreateAsset(glass, EntranceGlassMaterialPath);
        }

        foreach (Renderer renderer in entrance.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.name.Contains("Glass"))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = glass;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    private static void BuildLobbyProps(Transform parent)
    {
        // 접수대 (서쪽) — 위에 손전등 (PrologueSceneBuilder가 배치)
        Prop("HH_Information_Desk", new Vector3(3.5f, 0f, 6f), 0f, parent);
        Prop("HH_Information_Desk_Sign", new Vector3(2f, 3.95f, 9f), 0f, parent);
        Prop("HH_Office_Chair_Rolling", new Vector3(1.3f, 0f, 8.2f), 70f, parent);
        Prop("HH_Telephone_02", new Vector3(2.9f, 1.05f, 10.6f), 250f, parent);

        // 대기 의자 (동쪽)
        Prop("HH_Public_Seating_01", new Vector3(8.2f, 0f, 7f), 0f, parent);
        Prop("HH_Public_Seating_01", new Vector3(10.2f, 0f, 7f), 180f, parent);
        Prop("HH_Public_Seating_02", new Vector3(9.2f, 0f, 12.2f), 90f, parent);

        // 자판기 (동쪽 벽, 입구 가까이)
        Prop("HH_Vending_Machine_01", new Vector3(11.75f, 0f, 2.2f), 180f, parent);
        Prop("HH_Vending_Machine_02", new Vector3(11.75f, 0f, 3.5f), 180f, parent);

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
        Prop("HH_Navigation_Sign_Surgery", new Vector3(6f, 3.1f, 16.1f), 0f, parent);
        Prop("HH_Wall_Clock", new Vector3(11.5f, 2.6f, 10f), 270f, parent);
        Prop("HH_Medical_Posters_03", new Vector3(11.5f, 1.6f, 12.5f), 270f, parent);
        Prop("HH_Hospital_Floor_Plan", new Vector3(8.6f, 1.6f, 15.5f), 0f, parent);
    }
}
