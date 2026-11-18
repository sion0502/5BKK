using System.Collections;
using UnityEngine;

/// <summary>
/// 시연 맵 '숨겨 둔 크리처가 들이닥치는' 연출 공용 (④ 얼굴 없는 귀신, ⑤ Xenosaurid).
///  · 준비(arm): sealIndex 봉인(부적)이 기록되면. sealIndex &lt; 0 이면 처음부터 준비됨.
///  · 발동: 준비된 뒤 triggerZone에 플레이어가 들어오면(없으면 준비 즉시) → delay 뒤
///    대사 → 비활성으로 숨겨 둔 크리처를 spawn에서 켬 → openDoors 열기 / breakDoors는 breakDelay 뒤 부숨(쾅) → 추격 시작.
///  · 추격은 일반 AI(EnemyBase) — 놓치면 수색·순찰. EnemyBase는 수정하지 않음(internal 상태 변경만 호출).
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

    private bool armed;
    private bool fired;

    private void Start()
    {
        armed = sealIndex < 0 || DemoSealProgress.Has(sealIndex);
        DemoSealProgress.Collected += OnCollected;
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

        if (!string.IsNullOrEmpty(beforeLine))
        {
            SubtitleUI.ShowForDuration("나", beforeLine, 2f);
        }

        if (creature != null && !creature.gameObject.activeSelf)
        {
            if (spawn != null)
            {
                creature.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            }

            creature.gameObject.SetActive(true);
            DemoRoamingSpawner.RetireSameKind(creature); // 같은 종류 배회 괴물은 사라짐 (중복 방지)
            yield return null;
            yield return null;
        }

        foreach (DoorClick door in openDoors)
        {
            if (door != null)
            {
                DemoDoorOpener.Open(door, true);
            }
        }

        if (breakDoors.Length > 0)
        {
            // 문 뒤에서 두드림 → 부숨
            float t = 0f;
            int bangs = 0;
            while (t < breakDelay)
            {
                if (audioSource != null && bangClip != null && t >= bangs * 0.4f)
                {
                    audioSource.PlayOneShot(bangClip);
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

        // 사냥: 일정 시간 플레이어 위치를 계속 알려 줌 (숨으면 일반 AI 판정에 맡김)
        float end = Time.time + huntSeconds;
        while (Time.time < end && creature != null && creature.isActiveAndEnabled)
        {
            yield return new WaitForSeconds(0.5f);
            StartChase(false);
        }
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
        if (first)
        {
            Debug.Log("[Demo] " + creature.name + " 추격 시작");
        }
    }
}
