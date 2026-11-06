using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 컷씬 하나를 재생·종료합니다.
/// - PlayableDirector(Timeline)를 재생하고, 끝나면 onFinished 호출 후 nextSceneName이 있으면 그 씬을 로드
/// - ESC로 건너뛰기: 검은 화면으로 페이드 → 자막 지움 → 종료 처리
/// - 재생 중 커서 숨김
/// </summary>
public class CutsceneDirector : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool playOnStart = true;

    [Header("Finish")]
    [Tooltip("끝나면 로드할 씬 (빌드 세팅에 있어야 함). 비우면 씬 전환 없음")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    [Header("Skip")]
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private KeyCode skipKey = KeyCode.Escape;
    [SerializeField] private float skipFadeDuration = 0.5f;
    [Tooltip("건너뛰기·종료 때 쓰는 검은 화면. 비우면 코드로 생성")]
    [SerializeField] private CutsceneBlackScreen blackScreen;
    [SerializeField] private bool showSkipHint = true;

    private bool playing;
    private bool finishing;

    public bool IsPlaying => playing;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (director == null || playing)
        {
            return;
        }

        playing = true;
        finishing = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        director.extrapolationMode = DirectorWrapMode.None;
        director.stopped -= OnDirectorStopped;
        director.stopped += OnDirectorStopped;
        director.Play();

        if (allowSkip && showSkipHint)
        {
            CutsceneSkipHint.Show(skipKey);
        }
    }

    private void Update()
    {
        if (!playing || finishing || !allowSkip)
        {
            return;
        }

        if (Input.GetKeyDown(skipKey))
        {
            StartCoroutine(SkipRoutine());
        }
    }

    private IEnumerator SkipRoutine()
    {
        finishing = true;
        CutsceneSkipHint.Hide();
        SubtitleUI.Clear();

        CutsceneBlackScreen fade = GetBlackScreen();
        director.Pause();
        yield return fade.FadeTo(1f, skipFadeDuration);

        director.stopped -= OnDirectorStopped;
        director.Stop();
        Finish();
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (finishing)
        {
            return;
        }

        finishing = true;
        Finish();
    }

    private void Finish()
    {
        playing = false;
        director.stopped -= OnDirectorStopped;
        CutsceneSkipHint.Hide();
        SubtitleUI.Clear();

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            // 씬 전환 직전 한 프레임이라도 컷씬 화면이 다시 보이지 않도록 검게 덮음
            GetBlackScreen().Alpha = 1f;
        }

        onFinished.Invoke();

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private CutsceneBlackScreen GetBlackScreen()
    {
        if (blackScreen == null)
        {
            blackScreen = CutsceneBlackScreen.CreateRuntime();
        }

        return blackScreen;
    }

    private void OnDestroy()
    {
        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
        }
    }
}
