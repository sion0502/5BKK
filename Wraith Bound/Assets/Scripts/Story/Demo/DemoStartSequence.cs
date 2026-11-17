using System.Collections;
using UnityEngine;

/// <summary>
/// 시연 맵 연출 ① — 시작 방.
///  · 방에 들어서면(roomZone) 가운데 통로·북쪽 복도 천장 형광등이 입구 쪽부터 하나씩 지직거리며 켜짐 → "…누가 불을 켠 거지?"
/// (로비를 가로지르는 Xenosaurid 연출은 2026-11-09 뺌)
/// </summary>
public class DemoStartSequence : MonoBehaviour
{
    [SerializeField] private StoryZone roomZone;
    [Tooltip("입구 쪽부터 켜지는 순서")]
    [SerializeField] private PrologueCeilingLight[] lights = { };
    [SerializeField] private float lightInterval = 0.45f;
    [SerializeField] private string lightsLine = "…누가 불을 켠 거지?";

    private void Start()
    {
        foreach (PrologueCeilingLight l in lights)
        {
            if (l != null)
            {
                l.Set(false);
            }
        }

        if (roomZone != null)
        {
            roomZone.Entered += () => StartCoroutine(LightsOn());
        }
    }

    private IEnumerator LightsOn()
    {
        Debug.Log("[Demo] ① 형광등 순차 점등");
        foreach (PrologueCeilingLight l in lights)
        {
            if (l == null)
            {
                continue;
            }

            l.PlayFlickerSound();
            for (int i = 0; i < 3; i++)
            {
                l.Set(true);
                yield return new WaitForSeconds(0.05f + Random.value * 0.05f);
                l.Set(false);
                yield return new WaitForSeconds(0.04f + Random.value * 0.06f);
            }

            l.SwitchOn();
            yield return new WaitForSeconds(lightInterval);
        }

        yield return new WaitForSeconds(0.6f);
        SubtitleUI.ShowForDuration("나", lightsLine, 2.6f);
    }
}
