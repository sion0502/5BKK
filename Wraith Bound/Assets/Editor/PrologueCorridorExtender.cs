using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 프롤로그 긴 복도 B(x16~48)를 동쪽으로 늘림 — 마지막 컷씬에서 형광등 빛 웅덩이끼리 겹치지 않게(꺼질 때 드라마틱하게).
/// 끝(비상구 문·그 뒤 작은 공간·비상구 표시등·구역·지점)을 Extra만큼 밀고, 빈 구간을 같은 4m 모듈(벽·바닥·천장) 사본으로 채움.
/// 씬 전체 재생성 아님. 이미 늘렸으면(문이 x48이 아니면) 아무것도 안 함. 실행 뒤 NavMesh 다시 구움.
/// 모듈 기준점: 바닥·천장 (x,·,32)는 x−4~x 칸, 남쪽 벽 (x,0,28) rot90은 x−4~x, 북쪽 벽 (x,0,32) rot270은 x~x+4.
/// </summary>
public static class PrologueCorridorExtender
{
    public const float OldEndX = 48f;
    public const float Extra = 12f;

    [MenuItem("Tools/Story/Extend Prologue Corridor B (+12m)")]
    public static void Extend()
    {
        Transform wing = GameObject.Find("Prologue_Map")?.transform.Find("Wing/Shell");
        Transform events = GameObject.Find("Prologue_Events")?.transform;
        if (wing == null || events == null)
        {
            Debug.LogError("[CorridorExtender] Prologue 씬을 열고 실행");
            return;
        }

        Transform walls = wing.Find("Walls");
        Transform floorCeiling = wing.Find("FloorCeiling");
        Transform door = Find(walls, "HH_Hospital_DoubleDoor_01", new Vector3(OldEndX, 0f, 32f));
        if (door == null)
        {
            Debug.LogWarning("[CorridorExtender] x48에 비상구 문이 없음 — 이미 늘렸거나 배치가 다름. 아무것도 안 함");
            return;
        }

        // 밀 것: 끝 문, 문 뒤 작은 공간(벽 3·바닥·천장)
        var shift = new List<Transform>
        {
            door,
            Find(walls, "HH_Hospital_PartitionWall_4x4m", new Vector3(52f, 0f, 32f), 0f),
            Find(walls, "HH_Hospital_PartitionWall_4x4m", new Vector3(52f, 0f, 28f), 90f),
            Find(walls, "HH_Hospital_PartitionWall_4x4m", new Vector3(48f, 0f, 32f), 270f),
            Find(floorCeiling, "HH_Flooring", new Vector3(52f, 0f, 32f)),
            Find(floorCeiling, "HH_Ceiling", new Vector3(52f, 4f, 32f)),
        };

        // 채울 모듈의 본보기 (지금 복도 구간)
        Transform floorA = Find(floorCeiling, "HH_Flooring", new Vector3(48f, 0f, 32f));
        Transform floorB = Find(floorCeiling, "HH_Flooring", new Vector3(44f, 0f, 32f));
        Transform ceiling = Find(floorCeiling, "HH_Ceiling_Armstrong_4х4m_02", new Vector3(40f, 4f, 32f));
        Transform ceilingBroken = Find(floorCeiling, "HH_Ceiling", new Vector3(48f, 4f, 32f));
        Transform south = Find(walls, "HH_Hospital_PartitionWall_4x4m", new Vector3(48f, 0f, 28f), 90f);
        Transform north = Find(walls, "HH_Hospital_PartitionWall_4x4m", new Vector3(44f, 0f, 32f), 270f);
        foreach (Transform t in shift)
        {
            if (t == null)
            {
                Debug.LogError("[CorridorExtender] 밀 모듈을 못 찾음 — 중단");
                return;
            }
        }

        if (floorA == null || floorB == null || ceiling == null || ceilingBroken == null || south == null || north == null)
        {
            Debug.LogError("[CorridorExtender] 본보기 모듈을 못 찾음 — 중단");
            return;
        }

        Undo.SetCurrentGroupName("Extend Prologue Corridor B");
        foreach (Transform t in shift)
        {
            Undo.RecordObject(t, "shift");
            t.position += Vector3.right * Extra;
        }

        int steps = Mathf.RoundToInt(Extra / 4f);
        for (int k = 1; k <= steps; k++)
        {
            float x = OldEndX + 4f * k;
            Copy(k % 2 == 1 ? floorB : floorA, new Vector3(x, 0f, 32f));
            Copy(k == 2 ? ceilingBroken : ceiling, new Vector3(x, 4f, 32f));
            Copy(south, new Vector3(x, 0f, 28f));
            Copy(north, new Vector3(x - 4f, 0f, 32f));
        }

        // 비상구 표시등·구역·지점
        foreach (string path in new[] { "EmergencyExit", "Zones/Zone_ExitDoor", "Points/ExitDoorFace" })
        {
            Transform t = events.Find(path);
            if (t != null)
            {
                Undo.RecordObject(t, "shift");
                t.position += Vector3.right * Extra;
            }
            else
            {
                Debug.LogWarning("[CorridorExtender] 못 찾음: " + path);
            }
        }

        Physics.SyncTransforms();
        PrologueEventBuilder.RebakeNavMeshInOpenScene();
        EditorSceneManager.MarkSceneDirty(wing.gameObject.scene);
        Debug.Log("[CorridorExtender] 긴 복도 B를 x16~" + (OldEndX + Extra) + "로 늘림 (+" + Extra + "m), NavMesh 다시 구움");
    }

    private static Transform Find(Transform parent, string prefix, Vector3 position, float rotY = float.NaN)
    {
        foreach (Transform t in parent)
        {
            if (t.name.StartsWith(prefix) && Vector3.Distance(t.position, position) < 0.05f
                && (float.IsNaN(rotY) || Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, rotY)) < 1f))
            {
                return t;
            }
        }

        return null;
    }

    // 프리팹 연결을 유지한 사본 + 빌더가 따로 붙인 콜라이더(바닥 BoxCollider 등)도 복사
    private static void Copy(Transform template, Vector3 position)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject);
        GameObject copy = source != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(source, template.parent)
            : Object.Instantiate(template.gameObject, template.parent);
        copy.name = template.name;
        copy.transform.SetPositionAndRotation(position, template.rotation);
        copy.transform.localScale = template.localScale;
        copy.layer = template.gameObject.layer;
        copy.isStatic = template.gameObject.isStatic;
        foreach (BoxCollider box in template.GetComponents<BoxCollider>())
        {
            if (source == null || source.GetComponent<BoxCollider>() == null)
            {
                BoxCollider added = copy.AddComponent<BoxCollider>();
                EditorUtility.CopySerialized(box, added);
            }
        }

        Undo.RegisterCreatedObjectUndo(copy, "extend");
    }
}
