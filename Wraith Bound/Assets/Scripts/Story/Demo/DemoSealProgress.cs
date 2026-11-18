using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 맵 봉인 진행 (런 단위 — 씬을 새로 로드하면 초기화). 출구 대문(DemoSealGate)이 이걸 읽음.
/// 봉인물(부적 DemoTalisman / 예전 봉인 열쇠 DemoSealKey)은 각자 번호(index)가 있고, 아무거나 Total(3)개를 모으면 출구가 열림.
/// Chapter1_FixedDemo는 부적 7장 중 아무 3장 (2026-11-18). 인벤토리 아이템이 아님 — 칸을 차지하지 않음.
/// </summary>
public static class DemoSealProgress
{
    /// <summary>출구를 여는 데 필요한 개수.</summary>
    public const int Total = 3;

    private static readonly HashSet<int> collected = new HashSet<int>();

    /// <summary>번호(index)가 기록될 때 — 기록된 뒤의 Count로 읽으면 됨.</summary>
    public static event Action<int> Collected;

    public static int Count => collected.Count;

    public static bool Has(int index) => collected.Contains(index);

    public static void Collect(int index)
    {
        if (index < 0 || !collected.Add(index))
        {
            return;
        }

        Debug.Log("[Demo] 봉인물 " + (index + 1) + " 기록 (" + Count + "/" + Total + ")");
        Collected?.Invoke(index);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        collected.Clear();
        Collected = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            collected.Clear();
        }
    }
}
