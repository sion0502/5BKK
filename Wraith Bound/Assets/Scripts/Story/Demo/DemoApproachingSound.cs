using System.Collections;
using UnityEngine;

/// <summary>
/// 연출 전에 '무언가 다가오는' 소리 (2026-11-19 사용자 요청 — ③ 탈춤귀신 노래가 미리 다가오게).
/// 플레이어가 triggerCenter에서 triggerRadius 안으로 들어오면, from → to로 seconds 동안 움직이는 3D 소리(반복)를 틂 —
/// 플레이어에게 붙이지 않아 거리·방향이 느껴지고 점점 가까워짐. to에 닿으면 그 자리에서 계속.
/// sealIndex 봉인(부적)이 기록되면 stopDelay 뒤(귀신이 실제로 나타날 즈음) 서서히 꺼짐 — 그다음은 귀신 자체 소리.
/// </summary>
public class DemoApproachingSound : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private Transform from;
    [SerializeField] private Transform to;
    [SerializeField] private float seconds = 10f;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float minDistance = 4f;
    [SerializeField] private float maxDistance = 60f;

    [Header("시작")]
    [SerializeField] private Transform triggerCenter;
    [SerializeField] private float triggerRadius = 12f;

    [Header("끝")]
    [SerializeField] private int sealIndex = -1;
    [SerializeField] private float stopDelay = 2.7f;
    [SerializeField] private float fadeOut = 0.8f;

    private AudioSource source;
    private Transform player;
    private bool started;
    private bool stopping;

    private void Start()
    {
        DemoSealProgress.Collected += OnCollected;
    }

    private void OnDestroy()
    {
        DemoSealProgress.Collected -= OnCollected;
    }

    private void OnCollected(int index)
    {
        if (index != sealIndex || stopping)
        {
            return;
        }

        stopping = true;
        if (!started)
        {
            enabled = false; // 다가오기 전에 이미 태웠으면 소리 없이 끝
            return;
        }

        StartCoroutine(StopAfter());
    }

    private void Update()
    {
        if (started || stopping || clip == null || from == null || to == null || triggerCenter == null)
        {
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            player = p != null ? p.transform : null;
            if (player == null)
            {
                return;
            }
        }

        if ((player.position - triggerCenter.position).sqrMagnitude <= triggerRadius * triggerRadius)
        {
            started = true;
            StartCoroutine(Approach());
        }
    }

    private IEnumerator Approach()
    {
        var go = new GameObject("ApproachingSound");
        go.transform.SetParent(transform, false);
        go.transform.position = from.position;
        source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.Play();
        Debug.Log("[Demo] 다가오는 소리 시작 " + clip.name);

        for (float t = 0f; t < seconds && !stopping; t += Time.deltaTime)
        {
            go.transform.position = Vector3.Lerp(from.position, to.position, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }

        if (!stopping)
        {
            go.transform.position = to.position;
        }
    }

    private IEnumerator StopAfter()
    {
        yield return new WaitForSeconds(stopDelay);
        if (source == null)
        {
            yield break;
        }

        float start = source.volume;
        for (float t = 0f; t < fadeOut; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(start, 0f, t / fadeOut);
            yield return null;
        }

        Destroy(source.gameObject);
    }
}
