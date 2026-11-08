using System.Collections;
using UnityEngine;

/// <summary>
/// 허브(귀문의 방) 진행 — 순서는 강제하지 않고 독백으로 유도. 깨어남(HubAwakening)이 끝나면 시작.
///  · 문 세 개 앞에 서면 "문이… 세 개?" / 열린 문(붉은 복도) 앞이면 "이쪽만 열려 있어…"
///  · 캠코더를 다시 주우면 "내 캠코더…?"
///  · 부스 시연용 재촉: 오래 머뭇거리면 다음 할 일을 독백으로 (쪽지 → 캠코더 → 열린 문)
/// 봉인된 문은 각 문의 SealedDoorInteract, 쪽지는 StoryDocumentPickup이 직접 처리.
/// </summary>
public class HubStoryDirector : MonoBehaviour
{
    [SerializeField] private HubAwakening awakening;
    [SerializeField] private StoryZone doorsZone;
    [SerializeField] private StoryZone openDoorZone;
    [SerializeField] private StoryDocumentPickup note;

    [Header("Text")]
    [SerializeField] private string doorsLine = "문이… 세 개?";
    [SerializeField] private string openDoorLine = "이쪽만 열려 있어… 소독약 냄새. 그 병원이야.";
    [SerializeField] private string camcorderLine = "내 캠코더…? 분명 그때 떨어뜨렸는데. 같이 여기로 온 건가…";

    [Header("재촉 (부스 시연용, 초 — 0이면 안 함)")]
    [SerializeField] private float noteNudgeAfter = 20f;
    [SerializeField] private string noteNudgeLine = "…발밑에 쪽지랑… 저건 내 캠코더?";
    [SerializeField] private float camcorderNudgeAfter = 20f;
    [SerializeField] private string camcorderNudgeLine = "캠코더는 챙겨 가자.";
    [SerializeField] private float openDoorNudgeAfter = 30f;
    [SerializeField] private string openDoorNudgeLine = "열려 있는 건 저 붉은 복도 쪽 하나뿐이야…";

    private InventoryManager inventory;
    private bool noteRead;

    private static void Log(string message)
    {
        Debug.Log("[Hub] " + message);
    }

    private void Start()
    {
        inventory = GameObject.FindWithTag("Player")?.GetComponent<InventoryManager>();
        if (note != null)
        {
            note.Read += () => noteRead = true;
        }

        if (doorsZone != null)
        {
            doorsZone.Entered += () => Say(doorsLine, 2.4f);
        }

        if (openDoorZone != null)
        {
            openDoorZone.Entered += () => Say(openDoorLine, 3.2f);
        }

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (awakening != null && !awakening.HasFinished)
        {
            yield return null;
        }

        Log("허브 진행 시작");
        StartCoroutine(WatchCamcorder());
        if (doorsZone == null)
        {
            // 작은 방 — 깨어나자마자 문 세 개가 다 보이므로 구역 대신 바로
            yield return new WaitForSeconds(1.2f);
            Say(doorsLine, 2.4f);
        }

        // 쪽지 → 캠코더 → 열린 문 순으로, 해당 단계에서 오래 머뭇거릴 때만 한 번씩 재촉
        yield return Nudge(() => noteRead || note == null, noteNudgeAfter, noteNudgeLine);
        yield return Nudge(HasCamcorder, camcorderNudgeAfter, camcorderNudgeLine);
        yield return Nudge(() => openDoorZone == null || openDoorZone.HasFired, openDoorNudgeAfter, openDoorNudgeLine);
    }

    private IEnumerator Nudge(System.Func<bool> done, float after, string line)
    {
        float t = 0f;
        while (!done())
        {
            t += Time.deltaTime;
            if (after > 0f && t >= after)
            {
                Say(line, 3f);
                t = float.MinValue; // 한 번만
            }

            yield return null;
        }
    }

    private IEnumerator WatchCamcorder()
    {
        while (!HasCamcorder())
        {
            yield return null;
        }

        Log("캠코더 다시 주움");
        yield return new WaitForSeconds(0.4f);
        Say(camcorderLine, 3f);
    }

    private bool HasCamcorder()
    {
        return inventory != null && inventory.HasCamcorder;
    }

    private static void Say(string line, float seconds)
    {
        if (!string.IsNullOrEmpty(line))
        {
            SubtitleUI.ShowForDuration("나", line, seconds);
        }
    }
}
