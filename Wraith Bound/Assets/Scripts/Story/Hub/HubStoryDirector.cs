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
        noteRead = HubStoryProgress.NoteRead; // 예전에 읽었으면 쪽지 재촉 안 함 (쪽지는 다시 바닥에 놓여 있음)
        if (note != null)
        {
            note.Read += () =>
            {
                noteRead = true;
                HubStoryProgress.MarkNoteRead();
            };
        }

        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        // 열린 문까지 가 봤으면 (챕터로 들어갔든, 메뉴로 나갔든) 허브 첫 진행은 끝난 것으로
        if (openDoorZone != null && openDoorZone.HasFired)
        {
            HubStoryProgress.MarkComplete();
        }
    }

    private IEnumerator Run()
    {
        // 메인 메뉴 배경일 때는 '이어서 하기'로 플레이가 시작될 때까지 대기 (메뉴 위에 독백이 뜨지 않게)
        while (HubArrival.MenuBackdrop || (awakening != null && !awakening.HasFinished))
        {
            yield return null;
        }

        // 허브 첫 진행이 이미 끝났거나, 깨어난 적도 없는 입장(챕터 테스트 등)이면 조용히
        bool firstArrival = awakening == null || awakening.Played;
        if (!firstArrival)
        {
            HideFirstArrivalItems();
        }
        if (awakening != null && awakening.Played)
        {
            // 깨어남 연출 = 프롤로그에서 막 넘어온 새 진행 (또는 에디터에서 허브 바로 실행) → 처음부터
            HubStoryProgress.Clear();
            noteRead = false;
        }

        if (HubStoryProgress.Complete || (!firstArrival && !HubStoryProgress.Started))
        {
            Log("허브 재입장 — 독백 생략");
            yield break;
        }

        HubStoryProgress.MarkStarted();
        Log(firstArrival ? "허브 진행 시작" : "허브 진행 이어서 (남은 단계부터)");

        // 반응 독백은 저장해 두고 평생 한 번만. 깨어나는 동안 이미 구역에 들어가 발동했으면 지금 띄움
        SubscribeZone(doorsZone, HubStoryProgress.DoorsLine, doorsLine, 2.4f);
        SubscribeZone(openDoorZone, HubStoryProgress.OpenDoorLine, openDoorLine, 3.2f);
        if (firstArrival)
        {
            StartCoroutine(WatchCamcorder());
        }
        if (doorsZone == null)
        {
            // 작은 방 — 깨어나자마자 문 세 개가 다 보이므로 구역 대신 바로
            yield return new WaitForSeconds(1.2f);
            SayOnce(HubStoryProgress.DoorsLine, doorsLine, 2.4f);
        }

        // 쪽지 → 캠코더 → 열린 문 순으로, 해당 단계에서 오래 머뭇거릴 때만 한 번씩 재촉
        // (캠코더는 인벤토리가 저장되지 않아 다시 들어오면 또 주워야 하므로 지금 상태로 판단)
        if (firstArrival) // 재입장이면 쪽지·캠코더가 없으니 재촉도 안 함
        {
            yield return Nudge(() => noteRead || note == null, noteNudgeAfter, noteNudgeLine);
            yield return Nudge(HasCamcorder, camcorderNudgeAfter, camcorderNudgeLine);
        }
        yield return Nudge(() => openDoorZone == null || openDoorZone.HasFired, openDoorNudgeAfter, openDoorNudgeLine);

        HubStoryProgress.MarkComplete();
        Log("허브 첫 진행 완료");
    }

    // 깨어남 연출 없이 들어온 허브(챕터 클리어·사망 후 귀환 등)엔 바닥 쪽지·캠코더를 두지 않음 (2026-11-19 사용자 요청).
    // 쪽지는 뒷벽 게시판(HubNoteArchive)에 항상 걸려 있어 거기서 다시 읽음
    private void HideFirstArrivalItems()
    {
        if (note != null)
        {
            note.gameObject.SetActive(false);
        }

        foreach (ItemObject item in FindObjectsByType<ItemObject>(FindObjectsSortMode.None))
        {
            if (item.name.StartsWith("World_Camcorder") && item.gameObject.scene == gameObject.scene)
            {
                item.gameObject.SetActive(false);
            }
        }

        Log("재입장 — 바닥 쪽지·캠코더 숨김");
    }

    private static void SubscribeZone(StoryZone zone, string lineId, string line, float seconds)
    {
        if (zone == null)
        {
            return;
        }

        if (zone.HasFired)
        {
            SayOnce(lineId, line, seconds);
            return;
        }

        zone.Entered += () => SayOnce(lineId, line, seconds);
    }

    private static void SayOnce(string lineId, string line, float seconds)
    {
        if (HubStoryProgress.WasLineSaid(lineId))
        {
            return;
        }

        HubStoryProgress.MarkLineSaid(lineId);
        Say(line, seconds);
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
        SayOnce(HubStoryProgress.CamcorderLine, camcorderLine, 3f);
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
