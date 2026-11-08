using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 문 통과 확인용 (Play 모드). 빈 곳에 실제 문 모듈(Interior_Wall_Door_01, 문 높이 2.23m)을 세우고
/// AI를 뗀 크리처를 그 문으로 걸어 통과시키며 비스듬한 카메라로 캡처 + 접기 정도·머리 높이 기록(log.txt).
/// </summary>
public static class CreatureDoorTest
{
    static readonly Vector3 Origin = new Vector3(4000f, 0f, 4000f);
    const float DoorZ = 1.98f; // 문 모듈 안 출입구 중앙

    public static string Run(string prefabPath, int state, float speed, string folder)
    {
        if (!Application.isPlaying)
            return "Play 모드에서만";
        EnsureDoor();
        Directory.CreateDirectory(folder);
        foreach (string f in Directory.GetFiles(folder))
            File.Delete(f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var holder = new GameObject("tmp");
        holder.SetActive(false);
        GameObject go = Object.Instantiate(prefab, Origin + new Vector3(-4f, 0f, DoorZ), Quaternion.Euler(0f, 90f, 0f), holder.transform);
        go.name = "DoorWalker";
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is CreatureFootGrounding || mb is CreatureJointSnap || mb is CreatureChaseStretch || mb is CreatureEyeGlow
                  || mb is CreatureDoubleBody || mb is CreatureDoorSquash))
                Object.DestroyImmediate(mb);
        foreach (var ag in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            Object.DestroyImmediate(ag);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        go.transform.SetParent(null, true);
        go.SetActive(true);
        Object.Destroy(holder);

        Animator anim = go.GetComponent<Animator>();
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.applyRootMotion = false;
        anim.SetInteger("State", state);

        Camera cam = GameObject.Find("DoorTestCam").GetComponent<Camera>();
        var rt = new RenderTexture(640, 400, 24);
        var tex = new Texture2D(640, 400, TextureFormat.RGB24, false);
        float t0 = Time.realtimeSinceStartup, last = 0f, next = 0.3f;
        int shot = 0;
        CreatureDoorSquash squash = go.GetComponent<CreatureDoorSquash>();
        Transform head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null)
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                if (t.name.EndsWith("Head")) head = t;

        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            float t = Time.realtimeSinceStartup - t0;
            float dt = t - last;
            last = t;
            if (!Application.isPlaying || go == null || shot >= 24)
            {
                EditorApplication.update -= cb;
                if (go != null) Object.Destroy(go);
                return;
            }
            Vector3 p = go.transform.position;
            p.x += speed * dt;
            p.y = Origin.y;
            go.transform.position = p;
            if (t < next) return;
            next += 0.22f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 640, 400), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(folder, "c" + (shot++).ToString("00") + ".png"), tex.EncodeToPNG());
            File.AppendAllText(Path.Combine(folder, "log.txt"),
                string.Format("x={0:F2} fold={1:F2} head={2:F2}\n", p.x - Origin.x, squash != null ? squash.Amount : 0f, head != null ? head.position.y - Origin.y : 0f));
        };
        EditorApplication.update += cb;
        return "walking " + prefab.name;
    }

    static void EnsureDoor()
    {
        if (GameObject.Find("DoorTestRoot") != null)
            return;
        var root = new GameObject("DoorTestRoot");
        root.transform.position = Origin;
        GameObject door = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Object/Chapter1/Interior_Wall_Door_01.prefab"), Origin, Quaternion.identity, root.transform);
        foreach (Transform t in door.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Interior_Door_01"))
                t.gameObject.SetActive(false); // 문짝은 열린 셈
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.SetParent(root.transform);
        floor.transform.position = Origin + new Vector3(0f, -0.5f, 2f);
        floor.transform.localScale = new Vector3(20f, 1f, 20f);
        var lightGo = new GameObject("DoorTestLight");
        lightGo.transform.SetParent(root.transform);
        lightGo.transform.rotation = Quaternion.Euler(40f, 200f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        var camGo = new GameObject("DoorTestCam");
        camGo.transform.SetParent(root.transform);
        camGo.transform.position = Origin + new Vector3(4.5f, 2.2f, DoorZ + 6.5f);
        camGo.transform.LookAt(Origin + new Vector3(0f, 1.6f, DoorZ));
        Camera cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.fieldOfView = 45f;
        Physics.SyncTransforms();
    }
}
