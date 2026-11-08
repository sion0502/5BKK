using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 크리처 프리팹(사본 포함)에 CreatureDoorSquash(문 지날 때 몸 접기)를 붙임. Tools > Creatures > Setup Door Squash.
/// 귀신 2종(휴머노이드)은 기본값(허리 숙이고 무릎 굽힘), Xenosaurid(Generic, 원래 구부정)는 척추·긴 목을 숙임.
/// 무릎을 굽히면 골반이 크게 내려가야 해서 발 붙이기의 내림 한도를 1.2m로. 매번 새로 붙여 코드 기본값이 기준.
/// </summary>
public static class CreatureDoorSquashSetup
{
    static readonly string[] Humanoids =
    {
        "Assets/Prefab/Enemy/Monster/NurseGhost.prefab",
        "Assets/Model/Enemy/MaskGhost/Prefab/NurseGhost.prefab",
        "Assets/Prefab/Enemy/Monster/MaskGhost.prefab",
        "Assets/Model/Enemy/MaskGhost/Prefab/MaskGhost.prefab",
    };

    static readonly string[] Xenos =
    {
        "Assets/Prefab/Enemy/Monster/Xenosaurid.prefab",
        "Assets/Model/Enemy/Xenosaruid/Xenosaurid/Prefab/Xenosaurid.prefab",
        "Assets/Model/Enemy/MaskGhost/Prefab/Xenosaurid.prefab",
    };

    static CreatureDoorSquash.FoldBone Named(string name, float pitch)
    {
        return new CreatureDoorSquash.FoldBone { human = HumanBodyBones.LastBone, boneName = name, pitch = pitch };
    }

    [MenuItem("Tools/Creatures/Setup Door Squash")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (string path in Humanoids)
        {
            sb.AppendLine(Setup(path, null));
        }

        // 원래 구부정한 체형이라 척추·목만으론 부족 → 골반 숙임 + 역관절 다리(허벅지·정강이·발목 위 관절)도 굽힘
        var xenoBones = new[]
        {
            Named("Xenosaurid_ Pelvis", 12f),
            Named("Xenosaurid_ Spine", 25f),
            Named("Xenosaurid_ Spine1", 25f),
            Named("Xenosaurid_ Neck", 30f),
            Named("Xenosaurid_ Neck1", 30f),
            Named("Xenosaurid_ Neck2", 25f),
            Named("Xenosaurid_ Head", -40f), // 고개는 들어 앞을 봄
            Named("Xenosaurid_ L Thigh", -35f),
            Named("Xenosaurid_ R Thigh", -35f),
            Named("Xenosaurid_ L Calf", 60f),
            Named("Xenosaurid_ R Calf", 60f),
            Named("Xenosaurid_ L HorseLink", -40f),
            Named("Xenosaurid_ R HorseLink", -40f),
        };
        foreach (string path in Xenos)
        {
            sb.AppendLine(Setup(path, sq => sq.Configure(xenoBones,
                new[] { "Xenosaurid_ Head", "Xenosaurid_ Neck2", "Xenosaurid_ Neck1", "Xenosaurid_ Spine1" }, 0.35f,
                new[] { "Xenosaurid_ L Foot", "Xenosaurid_ R Foot", "Xenosaurid_ L Toe1", "Xenosaurid_ R Toe1" })));
        }
        Debug.Log("[DoorSquashSetup]\n" + sb);
    }

    static string Setup(string path, System.Action<CreatureDoorSquash> configure)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (CreatureDoorSquash old in root.GetComponents<CreatureDoorSquash>())
                Object.DestroyImmediate(old, true);
            CreatureDoorSquash squash = root.AddComponent<CreatureDoorSquash>();
            configure?.Invoke(squash);

            CreatureFootGrounding grounding = root.GetComponent<CreatureFootGrounding>();
            if (grounding != null)
                grounding.SetLimits(1.2f, root.GetComponent<CreatureChaseStretch>() != null ? 0.9f : 0.35f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return path;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
