using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 크리처 동작 확인용 (Play 모드): AI를 뗀 복제본을 만들어 Animator 상태를 정규화 시간별로 고정하고
/// 지정 방향(옆/정면)에서 렌더해 콘택트 시트 한 장으로 저장. 추가 컴포넌트(관절 꺾기 등)도 켤 수 있음.
/// </summary>
public static class CreaturePoseSheet
{
    public static string Render(string prefabPath, string state, int frames, string outPng, bool side = true, float distance = 4f)
    {
        if (!Application.isPlaying)
            return "Play 모드에서만";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var holder = new GameObject("PoseSheetHolder");
        holder.SetActive(false);
        GameObject go = Object.Instantiate(prefab, new Vector3(900f, 0f, 900f), Quaternion.identity, holder.transform);
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(mb);
        foreach (var ag in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            Object.DestroyImmediate(ag);
        go.transform.SetParent(null, true);
        go.SetActive(true);
        Object.DestroyImmediate(holder);

        Animator anim = go.GetComponent<Animator>();
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.applyRootMotion = false;
        anim.speed = 0f;

        var camGo = new GameObject("PoseSheetCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.35f, 0.38f);
        cam.fieldOfView = 40f;
        var lightGo = new GameObject("PoseSheetLight");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightGo.transform.rotation = Quaternion.Euler(25f, side ? -60f : 160f, 0f);

        string dir = Path.Combine(Path.GetDirectoryName(outPng), Path.GetFileNameWithoutExtension(outPng) + "_frames");
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(dir, "*.png"))
            File.Delete(f);
        // 스킨드 메쉬는 프레임당 한 번만 변형되므로, 한 프레임에 여러 포즈를 찍으려면 포즈마다 메쉬를 구워 대신 그림
        SkinnedMeshRenderer[] skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        var proxies = new (SkinnedMeshRenderer skin, MeshFilter filter, Mesh mesh)[skins.Length];
        for (int s = 0; s < skins.Length; s++)
        {
            var proxy = new GameObject("PoseProxy");
            var filter = proxy.AddComponent<MeshFilter>();
            proxy.AddComponent<MeshRenderer>().sharedMaterials = skins[s].sharedMaterials;
            proxies[s] = (skins[s], filter, new Mesh());
            skins[s].enabled = false;
        }

        var rt = new RenderTexture(320, 360, 24);
        var tex = new Texture2D(320, 360, TextureFormat.RGB24, false);
        for (int i = 0; i < frames; i++)
        {
            anim.Play(state, 0, (float)i / frames);
            anim.Update(0f);
            foreach (var p in proxies)
            {
                // 스케일 없이 굽고 대리 오브젝트에 원래 스케일 — useScale=true는 비균등 스케일(탈춤 1.6/1.85/1.8)을 제대로 반영 못 함
                p.skin.BakeMesh(p.mesh, false);
                p.filter.sharedMesh = p.mesh;
                p.filter.transform.SetPositionAndRotation(p.skin.transform.position, p.skin.transform.rotation);
                p.filter.transform.localScale = p.skin.transform.lossyScale;
            }
            Vector3 center = go.transform.position + Vector3.up * 1.4f;
            cam.transform.position = center + (side ? Vector3.right : Vector3.forward) * distance;
            cam.transform.LookAt(center);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 320, 360), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "p" + i.ToString("00") + ".png"), tex.EncodeToPNG());
        }

        foreach (var p in proxies)
        {
            Object.DestroyImmediate(p.filter.gameObject);
            Object.DestroyImmediate(p.mesh);
        }
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        Object.DestroyImmediate(rt);
        return CreatureJumpscarePreview.BuildSheet(dir, outPng);
    }
}
