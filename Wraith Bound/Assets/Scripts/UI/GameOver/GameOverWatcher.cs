using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayerDeathDebug의 사망 상태를 지켜보다가 게임오버 화면을 띄운다.
/// 적/사망 스크립트를 수정하지 않도록 상태값을 읽기만 함. 실행 시 자동 생성되어 씬이 바뀌어도 유지된다.
/// </summary>
public class GameOverWatcher : MonoBehaviour
{
    private static GameOverWatcher instance;

    private float sceneStartTime;
    private bool shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(GameOverWatcher));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameOverWatcher>();
    }

    private void Awake()
    {
        sceneStartTime = Time.time;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        sceneStartTime = Time.time;
        shown = false;
    }

    private void Update()
    {
        if (shown || !PlayerDeathDebug.IsDead)
            return;

        shown = true;
        // Time.time은 일시정지·사망 정지 동안 흐르지 않으므로 실제 플레이한 시간
        GameOverScreen.Show(Time.time - sceneStartTime);
    }
}
