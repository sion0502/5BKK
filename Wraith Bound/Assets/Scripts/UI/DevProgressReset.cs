using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 운영자 키 F5 — 저장된 진행 상황을 전부 초기화 (부스 시연을 처음 상태로 되돌릴 때).
///  · 기 해금 진행·착용 (TraitProgress / TraitLoadout)
///  · 스토리 진행: 허브 도착 기록('이어서 하기'), 허브 첫 진행 단계·독백 (GameProgress / HubStoryProgress)
///  · 튜토리얼 완료 기록
///  · 스토리 쪽지 수집, 부적 설명 팝업 본 기록 (sion 브랜치의 F5 초기화 항목 통합)
///  설정(배경음·환경음·마우스 감도·전체 화면)은 지우지 않음.
/// 메인 메뉴에서 누르면 메뉴를 다시 불러와 '첫 플레이' 화면으로 바뀜. 플레이 중이면 지금 판은 그대로 이어지고 저장만 지워짐.
/// 빌드에서도 동작, 씬을 넘어가도 유지.
/// </summary>
public class DevProgressReset : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private static DevProgressReset instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("DevProgressReset");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevProgressReset>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ResetAll();
        }
    }

    public static void ResetAll()
    {
        TraitProgress.ResetAll();
        GameProgress.Clear();
        TutorialManager.ClearSavedProgress();
        StoryNoteProgress.ResetAll();
        DemoTalismanTip.Reset(); // 다음 관람객에게 부적 설명 팝업 다시
        PlayerPrefs.Save();
        Debug.Log("[DevProgressReset] 진행 상황 전체 초기화 (기·쪽지·스토리·튜토리얼)");

        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
        {
            SceneManager.LoadScene(MainMenuSceneName); // '이어서 하기' 대신 첫 플레이 메뉴로
            return;
        }

        ScreenMessageUI.Show("진행 상황을 모두 초기화했습니다 (운영자)", 2.5f);
    }
}
