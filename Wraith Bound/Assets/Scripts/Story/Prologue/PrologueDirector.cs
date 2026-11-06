using System.Collections;
using UnityEngine;

/// <summary>
/// 프롤로그 진행 (조작 시작 ~ 고정 사망). CutscenePlayerHandoff가 조작을 넘길 때 이 오브젝트를 켜서 시작.
///  ① 정문을 열어 보면 "왜 안 열리지…" (SealedDoorInteract) →② E1 복도 끝 캠코더로만 보이는 환자 → ③ 진료실 일지 + 그 옆 병실 열쇠 줍기
///  → ④ E2 소리(철문 쾅)로만 등장을 알림 + "당장 숨어야 해" → 열쇠로 병실 문을 열고 트롤리 밑에 숨음
///  → ⑤ 숨으면 간호사가 병실에 들어와 트롤리 앞을 지나 뒷문으로 나감(위기 넘김) / 제때 안 숨으면 나타나 추격
///  → ⑥ 뒷문으로 빠져 긴 복도 중간에서 들킴 → 추격 → ⑦ 비상구 잠김·막다른 길에서 사망 → 허브
/// 부스 시연용: 각 단계에 시간 제한이 있어 머뭇거려도 다음 단계가 진행됨.
/// </summary>
public class PrologueDirector : MonoBehaviour
{
    [Header("Zones")]
    [SerializeField] private StoryZone corridorAZone;
    [SerializeField] private StoryZone corridorBMidZone;
    [SerializeField] private StoryZone exitDoorZone;

    [Header("Actors")]
    [SerializeField] private CamcorderOnlyApparition apparition;
    [SerializeField] private StoryDocumentPickup journal;
    [SerializeField] private PrologueNurse nurse;
    [SerializeField] private CutsceneDoorSwing steelDoor;
    [Tooltip("병실 입구 잠긴 문 — 플레이어가 닫아 두었으면 간호사가 지나갈 때 열림")]
    [SerializeField] private DoorClick wardDoor;
    [SerializeField] private PrologueDeathSequence death;

    [Header("Points")]
    [SerializeField] private Transform nurseSpawn;
    [SerializeField] private Transform corridorAMid;
    [SerializeField] private Transform wardEntrance;
    [Tooltip("숨는 곳 앞을 지나가는 경로 (왼쪽 → 앞 → 병실 뒷문 → 사라지는 곳)")]
    [SerializeField] private Transform hidePassStart;
    [SerializeField] private Transform hidePassFront;
    [SerializeField] private Transform hideSpotLookAt;
    [SerializeField] private Transform wardBackDoor;
    [SerializeField] private Transform nurseExit;
    [SerializeField] private Transform corridorBStart;
    [SerializeField] private Transform exitDoorFace;

    [Header("Timeouts (초)")]
    [SerializeField] private float e1Timeout = 40f;
    [SerializeField] private float apparitionTimeout = 22f;
    [Tooltip("진료실 열쇠를 안 주워도 이 시간이 지나면 E2")]
    [SerializeField] private float keyTimeout = 60f;
    [Tooltip("E2 소리 후 이 시간 안에 숨지 않으면 간호사가 나타나 추격")]
    [SerializeField] private float hideTimeout = 25f;
    [Tooltip("숨은 뒤 간호사가 나타나기까지")]
    [SerializeField] private float hiddenToAppearDelay = 1.5f;
    [Tooltip("숨는 곳 앞에서 잠깐 멈추는 시간 (0이면 그냥 지나감)")]
    [SerializeField] private float hidePauseSeconds = 0f;
    [SerializeField] private float sneakTimeout = 30f;
    [SerializeField] private float chaseCatchUpAfter = 25f;

    [Header("Chase")]
    [Tooltip("플레이어 달리기 속도 대비 추격 속도 (1보다 작아야 비상구까지 도착)")]
    [SerializeField] private float chaseSpeedRatio = 0.85f;
    [SerializeField] private float tooCloseDistance = 3.5f;

    private GameObject player;
    private PlayerHidingController hiding;
    private PlayerChaseEffectController chaseEffect;
    private InventoryManager inventory;
    private float playerRunSpeed = 6f;
    [Header("Sound (비워 두면 무음 — 등장은 소리로 알림)")]
    [SerializeField] private AudioSource e2AudioSource;
    [SerializeField] private AudioClip e2Clip;

    private bool chasing;
    private float chaseStartTime;

    private void Start()
    {
        player = GameObject.FindWithTag("Player");
        hiding = player.GetComponent<PlayerHidingController>();
        chaseEffect = player.GetComponent<PlayerChaseEffectController>();
        inventory = player.GetComponent<InventoryManager>();
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null && controller.runSpeed > 0f)
        {
            playerRunSpeed = controller.runSpeed;
        }

        nurse.Caught += OnNurseCaught;
        nurse.gameObject.SetActive(false);
        StartCoroutine(Run());
    }

    private bool PlayerHidden => hiding != null && hiding.isHiding;

    private static void Log(string message)
    {
        Debug.Log("[Prologue] " + message);
    }

    private IEnumerator Run()
    {
        // ① 갇힘 — 대사는 플레이어가 정문을 열어 보려 할 때 정문(SealedDoorInteract)이 띄움

        // ② E1 — 복도 A에 들어서면 (또는 시간 초과) 끝에 형체
        yield return WaitUntilOrTimeout(() => corridorAZone.HasFired, e1Timeout);
        Log("E1 캠코더 형체 (구역 진입=" + corridorAZone.HasFired + ")");
        yield return RunApparition();

        // ③ 진료실 일지 옆 병실 열쇠를 주우면 진행 (안 주워도 시간 지나면 진행 — 그땐 숨을 곳이 없어 추격으로)
        bool read = false;
        journal.Read += () => read = true;
        yield return WaitUntilOrTimeout(() => inventory != null && inventory.HasKey(), keyTimeout);
        bool hasKey = inventory != null && inventory.HasKey();
        Log("열쇠 단계 끝 (열쇠=" + hasKey + ", 일지 읽음=" + read + ")");
        if (hasKey)
        {
            yield return new WaitForSeconds(0.5f);
            SubtitleUI.ShowForDuration("나", "…열쇠? 병실 열쇠인가 봐.", 2.6f);
            yield return new WaitForSeconds(2.8f);
        }

        // ④ E2 — 소리로만 등장을 알림 → 숨으면 간호사가 사물함 앞을 지나감
        yield return RunSoundCue();
        bool passed = false;
        yield return RunHidePass(result => passed = result);

        Log(passed ? "사물함 위기 넘김" : "숨지 않아 들킴 → 추격");
        if (!passed)
        {
            yield break; // 추격 중 — 잡히면 OnNurseCaught
        }

        // ⑥ 몰래 빠져나가기 → 긴 복도 중간에서 들킴 (또는 시간 초과면 간호사가 되돌아옴)
        SubtitleUI.ShowForDuration("나", "…지나갔어. …지금이야, 나가자.", 3f);
        bool reachedMid = false;
        yield return WaitUntilOrTimeout(() => (reachedMid = corridorBMidZone.HasFired), sneakTimeout);

        Log(reachedMid ? "긴 복도 중간에서 들킴" : "몰래 빠져나가기 시간 초과 → 간호사 복귀");
        if (reachedMid)
        {
            nurse.Appear(corridorBStart.position, corridorBStart.rotation);
            SubtitleUI.ShowForDuration("나", "…!", 1.2f);
        }
        else
        {
            nurse.Appear(wardEntrance.position, wardEntrance.rotation);
        }

        StartChase(); // ⑦ 비상구 앞 막다른 길 사망은 ChaseWatch가 처리
    }

    private IEnumerator RunApparition()
    {
        bool seen = false;
        apparition.Seen += () => seen = true;
        apparition.Show();
        SubtitleUI.ShowForDuration("나", "…? 방금 저 끝에서 뭐가 움직였는데.", 3f);

        // 대사가 끝나면 게임을 멈추고 캠코더 사용법 팝업 (튜토리얼 상황형 팁과 같은 UI)
        yield return new WaitForSecondsRealtime(3.1f);
        if (!seen) // 그새 캠코더로 이미 봤으면 생략
        {
            yield return ShowCamcorderTip();
        }

        yield return WaitUntilOrTimeout(() => seen, apparitionTimeout);
        if (seen)
        {
            yield return new WaitForSeconds(0.8f);
            SubtitleUI.ShowForDuration("나", "…방금, 뭐였지?", 2.6f);
        }
        else
        {
            apparition.HideSilently();
        }
    }

    private IEnumerator ShowCamcorderTip()
    {
        TutorialTipPopupUI popup = FindFirstObjectByType<TutorialTipPopupUI>(FindObjectsInactive.Include);
        if (popup == null || !popup.isActiveAndEnabled)
        {
            ScreenMessageUI.Show("[F] 캠코더 꺼내기   [좌클릭] 화면 보기", 4.5f); // 팝업이 없는 씬이면 예전 안내
            yield break;
        }

        while (popup.IsShowing) // 튜토리얼 팁이 이미 떠 있으면 닫힐 때까지
        {
            yield return null;
        }

        bool holding = inventory != null && inventory.IsCamcorderHeld();
        string howTo = holding
            ? "<b>좌클릭</b>으로 캠코더 화면을 봅니다."
            : "<b>F</b>로 캠코더를 꺼내고 <b>좌클릭</b>으로 화면을 봅니다.";
        var tip = new TutorialTip(default, "캠코더",
            howTo + "\n캠코더 화면에는 <b>맨눈으로 보이지 않는 것</b>이 비칩니다.\n<b>마우스 휠</b>로 확대, 다시 <b>좌클릭</b>하면 내립니다.");

        bool closed = false;
        popup.Show(tip, new TutorialInputBlocker(player), () => closed = true);
        Log("캠코더 팁 표시 (게임 정지)");
        while (!closed)
        {
            yield return null;
        }
    }

    // 괴물은 아직 안 보임 — 복도 끝 철문이 쾅 열리는 소리 + 대사
    private IEnumerator RunSoundCue()
    {
        if (e2AudioSource != null && e2Clip != null)
        {
            e2AudioSource.PlayOneShot(e2Clip);
        }

        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            steelDoor.Blend = 1f - t / 0.5f; // 1=닫힘 → 0=열림 (곡선상 처음에 확 열림)
            yield return null;
        }

        steelDoor.Blend = 0f; // 추격 연출은 없음 — 괴물이 아직 플레이어를 못 봄

        yield return new WaitForSeconds(0.4f);
        SubtitleUI.ShowForDuration("나", "이게 무슨 소리지…?! 당장 숨어야 해.", 3.2f);
    }

    // ⑤ 숨을 때까지 기다림 → 숨으면 간호사가 병실로 들어와 사물함 앞을 지나 뒷문으로 나감(true)
    //    제때 안 숨거나, 지나가는 동안 숨는 곳에서 나와 들키면 추격(false)
    private IEnumerator RunHidePass(System.Action<bool> done)
    {
        yield return WaitUntilOrTimeout(() => PlayerHidden, hideTimeout);
        if (!PlayerHidden)
        {
            Log("제때 숨지 않음");
            nurse.Appear(nurseSpawn.position, nurseSpawn.rotation);
            StartChase();
            done(false);
            yield break;
        }

        yield return new WaitForSeconds(hiddenToAppearDelay);
        ForceOpen(wardDoor);
        nurse.Appear(nurseSpawn.position, nurseSpawn.rotation);

        Transform[] route = { corridorAMid, wardEntrance, hidePassStart, hidePassFront, wardBackDoor, nurseExit };
        foreach (Transform point in route)
        {
            bool arrived = false;
            nurse.WalkTo(point.position, () => arrived = true);
            while (!arrived)
            {
                if (SpotsPlayerUp(tooCloseDistance))
                {
                    StartChase();
                    done(false);
                    yield break;
                }

                yield return null;
            }

            if (point == hidePassFront && hidePauseSeconds > 0f)
            {
                nurse.Stop();
                nurse.FaceTowards(hideSpotLookAt.position);
                yield return new WaitForSeconds(hidePauseSeconds);
            }
        }

        nurse.Vanish();
        done(true);
    }

    // 플레이어가 병실 문을 닫고 숨었으면 간호사가 지나갈 수 있게 열어 둠 (DoorClick 내부 상태를 E로 연 것과 같게)
    private static void ForceOpen(DoorClick door)
    {
        if (door == null || door.IsOpen())
        {
            return;
        }

        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        System.Type type = typeof(DoorClick);
        type.GetMethod("SetDoorDirection", flags)?.Invoke(door, null);
        type.GetField("open", flags)?.SetValue(door, true);
        type.GetMethod("SyncNavMeshObstacle", flags)?.Invoke(door, new object[] { true });
    }

    private bool SpotsPlayerUp(float distance)
    {
        if (PlayerHidden)
        {
            return false;
        }

        float d = Vector3.Distance(nurse.transform.position, player.transform.position);
        return d <= distance && nurse.CanSee(player.transform);
    }

    private void StartChase()
    {
        chasing = true;
        chaseStartTime = Time.time;
        nurse.Chase(player.transform, playerRunSpeed * chaseSpeedRatio);
        StartCoroutine(ChaseEffectWhenSeen());
        StartCoroutine(ChaseWatch());
    }

    // 추격 연출(발각·붉은 화면 등)은 간호사가 플레이어를 실제로 본 순간부터 — 소리 듣고 찾아오는 중엔 안 켬 (본편 PlayerChaseStateLink와 같은 기준)
    private IEnumerator ChaseEffectWhenSeen()
    {
        while (chasing && !death.IsRunning)
        {
            if (!PlayerHidden && nurse.CanSee(player.transform))
            {
                Log("간호사가 플레이어를 봄 → 추격 연출");
                if (chaseEffect != null)
                {
                    chaseEffect.SetBeingChased(true);
                }

                yield break;
            }

            yield return null;
        }
    }

    // 추격 중: 비상구 앞에 닿으면 막다른 길 사망, 너무 오래 버티면(빙빙 돌기) 따라잡게
    private IEnumerator ChaseWatch()
    {
        while (chasing && !death.IsRunning)
        {
            if (exitDoorZone.IsPlayerInside)
            {
                Log("비상구 막다른 길 → 사망");
                chasing = false;
                death.Begin(nurse, true, exitDoorFace.position);
                yield break;
            }

            if (Time.time - chaseStartTime > chaseCatchUpAfter)
            {
                nurse.SetChaseSpeed(playerRunSpeed * 1.15f);
            }

            yield return null;
        }
    }

    private void OnNurseCaught()
    {
        if (death.IsRunning)
        {
            return;
        }

        chasing = false;
        bool atExit = exitDoorZone.IsPlayerInside;
        Log("잡힘 (거리 " + Vector3.Distance(nurse.transform.position, player.transform.position).ToString("F1") + "m, 비상구=" + atExit + ")");
        death.Begin(nurse, atExit, exitDoorFace.position);
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
    {
        float t = 0f;
        while (!condition() && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}
