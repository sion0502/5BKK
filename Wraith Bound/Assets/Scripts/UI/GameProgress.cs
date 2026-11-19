using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스토리 진행 기록 (PlayerPrefs). 메인 메뉴가 "첫 플레이"인지 "이어서 하기"가 가능한지 판단할 때 씁니다.
/// - 프롤로그를 끝내고 허브(Hub) 씬에 한 번이라도 도착하면 진행 기록이 생김
/// - 새로운 게임을 시작하면 지움 (허브 진행·튜토리얼 본 기록도 같이)
/// 에디터 메뉴 Tools > Story > Reset Game Progress 로 지우면 첫 플레이 메뉴를 다시 볼 수 있습니다.
/// </summary>
public static class GameProgress
{
    private const string HubReachedKey = "WB_Progress_HubReached";
    private const string HubSceneName = "Hub";

    public static bool HasSave => PlayerPrefs.GetInt(HubReachedKey, 0) == 1;

    public static void MarkHubReached()
    {
        if (HasSave)
        {
            return;
        }

        PlayerPrefs.SetInt(HubReachedKey, 1);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(HubReachedKey);
        PlayerPrefs.Save();
        HubStoryProgress.Clear();
        TutorialManager.ClearSavedProgress(); // 새 게임이면 프롤로그 튜토리얼부터 다시
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 메인 메뉴가 배경으로 허브를 Additive 로드하는 건 도착으로 치지 않음
        if (mode == LoadSceneMode.Single && scene.name == HubSceneName)
        {
            MarkHubReached();
        }
    }
}
