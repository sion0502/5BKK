using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 열린 Prologue 씬에 마지막 컷씬(비상구 문 → 형광등 소등·깜빡임·붉은 등·돌진) 구성을 추가/갱신. Tools > Story > Setup Prologue Finale.
/// 씬 전체를 다시 만들지 않음(사용자가 손본 배치 유지) — Prologue_Events/Finale 묶음만 지우고 다시 만듦.
/// 긴 복도 B는 x16~60·z28~32(PrologueCorridorExtender로 12m 늘림), 천장 4m, 끝(x60) 비상구 문.
/// </summary>
public static class PrologueFinaleSetup
{
    private const string LampPrefab = "Assets/ExternalAssets/The_Horror_Hospital/Prefabs/Hospital_Environment/HH_Ceiling_Lamp_01.prefab";
    private const string OnMat = "Assets/ExternalAssets/The_Horror_Hospital/Materials/Hospital/HH_Ceiling_Armstrong.mat";
    private const string OffMat = "Assets/Story/Intro/PowerOff/HH_Ceiling_Armstrong_Off.mat";
    private const string RedMat = "Assets/ExternalAssets/The_Horror_Hospital/Materials/Hospital/HH_Ceiling_Armstrong_Red.mat";

    // 비상구 문 x (긴 복도 B를 PrologueCorridorExtender로 늘린 뒤 기준)
    private const float DoorX = PrologueCorridorExtender.OldEndX + PrologueCorridorExtender.Extra;
    // 형광등 중심 x (간호사 쪽=먼 쪽부터 — 이 순서로 꺼짐). 8m 간격·좁은 원뿔이라 빛 웅덩이 사이가 어두움.
    // 간호사는 등 바로 밑에 섬 — 위에서 내리꽂는 빛이라 얼굴·몸에 그림자가 짙게 짐
    private static readonly float[] LampX = { 26f, 34f, 42f, 50f };
    private const float CorridorZ = 30f;
    private const float CeilingY = 3.99f;

    [MenuItem("Tools/Story/Setup Prologue Finale")]
    public static void Setup()
    {
        GameObject events = GameObject.Find("Prologue_Events");
        PrologueDirector director = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        PrologueNurse nurse = Object.FindFirstObjectByType<PrologueNurse>(FindObjectsInactive.Include);
        if (events == null || director == null || nurse == null)
        {
            Debug.LogError("[PrologueFinaleSetup] Prologue 씬을 열고 실행");
            return;
        }

        Transform old = events.transform.Find("Finale");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform root = new GameObject("Finale").transform;
        root.SetParent(events.transform, false);

        Material on = AssetDatabase.LoadAssetAtPath<Material>(OnMat);
        Material off = AssetDatabase.LoadAssetAtPath<Material>(OffMat);
        Material red = AssetDatabase.LoadAssetAtPath<Material>(RedMat);

        // 복도 형광등 4개
        Transform lampGroup = Child(root, "CorridorLights");
        var lights = new PrologueCeilingLight[LampX.Length];
        for (int i = 0; i < LampX.Length; i++)
        {
            lights[i] = Lamp(lampGroup, "FluorescentLight_" + (i + 1), new Vector3(LampX[i], CeilingY, CorridorZ), Vector3.one, on, off,
                l =>
                {
                    // 아래로 내리꽂는 스포트 — 점광원이면 천장이 하얗게 타고, 원뿔이 넓으면 옆 등과 빛이 겹침
                    l.type = LightType.Spot;
                    l.spotAngle = 95f;
                    l.innerSpotAngle = 50f;
                    l.color = new Color(0.86f, 0.94f, 1f);
                    l.intensity = 16f;
                    l.range = 6.5f;
                    l.shadowStrength = 1f;
                }, true);
        }

        // 비상구 문 위 붉은 등 (컷씬 전엔 꺼짐) — 천장 작은 등에서 복도 쪽(간호사 마지막 자리)을 비춤
        Vector3 finalPos = new Vector3(DoorX - 5f, 0f, CorridorZ);
        PrologueCeilingLight redLight = Lamp(root, "RedAlarmLight", new Vector3(DoorX - 0.5f, CeilingY, CorridorZ), new Vector3(0.22f, 1f, 0.35f), red, off,
            l =>
            {
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.08f, 0.04f);
                l.intensity = 14f;
                l.range = 14f;
                l.spotAngle = 75f;
                l.innerSpotAngle = 35f;
                l.transform.position = new Vector3(DoorX - 0.7f, 3.6f, CorridorZ);
                l.transform.LookAt(finalPos + Vector3.up * 1.7f);
            }, false);

        // 지점
        Transform points = Child(root, "Points");
        Transform stand = Point(points, "Finale_Stand", new Vector3(DoorX - 1.3f, 0f, CorridorZ));
        Transform doorLook = Point(points, "Finale_DoorLook", new Vector3(DoorX, 1.25f, CorridorZ - 0.2f));
        // 처음엔 두 번째 등 바로 밑 — 위에서 내리꽂는 빛에 얼굴·몸 그림자가 짙고, 뒤 맨 끝 등이 윤곽을 비춤 (맨 끝에 세우면 32m라 안개·어둠에 묻혀 안 보였음)
        Transform nurseStart = Point(points, "Finale_NurseStart", new Vector3(LampX[1] + 0.2f, 0f, CorridorZ));
        Transform[] blink =
        {
            Point(points, "Finale_Blink1", new Vector3((LampX[1] + LampX[2]) * 0.5f, 0f, CorridorZ + 0.3f)),
            Point(points, "Finale_Blink2", new Vector3(LampX[2] + 0.2f, 0f, CorridorZ - 0.4f)),
            Point(points, "Finale_Blink3", new Vector3(LampX[3] + 0.2f, 0f, CorridorZ + 0.2f)),
        };
        Transform final = Point(points, "Finale_Final", finalPos);

        // 컷씬
        GameObject cutsceneGo = new GameObject("PrologueFinale");
        cutsceneGo.transform.SetParent(root, false);
        PrologueFinaleCutscene cutscene = cutsceneGo.AddComponent<PrologueFinaleCutscene>();
        AudioSource audio = cutsceneGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        GameObject exitSign = GameObject.Find("ExitSign_Light");
        var so = new SerializedObject(cutscene);
        so.FindProperty("nurse").objectReferenceValue = nurse;
        SetArray(so.FindProperty("corridorLights"), lights);
        so.FindProperty("redLight").objectReferenceValue = redLight;
        SetArray(so.FindProperty("extraLightsOff"), exitSign != null ? new Object[] { exitSign.GetComponent<Light>() } : new Object[0]);
        var hide = new System.Collections.Generic.List<Object>();
        foreach (string name in new[] { "Canvas", "Tutorial" })
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
            {
                hide.Add(go);
            }
        }

        SetArray(so.FindProperty("hideDuringCutscene"), hide.ToArray());
        so.FindProperty("standPoint").objectReferenceValue = stand;
        so.FindProperty("doorLookPoint").objectReferenceValue = doorLook;
        so.FindProperty("nurseStart").objectReferenceValue = nurseStart;
        SetArray(so.FindProperty("blinkPoints"), blink);
        so.FindProperty("finalPoint").objectReferenceValue = final;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 비상구 문: 잠긴 문 안내 → 컷씬 시작
        PrologueFinaleDoor door = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 문 모듈 루트 하나에만 — 자식(메쉬·_Collider·_Glass)도 이름이 같은 접두사라 거기에도 붙으면
            // 레이가 맞은 자식의 (Arm 안 된) 컴포넌트가 먼저 잡혀 컷씬이 안 나왔음
            bool isRoot = t.parent == null || !t.parent.name.StartsWith("HH_Hospital_DoubleDoor_01");
            if (t.name.StartsWith("HH_Hospital_DoubleDoor_01") && Vector3.Distance(t.position, new Vector3(DoorX, 0f, 32f)) < 1f && isRoot)
            {
                foreach (PrologueFinaleDoor stray in t.GetComponentsInChildren<PrologueFinaleDoor>(true))
                {
                    if (stray.transform != t)
                    {
                        Object.DestroyImmediate(stray);
                    }
                }

                foreach (SealedDoorInteract sealedDoor in t.GetComponents<SealedDoorInteract>())
                {
                    Object.DestroyImmediate(sealedDoor);
                }

                door = t.GetComponent<PrologueFinaleDoor>();
                if (door == null)
                {
                    door = t.gameObject.AddComponent<PrologueFinaleDoor>();
                }

                var dso = new SerializedObject(door);
                dso.FindProperty("finale").objectReferenceValue = cutscene;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (door == null)
        {
            Debug.LogError("[PrologueFinaleSetup] 비상구 문(HH_Hospital_DoubleDoor_01, x" + DoorX + " z32)을 못 찾음");
        }

        var dirSo = new SerializedObject(director);
        dirSo.FindProperty("finale").objectReferenceValue = cutscene;
        dirSo.FindProperty("finaleDoor").objectReferenceValue = door;
        dirSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(events.scene);
        Debug.Log("[PrologueFinaleSetup] 완료 — 형광등 " + lights.Length + "개, 붉은 등, 비상구 문 연결");
    }

    private static PrologueCeilingLight Lamp(Transform parent, string name, Vector3 center, Vector3 scale, Material on, Material off,
        System.Action<Light> configure, bool startOn)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);
        GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
        // 천장 모듈처럼 z 180° 뒤집어 아래를 보게. 메쉬 기준점이 모서리(가로 2m × 세로 1m 판의 한쪽 끝)라 중심이 go 위치에 오게
        mesh.transform.localScale = scale;
        mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        mesh.transform.localPosition = new Vector3(1f * scale.x, 0f, 0.5f * scale.z);

        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 아래로
        light.shadows = LightShadows.Hard; // Soft는 그림자 아틀라스가 모자란다는 경고(점·스포트 그림자 수 초과)
        configure(light);

        AudioSource audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.minDistance = 2f;
        audio.maxDistance = 30f;

        PrologueCeilingLight ceiling = go.AddComponent<PrologueCeilingLight>();
        var so = new SerializedObject(ceiling);
        so.FindProperty("lightSource").objectReferenceValue = light;
        SetArray(so.FindProperty("renderers"), mesh.GetComponentsInChildren<Renderer>(true));
        so.FindProperty("onMaterial").objectReferenceValue = on;
        so.FindProperty("offMaterial").objectReferenceValue = off;
        so.FindProperty("startOn").boolValue = startOn;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (Renderer r in mesh.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = startOn ? on : off;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        light.enabled = startOn;
        return ceiling;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    {
        Transform t = Child(parent, name);
        t.position = position;
        return t;
    }

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
