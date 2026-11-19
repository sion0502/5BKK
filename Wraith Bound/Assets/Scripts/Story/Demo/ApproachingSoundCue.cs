using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시연 연출 예고 소리 — 괴물이 나타나기 전에, 소리만 먼 곳에서 등장 지점까지 길을 따라 다가옴.
///  · 거리 감쇠·벽 너머 먹먹함·잔향·좌우 방향은 SpatialSoundFx(괴물 소리와 같은 규칙)가 처리.
///  · 반복형(비명·포효): 끝나면 gap 뒤 다시. 루프형(노래): 계속 재생하며 점점 커짐.
///  · Finish(): 괴물이 실제로 나타날 때 호출 — 루프는 서서히 줄이고, 반복형은 지금 소리까지만 내고 사라짐.
/// </summary>
public class ApproachingSoundCue : MonoBehaviour
{
    private readonly List<Vector3> path = new List<Vector3>();
    private float totalLength;
    private float seconds;
    private float elapsed;
    private AudioSource source;
    private AudioClip clip;
    private float volume;
    private bool loop;
    private Vector2 gapRange;
    private float gapTimer;
    private bool finishing;
    private float level;
    private const float FadeInSeconds = 0.8f;
    private const float LoopFadeOutSeconds = 1.2f;

    /// <param name="points">소리가 지나갈 길 (먼 곳 → 등장 지점)</param>
    /// <param name="seconds">끝 지점까지 가는 시간</param>
    /// <param name="loop">true면 계속 재생(노래), false면 gap 간격으로 반복(비명·포효)</param>
    public static ApproachingSoundCue Play(AudioClip clip, IList<Vector3> points, float seconds, float volume, bool loop, Vector2 gap)
    {
        if (clip == null || points == null || points.Count == 0)
        {
            return null;
        }

        GameObject go = new GameObject("ApproachingSoundCue_" + clip.name);
        go.transform.position = points[0] + Vector3.up * 1.4f;
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.volume = 0f;
        go.AddComponent<SpatialSoundFx>(); // AudioSource가 먼저 있어야 3D 규칙이 적용됨

        ApproachingSoundCue cue = go.AddComponent<ApproachingSoundCue>();
        cue.Init(source, clip, points, seconds, volume, loop, gap);
        return cue;
    }

    private void Init(AudioSource src, AudioClip c, IList<Vector3> points, float duration, float vol, bool isLoop, Vector2 gap)
    {
        source = src;
        clip = c;
        seconds = Mathf.Max(0.1f, duration);
        volume = Mathf.Clamp01(vol);
        loop = isLoop;
        gapRange = new Vector2(Mathf.Max(0f, gap.x), Mathf.Max(gap.x, gap.y));
        path.Clear();
        path.AddRange(points);
        totalLength = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            totalLength += Vector3.Distance(path[i - 1], path[i]);
        }

        source.loop = loop;
        source.time = 0f;
        source.Play();
    }

    /// <summary>괴물이 실제로 나타날 때 — 소리를 넘겨주고 정리.</summary>
    public void Finish()
    {
        finishing = true;
    }

    private void Update()
    {
        if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / seconds);
        // 처음엔 천천히, 갈수록 빨라짐 — 다가오는 느낌
        transform.position = PointAt(t * t * (3f - 2f * t)) + Vector3.up * 1.4f;

        if (finishing)
        {
            if (loop)
            {
                level = Mathf.MoveTowards(level, 0f, Time.deltaTime / LoopFadeOutSeconds);
                source.volume = volume * level;
                if (level <= 0f)
                {
                    Destroy(gameObject);
                }
            }
            else if (!source.isPlaying)
            {
                Destroy(gameObject);
            }

            return;
        }

        level = Mathf.MoveTowards(level, 1f, Time.deltaTime / FadeInSeconds);
        source.volume = volume * level;

        if (!loop && !source.isPlaying)
        {
            gapTimer -= Time.deltaTime;
            if (gapTimer <= 0f)
            {
                source.pitch = Random.Range(0.94f, 1.06f);
                source.time = 0f;
                source.Play();
                gapTimer = Random.Range(gapRange.x, gapRange.y);
            }
        }
        else if (!loop)
        {
            gapTimer = Random.Range(gapRange.x, gapRange.y);
        }
    }

    private Vector3 PointAt(float t)
    {
        if (path.Count == 1 || totalLength <= 0.001f)
        {
            return path[path.Count - 1];
        }

        float target = t * totalLength;
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            if (target <= seg || i == path.Count - 1)
            {
                return Vector3.Lerp(path[i - 1], path[i], seg <= 0.001f ? 1f : Mathf.Clamp01(target / seg));
            }

            target -= seg;
        }

        return path[path.Count - 1];
    }
}
