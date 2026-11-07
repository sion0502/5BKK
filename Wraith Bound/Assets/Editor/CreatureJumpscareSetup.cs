using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>크리처 프리팹(경로가 다른 사본 포함)에 전용 점프스케어 컴포넌트를 붙임. Tools > Creatures > Setup Jumpscares.</summary>
public static class CreatureJumpscareSetup
{
    static readonly (string path, Type type)[] Targets =
    {
        ("Assets/Prefab/Enemy/Monster/NurseGhost.prefab", typeof(NurseJumpscare)),
        ("Assets/Model/Enemy/MaskGhost/Prefab/NurseGhost.prefab", typeof(NurseJumpscare)),
        ("Assets/Prefab/Enemy/Monster/MaskGhost.prefab", typeof(MaskGhostJumpscare)),
        ("Assets/Model/Enemy/MaskGhost/Prefab/MaskGhost.prefab", typeof(MaskGhostJumpscare)),
        ("Assets/Prefab/Enemy/Monster/Xenosaurid.prefab", typeof(XenosauridJumpscare)),
        ("Assets/Model/Enemy/Xenosaruid/Xenosaurid/Prefab/Xenosaurid.prefab", typeof(XenosauridJumpscare)),
        ("Assets/Model/Enemy/MaskGhost/Prefab/Xenosaurid.prefab", typeof(XenosauridJumpscare)),
    };

    [MenuItem("Tools/Creatures/Setup Jumpscares")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (var (path, type) in Targets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root); // 지운 연출 스크립트 흔적
                // 매번 새로 붙임 → 연출 수치는 코드 기본값이 기준 (프리팹에 예전 값이 남아 코드 수정이 안 먹던 문제)
                foreach (CreatureJumpscare other in root.GetComponents<CreatureJumpscare>())
                    UnityEngine.Object.DestroyImmediate(other);
                if (type != null)
                    root.AddComponent(type);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                sb.AppendLine(path + " ← " + (type != null ? type.Name : "기본 연출"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log("[JumpscareSetup]\n" + sb);
    }
}
