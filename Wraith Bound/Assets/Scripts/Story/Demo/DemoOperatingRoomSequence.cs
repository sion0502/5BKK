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

    private bool started;
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

        yield return new WaitForSeconds(ghostDelay);

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
            if (ghostSpawn != null)
            {
                ghost.transform.SetPositionAndRotation(ghostSpawn.position, ghostSpawn.rotation);
            }

            ghost.gameObject.SetActive(true);
            DemoRoamingSpawner.RetireSameKind(ghost); // 같은 종류 배회 괴물은 사라짐 (중복 방지)
            yield return null;
            yield return null;
        }

        ReleaseGhost();
        yield return new WaitForSeconds(0.9f);
        if (!string.IsNullOrEmpty(runLine))
        {
            SubtitleUI.ShowForDuration("나", runLine, 2f);
        }
    }

    private void ReleaseGhost()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (ghost == null || player == null || PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            return;
        }

        if (ghostSpawn != null && ghost.Agent != null)
        {
            ghost.Agent.Warp(ghostSpawn.position);
            ghost.transform.rotation = ghostSpawn.rotation;
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
