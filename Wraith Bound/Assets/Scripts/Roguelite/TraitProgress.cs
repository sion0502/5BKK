using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기 해금 진행 — 카운터(숨기 횟수·사망 수 등)와 해금 목록을 PlayerPrefs에 저장(죽거나 게임을 다시 켜도 유지).
/// 카운터가 기의 목표치에 닿으면 해금 + 화면 안내. 부스 반복 시연용 초기화: ResetAll (운영자 키는 TraitRuntime).
/// </summary>
public static class TraitProgress
{
    private const string PrefsKey = "Trait.Progress.v1";

    [Serializable]
    private class Entry
    {
        public string key;
        public int value;
    }

    [Serializable]
    private class Data
    {
        public List<Entry> counters = new List<Entry>();
        public List<string> unlocked = new List<string>();
    }

    private static Data data;

    public static event Action<TraitDef> Unlocked;

    private static Data D
    {
        get
        {
            if (data == null)
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json) ?? new Data();
            }

            return data;
        }
    }

    public static bool IsUnlocked(TraitId id)
    {
        return D.unlocked.Contains(id.ToString());
    }

    public static int Get(string key)
    {
        foreach (Entry e in D.counters)
        {
            if (e.key == key)
            {
                return e.value;
            }
        }

        return 0;
    }

    /// <summary>카운터 증가 → 목표치에 닿은 기가 있으면 해금.</summary>
    public static void Add(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        Entry entry = D.counters.Find(e => e.key == key);
        if (entry == null)
        {
            entry = new Entry { key = key };
            D.counters.Add(entry);
        }

        entry.value += amount;
        Debug.Log("[Trait] " + key + " = " + entry.value);
        foreach (TraitDef def in TraitCatalog.All)
        {
            if (def.Implemented && def.Counter == key && entry.value >= def.Goal && !IsUnlocked(def.Id))
            {
                Unlock(def);
            }
        }

        Save();
    }

    public static void Unlock(TraitDef def)
    {
        if (def == null || IsUnlocked(def.Id))
        {
            return;
        }

        D.unlocked.Add(def.Id.ToString());
        Save();
        Debug.Log("[Trait] 해금: " + def.Name);
        ScreenMessageUI.Show("새로운 기 해금 — " + def.Name, 3f);
        Unlocked?.Invoke(def);
    }

    public static void ResetAll()
    {
        data = new Data();
        Save();
        TraitLoadout.Clear();
        Debug.Log("[Trait] 진행 초기화");
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(D));
        PlayerPrefs.Save();
    }
}
