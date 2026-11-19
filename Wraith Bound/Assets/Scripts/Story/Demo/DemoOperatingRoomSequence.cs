using System.Collections;
using UnityEngine;

/// <summary>
/// 시연 맵 연출 ③ — 수술실.
///  · 수술대 위 무영등 하나만 켜져 봉인 열쇠 1을 비춤.
///  · 열쇠를 집으면 무영등이 지직거리다 꺼짐 → 정적 → 붉은 경보등이 맥박처럼 켜짐 → "…뭐, 뭐야…?"
///  · 옆방 문 너머에 탈춤귀신을 옮겨 놓고 곧장 추격(늘어나는 몸·겹친 몸·관절 꺾임은 크리처 추격 연출이 알아서 켜짐) → "…도망쳐!"
///  · 붉은 등은 redDuration 동안 맥박치다 꺼짐. 추격 자체는 일반 AI(놓치면 수색 → 순찰).
/// </summary>
public class DemoOperatingRoomSequence : MonoBehaviour
{
    [Tooltip("이 번호의 봉인(열쇠·부적)이 기록되면 연출 시작")]
    [SerializeField] private int sealIndex;
    [SerializeField] private Light surgicalLight;
    [SerializeField] private Renderer[] surgicalGlow = { };
    [SerializeField] private Light[] redLights = { };
    [SerializeField] private float redIntensity = 6f;
    [SerializeField] private float pulseSpeed = 2.4f;
    [SerializeField] private float redDuration = 25f;

    [Header("탈춤귀신")]
    [SerializeField] private EnemyBase ghost;
    [SerializeField] private Transform ghostSpawn;
    [SerializeField] private float ghostDelay = 1.4f;
    [Tooltip("귀신이 나올 때 열어 둘 문 — 닫힌 문 앞에서 경로가 끊겨 추격을 포기하던 것 방지")]
    [SerializeField] private DoorClick[] openOnRelease = { };
    [Tooltip("켜면 부서지는 문(DoorBrokenTest)은 열지 않고 귀신이 부수게 둠")]
    [SerializeField] private bool breakableDoorsStayClosed = true;

    [SerializeField] private string redLine = string.Empty; // 시연 연출은 대사 없이(2026-11-19 사용자 결정) — 비우면 생략
    [SerializeField] private string runLine = string.Empty;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip alarmClip;

    [Header("예고 소리 — 탈춤귀신 노래가 멀리서 다가옴")]
    [SerializeField] private bool approachCue = true;
    [Tooltip("비우면 탈춤귀신의 순찰 노래를 그대로 씀")]
    [SerializeField] private AudioClip cueClip;
    [SerializeField, Range(0f, 1f)] private float cueVolume = 0.8f;
    [Tooltip("노래가 등장 지점까지 다가오는 시간(초) — 이 시간 뒤 귀신이 나타남")]
    [SerializeField] private float cueSeconds = 6f;
    [Tooltip("노래가 시작하는 플레이어와의 거리(m). SpatialSoundFx 최대 거리(30m)보다 작게")]
    [SerializeField] private float cueStartDistance = 27f;
    [Tooltip("플레이어와 이 거리보다 가까이에서는 나타나지 않음 (정해 둔 자리가 더 가까우면 그 자리)")]
    [SerializeField] private float approachDistance = 12f;

    [Header("숨으면 어슬렁대다 방 밖으로")]
    [Tooltip("플레이어가 숨은 채 searchSeconds를 버티면 귀신이 수색을 그만두고 방 밖 다른 곳으로 걸어가 그곳부터 순찰")]
    [SerializeField] private bool leaveWhenHidden = true;
    [Tooltip("떠나 걸어간 뒤 안 보이면 사라짐 (2026-11-19 — 남아서 갈 길을 막았음)")]
    [SerializeField] private bool vanishAfterLeave = true;
    [Tooltip("숨지 않고 따돌린 경우: 풀려난 뒤 이 시간이 지나 귀신이 추격을 놓친 채 lostSeconds 동안 있으면 떠남")]
    [SerializeField] private float lostMinSeconds = 12f;
    [SerializeField] private float lostSeconds = 3f;
    [Tooltip("숨은 뒤 귀신이 숨은 곳 주변을 어슬렁대는 시간(초)")]
    [SerializeField] private float searchSeconds = 6f;
    [Tooltip("떠날 곳까지의 거리 범위(m) — 수술실 밖 복도 쪽")]
    [SerializeField] private float leaveMinDistance = 22f;
    [SerializeField] private float leaveMaxDistance = 40f;
    [Tooltip("떠나는 걸음 속도 (0이면 귀신 기본 이동 속도)")]
    [SerializeField] private float leaveWalkSpeed;
    [Tooltip("떠나는 중 숨는 곳에서 나온 플레이어가 이 거리(m) 안에서 보이면 다시 쫓아옴")]
    [SerializeField] private float reengageDistance = 6f;

    private bool started;
    private Vector3 appearPoint;
    private readonly System.Collections.Generic.List<Vector3> cuePath = new System.Collections.Generic.List<Vector3>();
    private bool pulsing;
    private float pulseStart;

    private void Start()
    {
        SetRed(0f);
        // 열쇠든 부적이든 이 연출에 걸린 봉인 번호가 기록되면 시작 (Chapter1_Demo = 봉인 열쇠, Chapter1_FixedDemo = 부적)
        DemoSealProgress.Collected += OnSealCollected;
    }

    private void OnDestroy()
    {
        DemoSealProgress.Collected -= OnSealCollected;
    }

    private void OnSealCollected(int index)
    {
        if (index == sealIndex && !started)
        {
            started = true;
            StartCoroutine(Run());
        }
    }

    private void Update()
    {
        if (!pulsing)
        {
            return;
        }

        float t = Time.time - pulseStart;
        float fade = t > redDuration ? Mathf.Clamp01(1f - (t - redDuration) / 2f) : 1f;
        float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * pulseSpeed * Mathf.PI * 2f * 0.5f));
        SetRed(redIntensity * pulse * fade);
        if (fade <= 0f)
        {
            pulsing = false;
        }
    }

    private IEnumerator Run()
    {
        Debug.Log("[Demo] ③ 수술실 — 열쇠 1, 붉은 등");
        yield return new WaitForSeconds(0.5f);

        // 무영등 지직 → 꺼짐
        for (int i = 0; i < 6; i++)
        {
            SetSurgical(i % 2 == 1);
            yield return new WaitForSeconds(0.04f + Random.value * 0.09f);
        }

        SetSurgical(false);
        yield return new WaitForSeconds(0.8f);

        pulsing = true;
        pulseStart = Time.time;
        if (audioSource != null && alarmClip != null)
        {
            audioSource.Play();
        }

        if (!string.IsNullOrEmpty(redLine))
        {
            SubtitleUI.ShowForDuration("나", redLine, 2f);
        }

        // 예고: 탈춤귀신 노래가 먼 곳에서 등장 지점까지 다가옴 → 그다음 귀신이 (안 보이는 곳에서) 나타남
        GameObject playerGo = GameObject.FindWithTag("Player");
        Transform playerT = playerGo != null ? playerGo.transform : null;
        Vector3 spawnPos = ghostSpawn != null ? ghostSpawn.position : (ghost != null ? ghost.transform.position : transform.position);
        appearPoint = spawnPos;
        ApproachingSoundCue cue = null;
        if (approachCue && ghost != null && !ghost.gameObject.activeSelf)
        {
            appearPoint = DemoCreatureStaging.Plan(spawnPos, playerT, ghost.transform, approachDistance, cueStartDistance, cuePath);
            MaskGhostSong song = ghost.GetComponentInChildren<MaskGhostSong>(true);
            AudioClip clip = cueClip != null ? cueClip : (song != null ? song.Clip : null);
            cue = ApproachingSoundCue.Play(clip, cuePath, cueSeconds, cueVolume, true, Vector2.zero);
            yield return new WaitForSeconds(Mathf.Max(ghostDelay, cueSeconds));
            if (DemoCreatureStaging.IsVisible(appearPoint, playerT, ghost.transform))
            {
                appearPoint = DemoCreatureStaging.Plan(spawnPos, playerT, ghost.transform, approachDistance, cueStartDistance, cuePath);
            }
        }
        else
        {
            yield return new WaitForSeconds(ghostDelay);
        }

        if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            if (cue != null)
            {
                cue.Finish();
            }

            yield break;
        }

        foreach (DoorClick door in openOnRelease)
        {
            // 부서지는 문은 열어 두지 않음 — 쫓아오는 탈춤귀신이 직접 부수고 들어오게 (2026-11-19 '연출 때 문이 안 부서진다')
            if (door != null && !(breakableDoorsStayClosed && door.GetComponent<DoorBrokenTest>() != null))
            {
                DemoDoorOpener.Open(door, true);
            }
        }

        // 귀신을 숨겨 둔 채(비활성) 배치한 씬(Chapter1_FixedDemo): 등장 자리에서 켜고 AI가 초기화될 때까지 기다림
        if (ghost != null && !ghost.gameObject.activeSelf)
        {
            ghost.transform.SetPositionAndRotation(appearPoint, FacePlayer(appearPoint, playerT));
            ghost.gameObject.SetActive(true);
            DemoRoamingSpawner.RetireSameKind(ghost); // 같은 종류 배회 괴물은 사라짐 (중복 방지)
            yield return null;
            yield return null;
        }

        if (cue != null)
        {
            cue.Finish(); // 귀신 자신의 노래가 이어받음
        }

        ReleaseGhost();
        yield return new WaitForSeconds(0.9f);
        if (!string.IsNullOrEmpty(runLine))
        {
            SubtitleUI.ShowForDuration("나", runLine, 2f);
        }

        if (leaveWhenHidden)
        {
            yield return WatchHideAndLeave();
        }
    }

    // 플레이어가 숨어 있는 동안(귀신이 쫓거나 수색 중일 때) 시간을 재다가 searchSeconds가 되면 방 밖 다른 곳으로 보냄.
    // 연출이 시작될 때 이미 숨어 있었어도 똑같이 — 숨은 곳 근처를 어슬렁대다 나감.
    private IEnumerator WatchHideAndLeave()
    {
        GameObject playerGo = GameObject.FindWithTag("Player");
        Transform player = playerGo != null ? playerGo.transform : null;
        PlayerHidingController hiding = player != null ? player.GetComponent<PlayerHidingController>() : null;
        float hidden = 0f;
        float lost = 0f;
        float released = Time.time;
        while (ghost != null && ghost.gameObject.activeInHierarchy && player != null)
        {
            yield return null;
            if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
            {
                yield break;
            }

            EnemyBase.State state = ghost.RuntimeState.CurrentState;
            bool engaged = ghost.enabled && (state == EnemyBase.State.Chase || state == EnemyBase.State.Investigate);
            hidden = hiding != null && hiding.isHiding && engaged ? hidden + Time.deltaTime : 0f;
            // 숨지 않고 따돌려도(귀신이 추격을 놓치고 순찰로 돌아감) 끝 — 예전엔 그대로 맵을 돌며 갈 길을 막았음 (2026-11-19)
            lost = ghost.enabled && !engaged && Time.time - released > lostMinSeconds ? lost + Time.deltaTime : 0f;
            if (hidden < searchSeconds && lost < lostSeconds)
            {
                continue;
            }

            hidden = 0f;
            lost = 0f;
            if (!DemoCreatureStaging.TryPickLeavePoint(ghost.transform.position, player.position, leaveMinDistance, leaveMaxDistance, out Vector3 leaveTo))
            {
                Debug.Log("[Demo] ③ 탈춤귀신 떠날 곳을 못 찾음 → 그냥 순찰로");
                ghost.Combat.ChangeState(EnemyBase.State.Patrol);
                continue;
            }

            Debug.Log("[Demo] ③ 플레이어가 숨어서 버팀/따돌림 → 탈춤귀신 방 밖으로 " + leaveTo.ToString("F1"));
            bool reengage = false;
            yield return DemoCreatureStaging.WalkAway(ghost, leaveTo, player, leaveWalkSpeed, reengageDistance, r => reengage = r);
            if (reengage && !PlayerDeathDebug.IsDying && !PlayerDeathDebug.IsDead)
            {
                Debug.Log("[Demo] ③ 떠나다 플레이어 발견 → 다시 추격");
                EnemyState s = ghost.RuntimeState;
                s.LastKnownPosition = player.position;
                ghost.Combat.ChangeState(EnemyBase.State.Chase);
                ghost.Combat.ArmChaseGraceTimer();
                released = Time.time;
                continue;
            }

            // 다 걸어 나갔으면 안 보일 때 사라짐 — 같은 종류 배회 괴물은 갈 길에서 먼 곳에 다시 나옴 (DemoSetPieceAftermath)
            if (vanishAfterLeave && ghost != null && !DemoCreatureStaging.IsVisible(ghost.transform.position, player, ghost.transform))
            {
                Debug.Log("[Demo] ③ 탈춤귀신 사라짐");
                ghost.gameObject.SetActive(false);
                yield break;
            }
        }
    }

    private void ReleaseGhost()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (ghost == null || player == null || PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            return;
        }

        if (ghost.Agent != null)
        {
            ghost.Agent.Warp(appearPoint);
            ghost.transform.rotation = FacePlayer(appearPoint, player.transform);
        }

        EnemyState s = ghost.RuntimeState;
        if (s == null || ghost.Combat == null)
        {
            return;
        }

        s.LastKnownPosition = player.transform.position;
        ghost.Combat.ChangeState(EnemyBase.State.Chase);
        ghost.Combat.ArmChaseGraceTimer();
        Debug.Log("[Demo] ③ 탈춤귀신 추격 시작");
    }

    private Quaternion FacePlayer(Vector3 from, Transform player)
    {
        if (player != null)
        {
            Vector3 look = player.position - from;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                return Quaternion.LookRotation(look);
            }
        }

        return ghostSpawn != null ? ghostSpawn.rotation : transform.rotation;
    }

    private void SetSurgical(bool on)
    {
        if (surgicalLight != null)
        {
            surgicalLight.enabled = on;
        }

        foreach (Renderer r in surgicalGlow)
        {
            if (r != null)
            {
                r.enabled = on;
            }
        }
    }

    private void SetRed(float intensity)
    {
        foreach (Light l in redLights)
        {
            if (l != null)
            {
                l.enabled = intensity > 0.01f;
                l.intensity = intensity;
            }
        }
    }
}
