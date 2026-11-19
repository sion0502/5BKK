using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 시연 맵 '숨겨 둔 크리처가 들이닥치는' 연출 공용 (④ 얼굴 없는 귀신, ⑤ Xenosaurid).
///  · 준비(arm): sealIndex 봉인(부적)이 기록되면. sealIndex &lt; 0 이면 처음부터 준비됨.
///  · 발동: 준비된 뒤 triggerZone에 플레이어가 들어오면(없으면 준비 즉시) → delay 뒤
///    대사 → 비활성으로 숨겨 둔 크리처를 spawn에서 켬 → openDoors 열기 / breakDoors는 breakDelay 뒤 부숨(쾅) → 추격 시작.
///  · 추격은 일반 AI(EnemyBase) — 놓치면 수색·순찰. EnemyBase는 수정하지 않음(internal 상태 변경만 호출).
///  · 예고: 크리처를 켜기 전에 그 크리처의 소리(비명·포효)가 먼 곳에서 등장 지점까지 길을 따라 다가옴 (ApproachingSoundCue).
///    등장 지점은 플레이어 눈에 안 보이는 곳으로 고름 — 눈앞에서 '뿅' 나타나지 않게 (DemoCreatureStaging).
///  · revealInSight(멀리서 보여주기): 대신 멀리(약 20m) 보이는 곳에 나타나 플레이어를 바라보며 멈춰 서 있다가
///    (근처 전등이 지직거림) 플레이어가 그걸 보면 비명과 함께 달려옴 — 안 보이는 곳에서 오면 다가오는 걸 몰랐음.
///  · chaseEffectOnStart: 쫓기 시작하는 순간부터 추격 연출(붉은 화면·심장 소리) — 크리처가 직접 보기 전이라도.
///  · leaveAfterHide: 플레이어가 숨어서 searchSeconds 동안 버티면(또는 놓치면) 숨은 곳 주변을 서성이던 크리처가
///    나타났던 자리로 걸어 돌아가 사라짐 — 예전엔 숨은 곳 근처를 계속 맴돌다 앞길로 가서 진행이 막혔음.
///    돌아가는 길에 플레이어가 숨는 곳에서 나와 가까이 보이면 다시 쫓아옴.
/// </summary>
public class DemoAmbushSequence : MonoBehaviour
{
    [Tooltip("이 번호의 봉인(부적)이 기록되면 준비. 음수면 처음부터 준비")]
    [SerializeField] private int sealIndex = -1;
    [SerializeField] private StoryZone triggerZone;
    [SerializeField] private float delay = 0.3f;

    [Header("크리처")]
    [SerializeField] private EnemyBase creature;
    [SerializeField] private Transform spawn;

    [Header("문")]
    [SerializeField] private DoorClick[] openDoors = { };
    [SerializeField] private DoorBrokenTest[] breakDoors = { };
    [Tooltip("크리처가 문 뒤에서 두드리는 시간 — 문을 부수기 전")]
    [SerializeField] private float breakDelay = 1.2f;

    [Tooltip("나타난 뒤 이 시간 동안은 못 봐도 플레이어 위치를 계속 쫓음 — 벽 너머라 시야에 안 들어오면 10초 뒤 추격을 포기하고 순찰로 돌아갔음")]
    [SerializeField] private float huntSeconds = 25f;

    [Header("대사 (화자 '나', 비우면 생략)")]
    [SerializeField] private string beforeLine = string.Empty;
    [SerializeField] private string afterLine = string.Empty;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bangClip;
    [SerializeField] private AudioClip breakClip;

    [Header("예고 소리 (멀리서 다가옴)")]
    [SerializeField] private bool approachCue = true;
    [Tooltip("비우면 크리처의 비명/포효를 그대로 씀")]
    [SerializeField] private AudioClip cueClip;
    [SerializeField, Range(0f, 1f)] private float cueVolume = 1f;
    [Tooltip("소리가 등장 지점까지 다가오는 시간(초) — 이 시간 뒤 크리처가 나타남")]
    [SerializeField] private float cueSeconds = 5f;
    [Tooltip("예고 소리가 시작하는 플레이어와의 거리(m). SpatialSoundFx 최대 거리(30m)보다 작게")]
    [SerializeField] private float cueStartDistance = 27f;
    [Tooltip("비명·포효 사이 쉬는 시간(초)")]
    [SerializeField] private Vector2 cueGap = new Vector2(1f, 2f);
    [Tooltip("플레이어와 이 거리보다 가까이에서는 나타나지 않음 (정해 둔 자리가 더 가까우면 그 자리)")]
    [SerializeField] private float approachDistance = 15f;

    [Header("등장 연출 — 멀리서 보여주기 (켜면 예고 소리 대신)")]
    [SerializeField] private bool revealInSight;
    [SerializeField] private float revealMinDistance = 12f;
    [SerializeField] private float revealDistance = 20f;
    [SerializeField] private float revealMaxDistance = 28f;
    [Tooltip("플레이어가 크리처를 이만큼(초) 화면에 담으면 달려옴")]
    [SerializeField] private float revealLookSeconds = 1.2f;
    [Tooltip("플레이어가 끝내 안 봐도 이 시간(초) 뒤엔 달려옴")]
    [SerializeField] private float revealMaxWait = 4f;
    [Tooltip("플레이어가 이 거리(m) 안으로 다가오면 바로 달려옴")]
    [SerializeField] private float revealBreakDistance = 7f;
    [Tooltip("멀리 보여줄 때 대사 (비우면 beforeLine)")]
    [SerializeField] private string revealLine = "…저, 저기… 뭐가 서 있어.";
    [Tooltip("멈춰 선 크리처 위에 희미한 빛 + 주변 전등 지직거림 — 어두운 복도에서도 보이게")]
    [SerializeField] private bool revealLight = true;

    [Header("추격 연출")]
    [Tooltip("쫓기 시작하는 순간부터 추격 연출(붉은 화면·심장 소리) — 크리처가 플레이어를 직접 보기 전이라도")]
    [SerializeField] private bool chaseEffectOnStart;

    [Header("숨으면 돌아가서 사라짐")]
    [SerializeField] private bool leaveAfterHide;
    [Tooltip("플레이어가 숨은 채 이 시간(초)을 버티면 크리처가 수색을 그만두고 돌아감 — 그동안 숨은 곳 주변을 서성임")]
    [SerializeField] private float searchSeconds = 7f;
    [Tooltip("돌아가는 걸음 속도 (0이면 크리처 기본 이동 속도)")]
    [SerializeField] private float leaveWalkSpeed;
    [Tooltip("돌아가는 중 숨는 곳에서 나온 플레이어가 이 거리(m) 안에서 보이면 다시 쫓아옴")]
    [SerializeField] private float reengageDistance = 6f;

    [Header("크기 (0이면 그대로)")]
    [Tooltip("크리처 크기 — 씬 배치 크기 대신")]
    [SerializeField] private float creatureScale;
    [Tooltip("몸 충돌체(캡슐)·NavMeshAgent 높이 상한(m) — 문틀보다 키가 크면 문 위 벽에 걸려 못 지나감")]
    [SerializeField] private float maxBodyHeight;

    private bool armed;
    private bool fired;
    private readonly List<Vector3> cuePath = new List<Vector3>();

    private void Start()
    {
        armed = sealIndex < 0 || DemoSealProgress.Has(sealIndex);
        DemoSealProgress.Collected += OnCollected;
        FitCreatureBody();
    }

    // 크기·충돌체 높이를 문을 지나갈 수 있게 맞춤 (프리팹은 그대로, 이 연출의 크리처만)
    private void FitCreatureBody()
    {
        if (creature == null)
        {
            return;
        }

        Transform body = creature.transform;
        if (creatureScale > 0f)
        {
            body.localScale = Vector3.one * creatureScale;
        }

        if (maxBodyHeight <= 0f)
        {
            return;
        }

        float scaleY = Mathf.Max(0.01f, body.lossyScale.y);
        float maxLocal = maxBodyHeight / scaleY;
        foreach (CapsuleCollider capsule in creature.GetComponents<CapsuleCollider>())
        {
            if (!capsule.isTrigger && capsule.direction == 1 && capsule.height > maxLocal)
            {
                float bottom = capsule.center.y - capsule.height * 0.5f;
                capsule.height = maxLocal;
                capsule.radius = Mathf.Min(capsule.radius, maxLocal * 0.5f);
                capsule.center = new Vector3(capsule.center.x, bottom + maxLocal * 0.5f, capsule.center.z);
            }
        }

        NavMeshAgent agent = creature.GetComponent<NavMeshAgent>();
        if (agent != null && agent.height > maxLocal)
        {
            agent.height = maxLocal;
        }
    }

    private AudioClip ResolveCueClip()
    {
        if (cueClip != null || creature == null)
        {
            return cueClip;
        }

        NonFaceGhostChaseScream scream = creature.GetComponentInChildren<NonFaceGhostChaseScream>(true);
        if (scream != null && scream.Clip != null)
        {
            return scream.Clip;
        }

        XenosauridController xeno = creature.GetComponent<XenosauridController>();
        if (xeno != null && xeno.TauntClip != null)
        {
            return xeno.TauntClip;
        }

        MaskGhostSong song = creature.GetComponentInChildren<MaskGhostSong>(true);
        return song != null ? song.Clip : null;
    }

    private void OnDestroy()
    {
        DemoSealProgress.Collected -= OnCollected;
    }

    private void OnCollected(int index)
    {
        if (index == sealIndex)
        {
            armed = true;
        }
    }

    private void Update()
    {
        if (!armed || fired)
        {
            return;
        }

        if (triggerZone == null || triggerZone.IsPlayerInside)
        {
            fired = true;
            StartCoroutine(Run());
        }
    }

    private IEnumerator Run()
    {
        Debug.Log("[Demo] 기습 연출 " + name);
        yield return new WaitForSeconds(delay);
        if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            yield break;
        }

        // 등장 지점(안 보이는 곳)과 예고 소리 길을 정하고, 소리가 먼저 다가옴
        GameObject playerGo = GameObject.FindWithTag("Player");
        Transform playerT = playerGo != null ? playerGo.transform : null;
        Vector3 spawnPos = spawn != null ? spawn.position : (creature != null ? creature.transform.position : transform.position);
        Vector3 appear = spawnPos;
        ApproachingSoundCue cue = null;
        bool revealed = false;
        if (revealInSight && creature != null && !creature.gameObject.activeSelf
            && DemoCreatureStaging.TryPickRevealPoint(spawnPos, playerT, creature.transform, revealMinDistance, revealMaxDistance, revealDistance, out Vector3 revealPoint))
        {
            revealed = true;
            appear = revealPoint;
            Debug.Log("[Demo] " + name + " 멀리서 보여주기 — 등장 " + appear.ToString("F1") + ", 거리 " + Vector3.Distance(appear, playerT.position).ToString("F0") + "m");
        }
        else if (approachCue && creature != null && !creature.gameObject.activeSelf)
        {
            appear = DemoCreatureStaging.Plan(spawnPos, playerT, creature.transform, approachDistance, cueStartDistance, cuePath);
            cue = ApproachingSoundCue.Play(ResolveCueClip(), cuePath, cueSeconds, cueVolume, false, cueGap);
            Debug.Log("[Demo] " + name + " 예고 소리 — 등장 " + appear.ToString("F1") + (Vector3.Distance(appear, spawnPos) > 0.5f ? " (정해 둔 자리 대신)" : ""));
            yield return new WaitForSeconds(cueSeconds);
            if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
            {
                yield break;
            }

            // 기다리는 동안 플레이어가 움직여 등장 지점이 보이게 됐으면 다시 고름
            if (DemoCreatureStaging.IsVisible(appear, playerT, creature.transform))
            {
                appear = DemoCreatureStaging.Plan(spawnPos, playerT, creature.transform, approachDistance, cueStartDistance, cuePath);
            }
        }

        // 등장 자리가 문 바로 안쪽(정해 둔 자리)일 때만 문을 두드리고 부숨 — 다른 곳에서 나오면 AI가 알아서 문을 지나감
        bool atAuthoredSpawn = !revealed && Vector3.Distance(appear, spawnPos) < 1.5f;

        // 멀리서 보여줄 땐 대사를 플레이어가 크리처를 실제로 봤을 때 띄움 (HoldInSight)
        string line = revealed && !string.IsNullOrEmpty(revealLine) ? revealLine : beforeLine;
        if (!revealed && !string.IsNullOrEmpty(line))
        {
            SubtitleUI.ShowForDuration("나", line, 2f);
        }

        if (creature != null && !creature.gameObject.activeSelf)
        {
            Quaternion facing = spawn != null ? spawn.rotation : creature.transform.rotation;
            if (playerT != null && !atAuthoredSpawn)
            {
                Vector3 look = playerT.position - appear;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    facing = Quaternion.LookRotation(look);
                }
            }

            creature.transform.SetPositionAndRotation(appear, facing);
            creature.gameObject.SetActive(true);
            yield return null;
            yield return null;
            if (creature.Agent != null && creature.Agent.isOnNavMesh)
            {
                creature.Agent.Warp(appear);
            }
        }

        if (cue != null)
        {
            cue.Finish(); // 이제 크리처 자신의 소리가 이어받음
        }

        if (revealed && creature != null)
        {
            yield return HoldInSight(appear, playerT, line);
            if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
            {
                yield break;
            }
        }

        foreach (DoorClick door in openDoors)
        {
            if (door != null)
            {
                DemoDoorOpener.Open(door, true);
            }
        }

        if (breakDoors.Length > 0 && atAuthoredSpawn)
        {
            // 문 뒤에서 두드림 → 부숨
            // 두드리는 소리가 비어 있으면 문 부서지는 기본 소리를 작게 써서 '쾅쾅'이 들리게 (예전엔 소리 없이 문만 부서졌음)
            bool isXeno = creature != null && creature.GetComponent<XenosauridController>() != null;
            AudioClip bang = bangClip != null ? bangClip : Resources.Load<AudioClip>(isXeno ? "Sound/XenoDoorThud" : "Sound/Door_Break_Bang");
            float bangVolume = bangClip != null ? 1f : 0.45f;
            float t = 0f;
            int bangs = 0;
            while (t < breakDelay)
            {
                if (audioSource != null && bang != null && t >= bangs * 0.4f)
                {
                    audioSource.PlayOneShot(bang, bangVolume);
                    bangs++;
                }

                t += Time.deltaTime;
                yield return null;
            }

            foreach (DoorBrokenTest door in breakDoors)
            {
                if (door != null && !door.IsBroken())
                {
                    door.BreakByEnemy(creature != null ? creature.transform.position : door.transform.position, breakClip);
                }
            }
        }

        StartChase(true);
        if (!string.IsNullOrEmpty(afterLine))
        {
            yield return new WaitForSeconds(0.6f);
            SubtitleUI.ShowForDuration("나", afterLine, 2f);
        }

        // 사냥: 일정 시간 플레이어 위치를 계속 알려 줌 (숨으면 일반 AI 판정에 맡김) → leaveAfterHide면 숨거나 놓친 뒤 돌아가서 사라짐
        yield return HuntAndLeave(appear, spawnPos);
    }

    private static bool PlayerGone => PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead;

    private static bool IsPlayerHiding(Transform player)
    {
        PlayerHidingController hiding = player != null ? player.GetComponent<PlayerHidingController>() : null;
        return hiding != null && hiding.isHiding;
    }

    private IEnumerator HuntAndLeave(Vector3 appearedAt, Vector3 spawnPos)
    {
        GameObject playerGo = GameObject.FindWithTag("Player");
        Transform player = playerGo != null ? playerGo.transform : null;

        while (true)
        {
            float end = Time.time + huntSeconds;
            float hidden = 0f;
            while (Time.time < end && creature != null && creature.isActiveAndEnabled)
            {
                yield return new WaitForSeconds(0.5f);
                if (PlayerGone)
                {
                    yield break;
                }

                hidden = IsPlayerHiding(player) ? hidden + 0.5f : 0f;
                if (leaveAfterHide && hidden >= searchSeconds)
                {
                    break;
                }

                StartChase(false);
            }

            if (!leaveAfterHide || creature == null || !creature.isActiveAndEnabled)
            {
                yield break;
            }

            // 사냥 시간이 끝났어도 아직 쫓는 중이면 — 숨어서 버티거나, 놓쳐서 순찰로 돌아갈 때까지 기다림
            while (hidden < searchSeconds && creature.isActiveAndEnabled
                && (creature.RuntimeState.CurrentState == EnemyBase.State.Chase || creature.RuntimeState.CurrentState == EnemyBase.State.Investigate))
            {
                yield return new WaitForSeconds(0.5f);
                if (PlayerGone)
                {
                    yield break;
                }

                hidden = IsPlayerHiding(player) ? hidden + 0.5f : 0f;
            }

            if (!creature.isActiveAndEnabled)
            {
                yield break;
            }

            // 돌아갈 자리: 나타났던 곳. 그곳이 플레이어(숨은 곳) 바로 옆이면 원래 정해 둔 등장 자리로
            Vector3 home = appearedAt;
            if (player != null && Vector3.Distance(home, player.position) < 8f)
            {
                home = spawnPos;
            }

            bool reengage = false;
            yield return ReturnAndVanish(home, player, r => reengage = r);
            if (!reengage || PlayerGone)
            {
                yield break;
            }

            StartChase(true); // 돌아가다 들킴 → 다시 사냥부터
        }
    }

    // AI를 멈추고 home까지 걸어간 뒤 사라짐. 도중에 숨는 곳에서 나온 플레이어가 가까이 보이면 다시 쫓도록 done(true)
    private IEnumerator ReturnAndVanish(Vector3 home, Transform player, System.Action<bool> done)
    {
        EnemyState s = creature.RuntimeState;
        NavMeshAgent agent = creature.Agent;
        Debug.Log("[Demo] " + creature.name + " 수색 끝 → 나타났던 자리로 돌아감 " + home.ToString("F1"));

        creature.Combat.ChangeState(EnemyBase.State.Patrol); // 비명·추격 연출 끔
        s.PlayerDeadLogged = true; // 돌아가는 동안엔 몸에 닿아도 잡지 않음 (EnemySense가 잡기 전에 확인하는 값)
        creature.enabled = false;  // AI가 다시 숨은 곳으로 가지 않게 직접 걷게 함
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.autoBraking = true;
            agent.speed = leaveWalkSpeed > 0f ? leaveWalkSpeed : (creature.Data != null && creature.Data.moveSpeed > 0f ? creature.Data.moveSpeed : 1.5f);
            agent.SetDestination(home);
        }

        creature.SnapToPatrolAnimation(false); // 걷는 모습

        float start = Time.time;
        while (Time.time - start < 40f)
        {
            if (PlayerGone)
            {
                done(false);
                yield break;
            }

            if (player != null && !IsPlayerHiding(player)
                && Vector3.Distance(player.position, creature.transform.position) < reengageDistance
                && DemoCreatureStaging.IsVisible(creature.transform.position, player, creature.transform))
            {
                Debug.Log("[Demo] " + creature.name + " 돌아가다 플레이어 발견 → 다시 추격");
                s.PlayerDeadLogged = false;
                creature.enabled = true;
                done(true);
                yield break;
            }

            if (agent == null || !agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid
                || (!agent.pathPending && agent.remainingDistance <= 0.8f))
            {
                break;
            }

            yield return null;
        }

        creature.gameObject.SetActive(false);
        s.PlayerDeadLogged = false;
        Debug.Log("[Demo] " + creature.name + " 사라짐");
        done(false);
    }

    private void StartChase(bool first)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (creature == null || player == null || PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            return;
        }

        EnemyState s = creature.RuntimeState;
        if (s == null || creature.Combat == null)
        {
            return;
        }

        PlayerHidingController hiding = player.GetComponent<PlayerHidingController>();
        if (!first && hiding != null && hiding.isHiding)
        {
            return; // 숨었으면 더 알려 주지 않음 — 숨기로 따돌릴 수 있게
        }

        s.LastKnownPosition = player.transform.position;
        if (first || s.CurrentState != EnemyBase.State.Chase)
        {
            creature.Combat.ChangeState(EnemyBase.State.Chase);
        }

        creature.Combat.ArmChaseGraceTimer();
        if (chaseEffectOnStart)
        {
            PlayerChaseStateLink link = player.GetComponent<PlayerChaseStateLink>();
            if (link != null)
            {
                link.MarkSighted(creature); // 직접 보기 전이라도 추격 연출 켬 (Chase를 벗어나면 평소처럼 꺼짐)
            }
        }

        if (first)
        {
            Debug.Log("[Demo] " + creature.name + " 추격 시작");
        }
    }

    // 멀리 보이는 곳에 멈춰 서서 플레이어를 바라봄 → 플레이어가 보면(또는 시간 초과·너무 가까워지면) 풀어 줌
    private IEnumerator HoldInSight(Vector3 at, Transform player, string line)
    {
        NavMeshAgent agent = creature.Agent;
        creature.enabled = false; // AI 정지 (감지·이동 없음) — Start에서 초기화는 이미 끝남
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        creature.SnapToPatrolAnimation(true); // 걷던 자세 그대로 얼어붙은 채 서 있음

        Light glow = null;
        var flicker = new List<Light>();
        var baseIntensity = new List<float>();
        if (revealLight)
        {
            glow = new GameObject("RevealGlow").AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.62f, 0.72f, 1f);
            glow.range = 4.5f;
            glow.intensity = 1.6f;
            glow.shadows = LightShadows.None;
            glow.transform.position = at + Vector3.up * 2.3f;

            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != glow && l.enabled && l.type != LightType.Directional && Vector3.Distance(l.transform.position, at) < 9f)
                {
                    flicker.Add(l);
                    baseIntensity.Add(l.intensity);
                }
            }
        }

        bool lineShown = false;
        float looked = 0f;
        float start = Time.time;
        while (Time.time - start < revealMaxWait && !PlayerDeathDebug.IsDying && !PlayerDeathDebug.IsDead)
        {
            if (player != null)
            {
                Vector3 look = player.position - creature.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    creature.transform.rotation = Quaternion.RotateTowards(creature.transform.rotation, Quaternion.LookRotation(look), 180f * Time.deltaTime);
                }

                if (Vector3.Distance(player.position, at) < revealBreakDistance)
                {
                    break;
                }
            }

            if (DemoCreatureStaging.IsOnScreen(at, player, creature.transform))
            {
                looked += Time.deltaTime;
                if (!lineShown && !string.IsNullOrEmpty(line))
                {
                    lineShown = true;
                    SubtitleUI.ShowForDuration("나", line, 2f);
                }

                if (looked >= revealLookSeconds)
                {
                    break;
                }
            }

            // 지직거림: 가끔 확 어두워졌다 돌아옴
            bool dip = Random.value < 0.18f;
            if (glow != null)
            {
                glow.intensity = dip ? 0.15f : Random.Range(1.2f, 1.8f);
            }

            for (int i = 0; i < flicker.Count; i++)
            {
                if (flicker[i] != null)
                {
                    flicker[i].intensity = baseIntensity[i] * (dip ? Random.Range(0f, 0.2f) : Random.Range(0.7f, 1f));
                }
            }

            yield return null;
        }

        for (int i = 0; i < flicker.Count; i++)
        {
            if (flicker[i] != null)
            {
                flicker[i].intensity = baseIntensity[i];
            }
        }

        if (glow != null)
        {
            Destroy(glow.gameObject);
        }

        // 풀어 줌 — 비명은 Chase 상태가 되면 크리처 소리(NonFaceGhostChaseScream)가 냄
        if (creature.Anim != null)
        {
            creature.Anim.speed = 1f;
        }

        creature.enabled = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        Debug.Log("[Demo] " + name + " 멈춤 해제 (화면에 담긴 시간 " + looked.ToString("F1") + "초)");
    }
}
