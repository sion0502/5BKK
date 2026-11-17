using System.Collections;
using UnityEngine;

/// <summary>
/// 시연 맵 연출 ② — 병동.
///  · 병동 큰 홀에 들어서면(zone) 북쪽 복도 형광등이 켜지고, 그 불빛을 등진 채 캠코더 화면으로만 보이는 그림자 사람(CamcorderOnlyApparition)이 북쪽 복도(수술실 쪽)를 가리킨 채 서 있음.
///    맨눈으론 안 보이니 잠시 뒤 "캠코더로 비춰 볼까" 독백(이미 뷰파인더를 보고 있으면 생략).
///  · 뷰파인더로 담으면 깜빡이며 사라지고 → 독백 → 가리키던 복도의 문이 저절로 열림.
/// 눈 카메라에서 CamcorderOnly 레이어를 빼는 것도 여기서 (프롤로그는 씬 빌더가 함).
/// </summary>
public class DemoWardSequence : MonoBehaviour
{
    [SerializeField] private StoryZone zone;
    [SerializeField] private CamcorderOnlyApparition figure;
    [Tooltip("그림자 사람 뒤 형광등 — 들어서면 지직거리며 켜져 실루엣을 드러냄(맨눈엔 빈 곳에 불만 켜진 것)")]
    [SerializeField] private PrologueCeilingLight[] backLights = { };
    [SerializeField] private float hintDelay = 2.5f;
    [SerializeField] private string hintLine = "…기분 나빠. 캠코더로 한번 비춰 볼까.";
    [SerializeField] private string seenLine = "…방금 그거… 저쪽을 가리켰어.";
    [Tooltip("사라진 뒤 저절로 열리는 문 (가리키던 복도)")]
    [SerializeField] private DoorClick[] doorsToOpen = { };
    [SerializeField] private float doorDelay = 1.6f;

    private bool seen;

    private void Start()
    {
        HideLayerFromEyes();
        foreach (PrologueCeilingLight l in backLights)
        {
            if (l != null)
            {
                l.Set(false);
            }
        }

        if (figure != null)
        {
            figure.HideSilently();
            figure.Seen += () => StartCoroutine(OnSeen());
        }

        if (zone != null)
        {
            zone.Entered += () => StartCoroutine(OnEnter());
        }
    }

    private static void HideLayerFromEyes()
    {
        int layer = LayerMask.NameToLayer(CamcorderOnlyApparition.LayerName);
        GameObject player = GameObject.FindWithTag("Player");
        if (layer < 0 || player == null)
        {
            return;
        }

        foreach (Camera cam in player.GetComponentsInChildren<Camera>(true))
        {
            if (cam.targetTexture == null)
            {
                cam.cullingMask &= ~(1 << layer);
            }
        }
    }

    private IEnumerator OnEnter()
    {
        if (figure == null)
        {
            yield break;
        }

        Debug.Log("[Demo] ② 병동 — 그림자 사람 등장");
        foreach (PrologueCeilingLight l in backLights)
        {
            if (l == null)
            {
                continue;
            }

            l.PlayFlickerSound();
            for (int i = 0; i < 4; i++)
            {
                l.Set(i % 2 == 0);
                yield return new WaitForSeconds(0.05f + Random.value * 0.08f);
            }

            l.SwitchOn();
            yield return new WaitForSeconds(0.25f);
        }

        figure.Show();
        yield return new WaitForSeconds(hintDelay);
        CamcorderController camcorder = FindFirstObjectByType<CamcorderController>();
        if (!seen && (camcorder == null || !camcorder.IsViewfinderActive))
        {
            SubtitleUI.ShowForDuration("나", hintLine, 3f);
        }
    }

    private IEnumerator OnSeen()
    {
        seen = true;
        Debug.Log("[Demo] ② 그림자 사람을 캠코더로 봄");
        yield return new WaitForSeconds(0.7f);
        SubtitleUI.ShowForDuration("나", seenLine, 2.8f);
        yield return new WaitForSeconds(doorDelay);
        foreach (DoorClick door in doorsToOpen)
        {
            if (door != null && !door.IsOpen())
            {
                DemoDoorOpener.Open(door, true);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
