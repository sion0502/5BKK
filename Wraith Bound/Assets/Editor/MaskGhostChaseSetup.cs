using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 탈춤귀신 추격 연출 부착 (Tools > Creatures > Setup MaskGhost Chase).
/// 관절 꺾기(CreatureJointSnap)·추격 시 몸 늘어남(CreatureChaseStretch)·붉은 안광(CreatureEyeGlow)·겹친 몸(CreatureDoubleBody)을 두 프리팹 사본에 붙이고,
/// 다리가 길어져도 발이 바닥에 닿도록 발 붙이기 올림 한도를 키움. 추격 클립(TalChase)은 MaskGhost.controller의 Chase 상태.
/// 매번 컴포넌트를 새로 붙여 코드 기본값이 기준.
/// </summary>
public static class MaskGhostChaseSetup
{
    static readonly string[] Prefabs =
    {
        "Assets/Prefab/Enemy/Monster/MaskGhost.prefab",
        "Assets/Model/Enemy/MaskGhost/Prefab/MaskGhost.prefab",
    };

    [MenuItem("Tools/Creatures/Setup MaskGhost Chase")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Replace<CreatureJointSnap>(root);
                Replace<CreatureChaseStretch>(root);
                Replace<CreatureEyeGlow>(root);
                Replace<CreatureDoubleBody>(root);
                CreatureFootGrounding grounding = root.GetComponent<CreatureFootGrounding>();
                if (grounding != null)
                    grounding.SetLimits(1.2f, 0.9f); // 내림 1.2: 문 지날 때 무릎 굽힘(CreatureDoorSquash)
                PrefabUtility.SaveAsPrefabAsset(root, path);
                sb.AppendLine(path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log("[MaskGhostChaseSetup]\n" + sb);
    }

    static void Replace<T>(GameObject root) where T : Component
    {
        foreach (T old in root.GetComponents<T>())
            Object.DestroyImmediate(old, true);
        root.AddComponent<T>();
    }
}
