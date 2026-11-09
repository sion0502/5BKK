using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연 맵 봉인 열쇠 진행 (런 단위 — 씬을 새로 로드하면 초기화). 출구 봉인 구슬(연출 ⑥)이 이걸 읽음.
/// 열쇠는 인벤토리 아이템이 아니라 이야기용 수집물(DemoSealKey) — 칸을 차지하지 않음.
/// </summary>
public static class DemoSealProgress
{
    public const int Total = 3;

    private static bool[] collected = new bool[Total];

    public static event Action<int> Collected;

    public static int Count
    {
        get
        {
            int n = 0;
            foreach (bool b in collected)
            {
                if (b)
                {
                    n++;
                }
            }

            return n;
        }
    }

    public static bool Has(int index) => index >= 0 && index < Total && collected[index];

    public static void Collect(int index)
    {
        if (index < 0 || index >= Total || collected[index])
        {
            return;
        }

        collected[index] = true;
        Debug.Log("[Demo] 봉인 열쇠 " + (index + 1) + " 획득 (" + Count + "/" + Total + ")");
        Collected?.Invoke(index);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        collected = new bool[Total];
        Collected = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            collected = new bool[Total];
        }
    }
}
