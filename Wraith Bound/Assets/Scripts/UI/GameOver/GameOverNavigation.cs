using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임오버 화면 선택지의 씬 이동. 사망 때 멈춘 시간·오디오를 되돌린 뒤 로드한다.
/// (PlayerDeathDebug의 사망 상태는 Single 씬 로드 시 자체적으로 초기화됨)
/// </summary>
public static class GameOverNavigation
{
    public const string HubSceneName = "Hub";

    /// <summary>지금 챕터를 처음부터 다시 시작.</summary>
    public static void RestartChapter()
    {
        Scene active = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // EnemyScene 같은 테스트 씬은 빌드 세팅에 없어 이름으로 로드가 안 되므로 에디터에선 경로로 다시 로드
        if (!Application.CanStreamedLevelBeLoaded(active.name))
        {
            RestoreTimeAndAudio();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                active.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        Load(active.name);
    }

    public static void GoToHub()
    {
        Load(HubSceneName);
    }

    public static void RestoreTimeAndAudio()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private static void Load(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameOver] 빌드 세팅에 없는 씬입니다: {sceneName}");
            return;
        }

        RestoreTimeAndAudio();
        SceneManager.LoadScene(sceneName);
    }
}
