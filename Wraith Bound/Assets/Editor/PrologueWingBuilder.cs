using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static PrologueMapKit;

/// <summary>
/// 프롤로그 로비 북쪽 구역: 복도 A · ② 진료실 · ③ 병실(잠긴 문, 트롤리 숨기) · 크리처 대기실 · ④ 긴 복도 B · 비상구.
/// 좌표는 PrologueLayout. 잠긴 문·숨는 곳은 본편 프리팹 그대로(LockedDoor.prefab, Trolley_Hide.prefab).
/// </summary>
public static class PrologueWingBuilder
{
    /// <summary>복도 A 북쪽 끝 철문 문짝 (E2에서 쾅 열리고 크리처가 나옴).</summary>
    public static Transform SteelDoor { get; private set; }

    private const string LockedDoorPrefab = "Assets/Prefab/Object/Chapter1/LockedDoor.prefab";
    private const string TrolleyHidePrefab = "Assets/Prefab/Object/Chapter1/Trolley_Hide.prefab";

    /// <summary>병실 입구 잠긴 문 (진료실 열쇠로 염) — 본편 LockedDoor.prefab의 DoorClick.</summary>
    public static DoorClick WardDoor { get; private set; }

    /// <summary>복도 B 끝 비상구 문 (잠김) 루트.</summary>
    public static Transform ExitDoor { get; private set; }

    // 4×4 칸 (x, z 최소 모서리)
    private static readonly Vector2Int[] CorridorA = { new(4, 16), new(4, 20), new(4, 24), new(4, 28) };
    private static readonly Vector2Int[] SpawnRoom = { new(4, 32) };
    private static readonly Vector2Int[] Exam = { new(-4, 20), new(0, 20), new(-4, 24), new(0, 24) };
    private static readonly Vector2Int[] Ward = { new(8, 20), new(12, 20), new(8, 24), new(12, 24), new(8, 28), new(12, 28) };

    public static void Build(Transform root)
    {
        Transform shell = Child(root, "Shell");
        Transform props = Child(root, "Props");

        var cells = new List<Vector2Int>();
        cells.AddRange(CorridorA);
        cells.AddRange(SpawnRoom);
        cells.AddRange(Exam);
        cells.AddRange(Ward);
        for (int x = 16; x < 48; x += 4)
        {
            cells.Add(new Vector2Int(x, 28)); // 긴 복도 B
        }

        cells.Add(new Vector2Int(48, 28)); // 비상구 너머 (유리 너머가 비어 보이지 않게 바닥만)
        BuildFloorsAndCeilings(Child(shell, "FloorCeiling"), cells);
        BuildWalls(Child(shell, "Walls"));
        BuildProps(props);
        // 숨는 곳: 본편 트롤리 그대로 (밑으로 숨음, 앞면 +z — 크리처가 그 앞을 지나감)
        GameObject trolley = Place(Load(TrolleyHidePrefab), PrologueLayout.HideSpotPosition, Quaternion.Euler(0f, PrologueLayout.HideSpotYaw, 0f), props);
        if (trolley != null)
        {
            trolley.transform.localScale = Vector3.one; // 본편 프리팹은 5배 타일 안에서 쓰려고 루트 크기가 줄어 있을 수 있음
        }
    }

    private static void BuildFloorsAndCeilings(Transform parent, List<Vector2Int> cells)
    {
        GameObject floorA = Load(Interior + "HH_Flooring_Concrete_4x4m_02.prefab");
        GameObject floorB = Load(Interior + "HH_Flooring_Concrete_4x4m_04.prefab");
        GameObject ceil = Load(Interior + "HH_Ceiling_Armstrong_4х4m_02.prefab");
        GameObject broken = Load(Interior + "HH_Ceiling_Armstrong_4х4m_Broken.prefab");
        GameObject lamp = Load(Interior + "HH_Ceiling_Armstrong_Lamp_4х4m_02.prefab");

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int c = cells[i];
            FloorTile(((c.x + c.y) / 4) % 2 == 0 ? floorA : floorB, c.x, c.y, parent);
            GameObject top = i % 5 == 2 ? broken : i % 3 == 0 ? lamp : ceil;
            CeilingTile(top, c.x, c.y, parent);
        }
    }

    private static void BuildWalls(Transform parent)
    {
        // 복도 A 서쪽 벽 (x=4) — z24~28은 진료실 입구
        Along(Wall4, new Vector3(4f, 0f, 16f), Vector3.forward, parent);
        Along(Wall4, new Vector3(4f, 0f, 20f), Vector3.forward, parent);
        Along(Archway, new Vector3(4f, 0f, 24f), Vector3.forward, parent);
        Along(Wall4, new Vector3(4f, 0f, 28f), Vector3.forward, parent);

        // 복도 A 동쪽 벽 (x=8) — z24~28은 병실 입구
        Along(Wall4, new Vector3(8f, 0f, 32f), Vector3.back, parent);
        GameObject wardDoor = Along(Load(LockedDoorPrefab), new Vector3(8f, 0f, 28f), Vector3.back, parent); // 병실 입구 (열쇠)
        if (wardDoor != null)
        {
            wardDoor.transform.localScale = Vector3.one; // 프리팹 루트가 0.2 (Chapter1 타일이 5배라서) → 이 맵은 실제 크기
        }
        WardDoor = wardDoor != null ? wardDoor.GetComponentInChildren<DoorClick>(true) : null;
        Along(Wall4, new Vector3(8f, 0f, 24f), Vector3.back, parent);
        Along(Wall4, new Vector3(8f, 0f, 20f), Vector3.back, parent);

        // 복도 A 북쪽 끝 철문 (z=32) + 그 뒤 크리처 대기실
        GameObject steel = Along(DoorWall, new Vector3(4f, 0f, 32f), Vector3.right, parent);
        SteelDoor = steel != null ? FindDeep(steel.transform, "HH_Interior_Doors_02_LOD0") : null;
        Along(Wall4, new Vector3(4f, 0f, 32f), Vector3.forward, parent);
        Along(Wall4, new Vector3(8f, 0f, 36f), Vector3.back, parent);
        Along(Wall4, new Vector3(4f, 0f, 36f), Vector3.right, parent);

        // ② 진료실 (x−4~4, z20~28) — 동쪽은 복도 A 서쪽 벽
        Along(Wall4, new Vector3(4f, 0f, 20f), Vector3.left, parent);
        Along(Wall4, new Vector3(0f, 0f, 20f), Vector3.left, parent);
        Along(Wall4, new Vector3(-4f, 0f, 20f), Vector3.forward, parent);
        Along(Wall4, new Vector3(-4f, 0f, 24f), Vector3.forward, parent);
        Along(Wall4, new Vector3(-4f, 0f, 28f), Vector3.right, parent);
        Along(Wall4, new Vector3(0f, 0f, 28f), Vector3.right, parent);

        // ③ 병실 (x8~16, z20~32) — 서쪽은 복도 A 동쪽 벽, 동쪽 z28~32는 뒷문(복도 B로)
        Along(Wall4, new Vector3(16f, 0f, 20f), Vector3.left, parent);
        Along(Wall4, new Vector3(12f, 0f, 20f), Vector3.left, parent);
        Along(Wall4, new Vector3(8f, 0f, 32f), Vector3.right, parent);
        Along(Wall4, new Vector3(12f, 0f, 32f), Vector3.right, parent);
        Along(Archway, new Vector3(16f, 0f, 32f), Vector3.back, parent);
        Along(Wall4, new Vector3(16f, 0f, 28f), Vector3.back, parent);
        Along(Wall4, new Vector3(16f, 0f, 24f), Vector3.back, parent);

        // ④ 긴 복도 B (x16~48, z28~32)
        for (float x = 48f; x > 16f; x -= 4f)
        {
            Along(Wall4, new Vector3(x, 0f, 28f), Vector3.left, parent);
        }

        for (float x = 16f; x < 48f; x += 4f)
        {
            Along(Wall4, new Vector3(x, 0f, 32f), Vector3.right, parent);
        }

        // 끝: 비상구 (유리 양문, 열리지 않음)
        GameObject exit = Along(Load(Interior + "HH_Hospital_DoubleDoor_01.prefab"), new Vector3(48f, 0f, 32f), Vector3.back, parent);
        ExitDoor = exit != null ? exit.transform : null;
        // 유리 너머 작은 공간을 막음 (빈 허공이 보이지 않게)
        Along(Wall4, new Vector3(52f, 0f, 32f), Vector3.back, parent);
        Along(Wall4, new Vector3(52f, 0f, 28f), Vector3.left, parent);
        Along(Wall4, new Vector3(48f, 0f, 32f), Vector3.right, parent);
    }

    private static void BuildProps(Transform parent)
    {
        // 복도 A
        Prop("HH_Stretcher_01", new Vector3(4.9f, 0f, 21.5f), 0f, parent);
        Prop("HH_IV_Stand", new Vector3(7.3f, 0f, 18.6f), 30f, parent);
        Prop("HH_Sign_Warning", new Vector3(6f, 1.9f, 31.85f), 180f, parent);
        Prop("HH_Quarantine_Tape_Stanchions_2m", new Vector3(6f, 0f, 30.0f), 90f, parent);

        // ② 진료실
        Prop("HH_Desk_Doctor", new Vector3(-1.4f, 0f, 25.6f), 90f, parent);
        Prop("HH_Office_Chair", new Vector3(-2.6f, 0f, 25.4f), 80f, parent);
        Prop("HH_Document_Cabinet", new Vector3(-3.4f, 0f, 21.2f), 90f, parent);
        Prop("HH_Glass_Medical_Cabinet", new Vector3(1.0f, 0f, 27.4f), 180f, parent);
        Prop("HH_Medical_Screen_01", new Vector3(2.2f, 0f, 21.6f), 30f, parent);
        Prop("HH_Hospital_Bed_02", new Vector3(-2.2f, 0f, 21.6f), 90f, parent);

        // ③ 병실
        // 뒷문(동쪽 벽 z≈29.6~31.2)과 입구(서쪽 z≈25.6~27.2) 앞은 비워 둠 — 크리처·플레이어 동선
        Prop("HH_Hospital_Bed_01", new Vector3(10.4f, 0f, 30.5f), 0f, parent);
        Prop("HH_Hospital_Bed_01", new Vector3(13.0f, 0f, 26.5f), 90f, parent);
        Prop("HH_IV_Stand", new Vector3(11.9f, 0f, 31.0f), 0f, parent);
        Prop("HH_Nightstand_Box_01", new Vector3(12.0f, 0f, 29.3f), 0f, parent);
        Prop("HH_Wheelchair", new Vector3(15.0f, 0f, 24.2f), 250f, parent);

        // ④ 긴 복도 B — 가운데는 비워 둠(추격 동선)
        Prop("HH_Stretcher_01", new Vector3(22f, 0f, 31.2f), 90f, parent);
        Prop("HH_Medical_Trolley", new Vector3(29f, 0f, 28.7f), 0f, parent);
        Prop("HH_Oxygen_Cylinder_01", new Vector3(35f, 0f, 31.4f), 0f, parent);
        Prop("HH_Wheelchair", new Vector3(40f, 0f, 28.8f), 200f, parent);
        Prop("HH_Hospital_Journal_04", new Vector3(26f, 0f, 30.3f), 40f, parent);
    }
}
