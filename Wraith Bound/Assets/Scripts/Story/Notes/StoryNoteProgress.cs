using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 읽은 스토리 쪽지 번호 — 죽어도·씬을 넘어도 유지(PlayerPrefs). 기획서 '스토리 아이템은 유지되어 허브에서 스토리를 유추'.
/// 챕터는 이미 읽은 쪽지를 다시 배치하지 않고(StoryNoteSpawner), 허브 게시판(HubNoteArchive)은 읽은 쪽지만 걸어 둠.
/// 읽는 순간 바로 저장(죽거나 중간에 그만둬도 남음) — 앞사람이 읽은 쪽지가 허브 게시판에 쌓여 다음 사람이 봄(2026-11-19).
/// 운영자 초기화 F5(TraitRuntime)가 기 해금과 함께 이것도 지움(사용자 결정). 에디터에선 Tools > Story > Reset Story Notes로 쪽지만.
/// </summary>
public static class StoryNoteProgress
{
    private const string Key = "StoryNotes.Collected.v1";

    private static HashSet<int> collected;

    public static event Action<int> Collected;

    /// <summary>읽음·초기화 모두 — 허브 게시판 다시 그리기용.</summary>
    public static event Action Changed;

    public static int Count => Set.Count;

    public static bool Has(int id) => Set.Contains(id);

    private static HashSet<int> Set
    {
        get
        {
            if (collected == null)
            {
                collected = new HashSet<int>();
                foreach (string part in PlayerPrefs.GetString(Key, string.Empty).Split(','))
                {
                    if (int.TryParse(part, out int id))
                    {
                        collected.Add(id);
                    }
                }
            }

            return collected;
        }
    }

    public static void Collect(int id)
    {
        if (!Set.Add(id))
        {
            return;
        }

        Save();
        Debug.Log("[Story] 쪽지 " + (id + 1) + " 읽음 (" + Count + "/" + StoryNoteCatalog.Count + ")");
        Collected?.Invoke(id);
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        Set.Clear();
        Save();
        Debug.Log("[Story] 쪽지 수집 기록 초기화");
        Changed?.Invoke();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(Key, string.Join(",", Set));
        PlayerPrefs.Save();
    }
}
