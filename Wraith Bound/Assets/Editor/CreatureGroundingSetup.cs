using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 크리처 프리팹에 CreatureFootGrounding을 붙이고 발바닥 정점을 구워 넣음.
/// 프리팹 포즈로 메쉬를 구운 뒤, 발 뼈(+하위 뼈)에 가중치가 주로 걸린 정점 중 가장 낮은 것들 →
/// 발끝·뒤꿈치·양옆 끝과 최저점을 골라 그 뼈의 로컬 좌표로 저장. 모델·리그를 바꾸면 다시 실행.
/// </summary>
public static class CreatureGroundingSetup
{
    const float SoleBand = 0.02f;   // 최저점에서 이 높이(m) 안의 정점을 발바닥으로 봄
    const int MaxPointsPerFoot = 8;

    /// <summary>Xenosaurid는 원본 애니메이션 발 높이가 정확해서(발톱만 2cm 파고듦) 제외 — 붙이면 달리기에서 오히려 뜸.</summary>
    public static readonly string[] GroundedPrefabs =
    {
        "Assets/Prefab/Enemy/Monster/NurseGhost.prefab",
        "Assets/Prefab/Enemy/Monster/MaskGhost.prefab",
        // 같은 크리처의 다른 경로 사본 (Prologue·EnemyScene이 씀)
        "Assets/Model/Enemy/MaskGhost/Prefab/NurseGhost.prefab",
        "Assets/Model/Enemy/MaskGhost/Prefab/MaskGhost.prefab",
    };

    [MenuItem("Tools/Creatures/Setup Foot Grounding")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (string path in GroundedPrefabs)
            sb.AppendLine(Setup(path));
        Debug.Log("[GroundingSetup]\n" + sb);
    }

    public static string Setup(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator anim = root.GetComponentInChildren<Animator>(true);
            Transform hips = FindHips(root.transform, anim);
            List<Transform> feet = FindFeet(root.transform, anim);
            if (hips == null || feet.Count < 2)
                return prefabPath + " — 골반/발 뼈를 못 찾음 (hips=" + hips + ", feet=" + feet.Count + ")";

            var points = new List<CreatureFootGrounding.SolePoint>();
            var report = new StringBuilder();
            foreach (Transform foot in feet)
            {
                var chain = new HashSet<Transform>(foot.GetComponentsInChildren<Transform>(true));
                List<CreatureFootGrounding.SolePoint> sole = CollectSole(root, chain, out float minY);
                points.AddRange(sole);
                report.Append(" " + foot.name + ":" + sole.Count + "pt(minY " + minY.ToString("F3") + ")");
            }

            if (points.Count == 0)
                return prefabPath + " — 발 정점 없음";

            CreatureFootGrounding grounding = root.GetComponent<CreatureFootGrounding>();
            if (grounding == null)
                grounding = root.AddComponent<CreatureFootGrounding>();
            grounding.Configure(hips, points.ToArray());
            EditorUtility.SetDirty(grounding);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return prefabPath + " hips=" + hips.name + report;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindHips(Transform root, Animator anim)
    {
        if (anim != null && anim.isHuman)
            return anim.GetBoneTransform(HumanBodyBones.Hips);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.EndsWith("pelvis") || n.EndsWith("hips"))
                return t;
        }
        return null;
    }

    public static List<Transform> FindFeet(Transform root, Animator anim)
    {
        var feet = new List<Transform>();
        if (anim != null && anim.isHuman)
        {
            Transform l = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform r = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (l != null) feet.Add(l);
            if (r != null) feet.Add(r);
            return feet;
        }

        // Generic: 이름에 Foot이 들어간 뼈 중 위쪽에 다른 Foot이 없는 것 (Xenosaurid_ L Foot / R Foot)
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.ToLowerInvariant().Contains("foot"))
                continue;
            bool nested = false;
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name.ToLowerInvariant().Contains("foot")) { nested = true; break; }
            if (!nested)
                feet.Add(t);
        }
        return feet;
    }

    static List<CreatureFootGrounding.SolePoint> CollectSole(GameObject root, HashSet<Transform> chain, out float minY)
    {
        var candidates = new List<(Vector3 world, Transform bone)>();
        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh shared = smr.sharedMesh;
            if (shared == null)
                continue;
            Transform[] bones = smr.bones;
            BoneWeight[] weights = shared.boneWeights;
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            Vector3[] verts = baked.vertices;
            Object.DestroyImmediate(baked);
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length && i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                if (w.weight0 < 0.5f || w.boneIndex0 < 0 || w.boneIndex0 >= bones.Length)
                    continue;
                Transform bone = bones[w.boneIndex0];
                if (bone == null || !chain.Contains(bone))
                    continue;
                candidates.Add((m.MultiplyPoint3x4(verts[i]), bone));
            }
        }

        var result = new List<CreatureFootGrounding.SolePoint>();
        minY = float.MaxValue;
        foreach (var c in candidates)
            minY = Mathf.Min(minY, c.world.y);
        if (candidates.Count == 0)
            return result;

        float lowY = minY;
        var band = candidates.FindAll(c => c.world.y <= lowY + SoleBand);
        // 최저점 + 발바닥 영역의 앞뒤·좌우 끝 → 어느 쪽으로 기울어도 닿는 점을 잡음
        var picks = new List<(Vector3 world, Transform bone)>
        {
            band.Find(c => c.world.y == lowY),
            MinBy(band, c => c.world.x), MinBy(band, c => -c.world.x),
            MinBy(band, c => c.world.z), MinBy(band, c => -c.world.z),
            MinBy(band, c => c.world.x + c.world.z), MinBy(band, c => -c.world.x - c.world.z),
            MinBy(band, c => c.world.x - c.world.z),
        };
        var seen = new HashSet<Vector3>();
        foreach (var p in picks)
        {
            if (p.bone == null || !seen.Add(p.world) || result.Count >= MaxPointsPerFoot)
                continue;
            result.Add(new CreatureFootGrounding.SolePoint { bone = p.bone, local = p.bone.InverseTransformPoint(p.world) });
        }
        return result;
    }

    static (Vector3 world, Transform bone) MinBy(List<(Vector3 world, Transform bone)> list, System.Func<(Vector3 world, Transform bone), float> key)
    {
        var best = list[0];
        float bestKey = key(best);
        for (int i = 1; i < list.Count; i++)
        {
            float k = key(list[i]);
            if (k < bestKey) { bestKey = k; best = list[i]; }
        }
        return best;
    }
}
