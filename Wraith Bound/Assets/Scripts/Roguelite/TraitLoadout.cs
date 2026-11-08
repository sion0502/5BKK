using System;
using System.Collections.Generic;

/// <summary>
/// 지금 착용한 기 (허브 기 제단에서 고름). 기본 2칸, 구사일생·시련처럼 '1개만' 기를 끼면 그것 하나만.
/// 씬을 넘어가도 유지되는 런 단위 상태(저장은 안 함 — 게임을 다시 켜면 비어 있음).
/// </summary>
public static class TraitLoadout
{
    public const int DefaultSlots = 2;

    private static readonly List<TraitId> equipped = new List<TraitId>();

    public static event Action Changed;

    public static IReadOnlyList<TraitId> Equipped => equipped;

    public static bool IsEquipped(TraitId id)
    {
        return equipped.Contains(id);
    }

    public static int MaxSlots
    {
        get
        {
            foreach (TraitId id in equipped)
            {
                if (TraitCatalog.Get(id).SingleSlot)
                {
                    return 1;
                }
            }

            return DefaultSlots;
        }
    }

    /// <summary>착용(이미 끼고 있으면 해제). 칸이 차면 가장 먼저 낀 것을 뺌. 해금 안 된 기는 false.</summary>
    public static bool Toggle(TraitId id)
    {
        if (equipped.Remove(id))
        {
            Changed?.Invoke();
            return true;
        }

        TraitDef def = TraitCatalog.Get(id);
        if (def == null || !def.Implemented || !TraitProgress.IsUnlocked(id))
        {
            return false;
        }

        if (def.SingleSlot)
        {
            equipped.Clear();
        }
        else
        {
            equipped.RemoveAll(e => TraitCatalog.Get(e).SingleSlot);
            while (equipped.Count >= DefaultSlots)
            {
                equipped.RemoveAt(0);
            }
        }

        equipped.Add(id);
        Changed?.Invoke();
        return true;
    }

    public static void Clear()
    {
        equipped.Clear();
        Changed?.Invoke();
    }
}
