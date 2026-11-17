using UnityEditor;
using UnityEngine;

/// <summary>기 해금 진행 테스트용 메뉴 (PlayerPrefs라 에디터에서도 계속 남음).</summary>
public static class TraitDebugMenu
{
    [MenuItem("Tools/Roguelite/Reset Trait Progress")]
    public static void ResetProgress()
    {
        TraitProgress.ResetAll();
        Debug.Log("[Trait] 해금 진행 초기화");
    }

    [MenuItem("Tools/Roguelite/Unlock All Traits (Test)")]
    public static void UnlockAll()
    {
        foreach (TraitDef def in TraitCatalog.All)
        {
            if (def.Implemented)
            {
                TraitProgress.Unlock(def);
            }
        }

        Debug.Log("[Trait] 시연 9종 전부 해금 (테스트)");
    }
}
