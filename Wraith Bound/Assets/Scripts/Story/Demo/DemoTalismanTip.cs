using UnityEngine;

/// <summary>
/// 처음으로 부적을 조준하면 튜토리얼 팁 팝업(게임 정지, Space·ESC·클릭으로 닫기)으로 부적 설명 (2026-11-19 사용자 요청).
/// 한 관람객당 한 번 — 게임을 켜 둔 동안 기억하고, 운영자 초기화(F5, TraitRuntime)가 Reset으로 지움.
/// 시연 씬에 부적(DemoTalisman)이 있으면 그 Start가 Ensure로 감시 오브젝트를 하나 만듦. 팝업이 없는 씬이면 화면 문구로 대신.
/// </summary>
public class DemoTalismanTip : MonoBehaviour
{
    private const string Title = "부적";
    private const string Message =
        "출구 대문은 <b>부적 세 장</b>으로 봉인돼 있습니다.\n" +
        "병원 곳곳에 놓인 부적을 조준하고 <b>E</b>를 누르면 부적이 불타고,\n대문의 봉인도 한 장씩 함께 타오릅니다.\n" +
        "<b>세 장</b>을 태우면 출구가 열립니다.\n\n" +
        "<b>캠코더 화면</b>으로 비추면 다음 부적의 기운이 벽 너머로도 보입니다.";

    private static DemoTalismanTip instance;
    private static bool shown;

    private PlayerInteractor interactor;
    private GameObject player;

    public static void Ensure()
    {
        if (instance != null || shown)
        {
            return;
        }

        instance = new GameObject("DemoTalismanTip").AddComponent<DemoTalismanTip>();
    }

    public static void Reset()
    {
        shown = false;
    }

    private void Update()
    {
        if (shown)
        {
            Destroy(gameObject);
            return;
        }

        if (interactor == null)
        {
            player = GameObject.FindWithTag("Player");
            interactor = player != null ? player.GetComponentInChildren<PlayerInteractor>() : null;
            if (interactor == null)
            {
                return;
            }
        }

        if (!(interactor.CurrentTarget is DemoTalisman talisman) || talisman.GetInteractPrompt() == null || StoryDocumentReader.IsOpen)
        {
            return;
        }

        TutorialTipPopupUI popup = FindFirstObjectByType<TutorialTipPopupUI>(FindObjectsInactive.Include);
        if (popup != null && popup.IsShowing)
        {
            return; // 다른 팁이 떠 있으면 닫힌 뒤에
        }

        shown = true;
        if (popup != null && popup.isActiveAndEnabled)
        {
            popup.Show(new TutorialTip(default, Title, Message), new TutorialInputBlocker(player), null);
        }
        else
        {
            ScreenMessageUI.Show("[E] 부적 태우기 — 세 장을 태우면 출구의 봉인이 풀린다", 4f);
        }

        Debug.Log("[Demo] 부적 튜토리얼 팁 표시");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
