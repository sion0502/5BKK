using System.Collections;
using UnityEngine;

/// <summary>
/// 메인 메뉴 BGM (Detuned Pulse). 메뉴가 떠 있는 동안 재생하고, 곡이 끝나면 3초 쉬었다가 다시 재생(반복).
/// '새로운 게임' / '이어서 하기'로 게임이 시작되면 MenuMusic.Stop()으로 서서히 꺼짐.
/// 소리 파일은 Resources/GameSfx.asset의 Menu Music에 연결돼 있습니다.
/// </summary>
public class MenuMusic : MonoBehaviour
{
    private const float RepeatGapSeconds = 3f;

    private static MenuMusic instance;

    private AudioSource source;
    private float volume;
    private bool stopping;

    public static void Begin()
    {
        if (instance != null)
        {
            return;
        }

        AudioClip clip = GameSfx.MenuMusicClip;
        if (clip == null)
        {
            return;
        }

        GameObject go = new GameObject("MenuMusic");
        instance = go.AddComponent<MenuMusic>();
        instance.Run(clip, GameSfx.MenuMusicVolume);
    }

    /// <summary>게임이 시작되어 메뉴 BGM을 끌 때. fadeSeconds 동안 서서히 줄어듦.</summary>
    public static void Stop(float fadeSeconds = 1f)
    {
        if (instance != null && !instance.stopping)
        {
            instance.stopping = true;
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.FadeOutRoutine(fadeSeconds));
        }
    }

    private void Run(AudioClip clip, float targetVolume)
    {
        volume = targetVolume;
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        StartCoroutine(PlayLoopRoutine());
    }

    private IEnumerator PlayLoopRoutine()
    {
        while (true)
        {
            source.Play();
            while (source.isPlaying)
            {
                yield return null;
            }

            // 곡이 끝나면 3초 쉬었다가 다시
            yield return new WaitForSecondsRealtime(RepeatGapSeconds);
        }
    }

    private IEnumerator FadeOutRoutine(float seconds)
    {
        float start = source.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }

        source.Stop();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
