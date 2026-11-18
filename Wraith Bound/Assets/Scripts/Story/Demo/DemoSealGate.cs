using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 시연 맵 출구 대문 — 부적으로 봉인돼 있음. 맵의 부적(DemoTalisman)을 한 장 태울 때마다 대문의 봉인 부적도 한 장 타 없어지고
/// 붉은 봉인 빛이 약해짐 → 셋 다 타면 봉인이 풀려 문이 열림 → 그 뒤는 팀원 ExitEventCube가 허브로 보냄.
/// 팀원 SealTest(G키로 구슬 깨고 문 열기)는 수정하지 않고 끈 뒤 문 여는 코루틴·소리만 리플렉션으로 빌려 씀.
/// 원래 있던 봉인 구슬·에너지 줄기(hiddenObjects)는 시작 때 숨김.
/// </summary>
public class DemoSealGate : MonoBehaviour
{
    [SerializeField] private SealTest gate;
    [SerializeField] private TalismanBurnEffect[] gateTalismans = { };
    [SerializeField] private GameObject[] hiddenObjects = { };
    [SerializeField] private Light sealLight;
    [SerializeField] private float sealIntensity = 3f;
    [SerializeField] private StoryZone approachZone;
    [SerializeField] private float burnDelay = 0.4f;
    [SerializeField] private float openDelay = 1.2f;

    [SerializeField] private string openLine = "…열렸어. 나가자.";

    private bool opened;
    private float lastHint = -99f;
    private float lightTarget;

    private void Start()
    {
        if (gate != null)
        {
            gate.enabled = false; // G키 디버그 끔 — 부적으로만 열림
        }

        foreach (GameObject o in hiddenObjects)
        {
            if (o != null)
            {
                o.SetActive(false);
            }
        }

        for (int i = 0; i < DemoSealProgress.Count && i < gateTalismans.Length; i++)
        {
            if (gateTalismans[i] != null)
            {
                gateTalismans[i].BurnInstant();
            }
        }

        lightTarget = SealStrength(DemoSealProgress.Count);
        if (sealLight != null)
        {
            sealLight.intensity = lightTarget;
        }

        DemoSealProgress.Collected += OnCollected;
        if (DemoSealProgress.Count >= DemoSealProgress.Total)
        {
            StartCoroutine(Open());
        }
    }

    private void OnDestroy()
    {
        DemoSealProgress.Collected -= OnCollected;
    }

    private float SealStrength(int burnt)
    {
        return sealIntensity * Mathf.Clamp01(1f - burnt / (float)DemoSealProgress.Total);
    }

    private void Update()
    {
        if (sealLight != null)
        {
            // 봉인 빛: 남은 봉인만큼, 약해질수록 더 불안하게 깜빡임
            float unstable = 1f - lightTarget / Mathf.Max(0.01f, sealIntensity);
            float flicker = 1f - unstable * 0.6f * Mathf.PerlinNoise(Time.time * 6f, 0.7f);
            sealLight.intensity = Mathf.MoveTowards(sealLight.intensity, lightTarget * flicker, Time.deltaTime * sealIntensity * 2f);
            sealLight.enabled = sealLight.intensity > 0.01f;
        }

        if (!opened && approachZone != null && approachZone.IsPlayerInside && Time.time - lastHint > 6f)
        {
            lastHint = Time.time;
            int left = DemoSealProgress.Total - DemoSealProgress.Count;
            ScreenMessageUI.Show("부적으로 봉인돼 있다 — 남은 봉인 " + left + "장", 2.6f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 테스트: G = 아직 안 태운 다음 부적을 태운 것으로 처리 (SealTest의 G키 대신)
        if (Input.GetKeyDown(KeyCode.G))
        {
            for (int i = 0; i < 32; i++)
            {
                if (!DemoSealProgress.Has(i))
                {
                    DemoSealProgress.Collect(i);
                    break;
                }
            }
        }
#endif
    }

    private void OnCollected(int index)
    {
        if (DemoSealProgress.Count <= DemoSealProgress.Total)
        {
            StartCoroutine(BurnNext(DemoSealProgress.Count));
        }
    }

    private IEnumerator BurnNext(int count)
    {
        yield return new WaitForSeconds(burnDelay);
        int i = count - 1;
        if (i >= 0 && i < gateTalismans.Length && gateTalismans[i] != null)
        {
            gateTalismans[i].Burn();
        }

        lightTarget = SealStrength(count);
        if (count >= DemoSealProgress.Total)
        {
            yield return new WaitForSeconds(openDelay + 2.2f); // 마지막 부적이 다 탄 뒤
            yield return Open();
        }
    }

    private IEnumerator Open()
    {
        if (opened || gate == null)
        {
            yield break;
        }

        opened = true;
        lightTarget = 0f;
        foreach (TalismanBurnEffect t in gateTalismans)
        {
            if (t != null && !t.IsBurnt && !t.IsBurning)
            {
                t.BurnInstant();
            }
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SealTest).GetField("opened", flags)?.SetValue(gate, true);
        typeof(SealTest).GetMethod("PlayDoorOpenedSound", flags)?.Invoke(gate, null);
        if (gate.leftDoor != null && gate.rightDoor != null && typeof(SealTest).GetMethod("OpenDoors", flags)?.Invoke(gate, null) is IEnumerator doors)
        {
            StartCoroutine(doors);
        }

        Debug.Log("[Demo] 출구 봉인 해제 — 문 열림");
        ScreenMessageUI.Show("출구의 봉인이 모두 풀렸다", 3f);
        yield return new WaitForSeconds(1.2f);
        SubtitleUI.ShowForDuration("나", openLine, 2.4f);
    }
}
