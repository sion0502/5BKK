using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 진행 관리.
///  • 기본 진행: 우상단 안내, 직접 해봐야 다음 단계로 (steps 순서대로)
///  • 상황형 팁: 조건이 처음 충족될 때 중앙 팝업 + 시간 정지 (tips, 각 1회)
///  • E 길게 누르기: 튜토리얼 전체 건너뛰기
/// SceneStarter가 있으면 착지 연출이 끝난 뒤 시작합니다. Tutorial 프리팹을 씬에 놓기만 하면 동작합니다.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const string CompletedPrefsKey = "Tutorial.Completed";

    [Header("Mode")]
    [Tooltip("켜면 매번 표시(시연용). 끄면 한 번 완료·건너뛰기한 뒤로는 표시하지 않습니다.")]
    [SerializeField] private bool showEveryTime = true;
    [Tooltip("SceneStarter가 없는 씬(테스트 씬 등)에서 시작까지 기다리는 시간")]
    [SerializeField] private float startDelayWithoutStarter = 1f;

    [Header("Steps")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();
    [SerializeField] private List<TutorialTip> tips = new List<TutorialTip>();

    [Header("Timing")]
    [SerializeField] private float stepCompleteDelay = 0.8f;
    [SerializeField] private float finishMessageDuration = 2.5f;
    [SerializeField] private string finishMessage = "튜토리얼 완료\n어둠 속에서 살아남으세요";

    [Header("Skip")]
    [SerializeField] private float skipHoldDuration = 2f;

    [Header("References")]
    [SerializeField] private TutorialPromptUI promptUI;
    [SerializeField] private TutorialTipPopupUI tipPopup;
    [SerializeField] private TutorialHighlight highlight;

    private TutorialConditions conditions;
    private TutorialInputBlocker inputBlocker;
    private Transform playerTransform;
    private readonly HashSet<int> shownTips = new HashSet<int>();

    private int stepIndex = -1;
    private bool stepsRunning;
    private bool isAdvancing;
    private bool tutorialActive;
    private float skipHoldTimer;

    /// <summary>인스펙터 우클릭 메뉴 / 프리팹 빌더에서 기본 문구로 되돌릴 때 사용.</summary>
    [ContextMenu("기본 단계·팁 문구로 초기화")]
    public void ResetToDefaults()
    {
        steps = TutorialDefaults.CreateSteps();
        tips = TutorialDefaults.CreateTips();
    }

    [ContextMenu("완료 기록 지우기 (PlayerPrefs)")]
    private void ClearCompletedRecord()
    {
        PlayerPrefs.DeleteKey(CompletedPrefsKey);
    }

    void Start()
    {
        if (!showEveryTime && PlayerPrefs.GetInt(CompletedPrefsKey, 0) == 1)
        {
            gameObject.SetActive(false);
            return;
        }

        if (steps.Count == 0)
        {
            steps = TutorialDefaults.CreateSteps();
        }

        if (tips.Count == 0)
        {
            tips = TutorialDefaults.CreateTips();
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[Tutorial] Player 태그 오브젝트가 없어 튜토리얼을 시작하지 않습니다.");
            return;
        }

        playerTransform = player.transform;
        conditions = new TutorialConditions(player);
        inputBlocker = new TutorialInputBlocker(player);

        if (!conditions.IsValid)
        {
            Debug.LogWarning("[Tutorial] 플레이어 컴포넌트(PlayerController/InventoryManager)를 찾지 못했습니다.");
            return;
        }

        SceneStarter starter = FindFirstObjectByType<SceneStarter>();
        if (starter != null && !starter.IsSequenceFinished)
        {
            starter.OnSequenceFinished += HandleIntroFinished;
        }
        else
        {
            StartCoroutine(StartAfterDelay(startDelayWithoutStarter));
        }
    }

    void OnDestroy()
    {
        SceneStarter starter = FindFirstObjectByType<SceneStarter>();
        if (starter != null)
        {
            starter.OnSequenceFinished -= HandleIntroFinished;
        }
    }

    private void HandleIntroFinished()
    {
        BeginTutorial();
    }

    private IEnumerator StartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginTutorial();
    }

    private void BeginTutorial()
    {
        if (tutorialActive)
        {
            return;
        }

        tutorialActive = true;
        stepsRunning = steps.Count > 0;
        if (promptUI != null)
        {
            promptUI.SetSkipVisible(true);
        }

        if (stepsRunning)
        {
            EnterStep(0);
        }
    }

    void Update()
    {
        if (!tutorialActive)
        {
            return;
        }

        bool tipShowing = tipPopup != null && tipPopup.IsShowing;

        // 팁이 없는데 시간이 멈춰 있으면 일시정지 메뉴가 열린 상태 → 아무것도 하지 않음
        if (!tipShowing && Time.timeScale == 0f)
        {
            return;
        }

        if (HandleSkipInput())
        {
            return;
        }

        if (tipShowing)
        {
            return;
        }

        if (TryShowTip())
        {
            return;
        }

        UpdateCurrentStep();
    }

    // ───────── 기본 진행 ─────────

    private void EnterStep(int index)
    {
        stepIndex = index;
        TutorialStep step = steps[index];

        conditions.BeginStep(step);
        if (promptUI != null)
        {
            promptUI.ShowStep(step, index, steps.Count);
        }

        if (highlight != null)
        {
            if (step.highlightTarget)
            {
                highlight.Begin(conditions.FlashlightItem, playerTransform);
            }
            else
            {
                highlight.End();
            }
        }
    }

    private void UpdateCurrentStep()
    {
        if (!stepsRunning || isAdvancing || stepIndex < 0 || stepIndex >= steps.Count)
        {
            return;
        }

        if (conditions.EvaluateStep(steps[stepIndex]) >= 1f)
        {
            StartCoroutine(CompleteStepRoutine());
        }
    }

    private IEnumerator CompleteStepRoutine()
    {
        isAdvancing = true;

        if (promptUI != null)
        {
            promptUI.MarkComplete();
        }

        if (highlight != null && steps[stepIndex].highlightTarget)
        {
            highlight.End();
        }

        yield return new WaitForSecondsRealtime(stepCompleteDelay);

        // 팁이 떠 있는 동안에는 다음 단계로 넘어가지 않음
        while (tipPopup != null && tipPopup.IsShowing)
        {
            yield return null;
        }

        isAdvancing = false;

        int next = stepIndex + 1;
        if (next < steps.Count)
        {
            EnterStep(next);
        }
        else
        {
            StartCoroutine(FinishStepsRoutine());
        }
    }

    private IEnumerator FinishStepsRoutine()
    {
        stepsRunning = false;
        MarkCompleted();

        if (promptUI != null)
        {
            promptUI.ShowFinished(finishMessage);
        }

        yield return new WaitForSecondsRealtime(finishMessageDuration);

        if (promptUI != null)
        {
            promptUI.Hide();
        }

        // 상황형 팁은 기본 진행이 끝난 뒤에도 남은 것이 있으면 계속 감시
        if (shownTips.Count >= tips.Count)
        {
            tutorialActive = false;
        }
    }

    // ───────── 상황형 팁 ─────────

    private bool TryShowTip()
    {
        if (tipPopup == null)
        {
            return false;
        }

        for (int i = 0; i < tips.Count; i++)
        {
            if (shownTips.Contains(i) || !conditions.IsTipTriggered(tips[i].trigger))
            {
                continue;
            }

            shownTips.Add(i);
            tipPopup.Show(tips[i], inputBlocker, HandleTipClosed);
            return true;
        }

        return false;
    }

    private void HandleTipClosed()
    {
        if (!stepsRunning && shownTips.Count >= tips.Count)
        {
            tutorialActive = false;
        }
    }

    // ───────── 건너뛰기 ─────────

    /// <returns>건너뛰기가 실행되면 true</returns>
    private bool HandleSkipInput()
    {
        if (!stepsRunning)
        {
            return false;
        }

        if (Input.GetButton("Interact"))
        {
            skipHoldTimer += Time.unscaledDeltaTime;
        }
        else
        {
            skipHoldTimer = 0f;
        }

        if (promptUI != null)
        {
            promptUI.SetSkipProgress(skipHoldDuration > 0f ? skipHoldTimer / skipHoldDuration : 1f);
        }

        if (skipHoldTimer < skipHoldDuration)
        {
            return false;
        }

        SkipTutorial();
        return true;
    }

    public void SkipTutorial()
    {
        StopAllCoroutines();

        tutorialActive = false;
        stepsRunning = false;
        isAdvancing = false;
        skipHoldTimer = 0f;

        if (tipPopup != null)
        {
            tipPopup.ForceClose();
        }

        if (highlight != null)
        {
            highlight.End();
        }

        if (promptUI != null)
        {
            promptUI.SetSkipProgress(0f);
            promptUI.Hide();
        }

        MarkCompleted();
        Debug.Log("[Tutorial] 튜토리얼을 건너뛰었습니다.");
    }

    private static void MarkCompleted()
    {
        PlayerPrefs.SetInt(CompletedPrefsKey, 1);
        PlayerPrefs.Save();
    }
}
