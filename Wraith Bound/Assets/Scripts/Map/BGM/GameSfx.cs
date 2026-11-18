using System.Collections;
using UnityEngine;

/// <summary>
/// 여러 씬에서 공통으로 쓰는 효과음 모음 (Resources/GameSfx.asset).
/// 소리 파일은 에셋에서 클립만 연결해 두고, 코드에서는 GameSfx.PlayXxx()로 부릅니다.
/// </summary>
public class GameSfx : ScriptableObject
{
    private static GameSfx instance;
    private static float lastKeyDropTime = -10f;

    private static GameSfx Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameSfx>("GameSfx");
            }

            return instance;
        }
    }

    [Header("Paper flutter — 일지·쪽지 꺼낼 때 / 집어넣을 때 (원본이 길어 또렷한 구간만 잘라 씀)")]
    [SerializeField] private AudioClip paperFlutter;
    [SerializeField, Range(0f, 1f)] private float paperVolume = 0.8f;
    [SerializeField] private float paperStartOffset = 8.4f;
    [SerializeField] private float paperPlaySeconds = 1.8f;
    [SerializeField] private float paperFadeOutSeconds = 0.5f;

    [Header("Key — 열쇠로 잠긴 문을 열 때 / 자물쇠가 바닥에 떨어질 때")]
    [SerializeField] private AudioClip keyUnlock;
    [SerializeField, Range(0f, 1f)] private float keyUnlockVolume = 1f;
    [SerializeField] private AudioClip keyDrop;
    [SerializeField, Range(0f, 1f)] private float keyDropVolume = 1f;

    [Header("Door Rattle — 프롤로그 마지막 문 앞에서 '안 열려' 할 때")]
    [SerializeField] private AudioClip doorRattle;
    [SerializeField, Range(0f, 1f)] private float doorRattleVolume = 1f;
    [SerializeField] private float doorRattleStartOffset = 0.2f; // 클립 앞부분 무음/약한 부분 건너뜀

    [Header("Menu Music — 메인 메뉴 BGM (Detuned Pulse)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.5f;

    public static AudioClip MenuMusicClip => Instance != null ? Instance.menuMusic : null;
    public static float MenuMusicVolume => Instance != null ? Instance.menuMusicVolume : 0.5f;

    [Header("EscHover — ESC(일시정지) 메뉴 버튼에 마우스를 올렸을 때")]
    [SerializeField] private AudioClip escHover;
    [SerializeField, Range(0f, 1f)] private float escHoverVolume = 1f;

    [Header("Keypad — 번호 자물쇠 잠금 해제")]
    [SerializeField] private AudioClip keypadBeep;
    [SerializeField, Range(0f, 1f)] private float keypadBeepVolume = 1f;

    public static void PlayEscHover()
    {
        GameSfx sfx = Instance;
        if (sfx != null && sfx.escHover != null)
        {
            Play(sfx.escHover, sfx.escHoverVolume, 0f, 0f, 0f);
        }
    }

    /// <summary>키패드 잠금 해제 소리. 소리가 연결돼 있으면 재생하고 true.</summary>
    public static bool PlayKeypadBeep()
    {
        GameSfx sfx = Instance;
        if (sfx == null || sfx.keypadBeep == null)
        {
            return false;
        }

        Play(sfx.keypadBeep, sfx.keypadBeepVolume, 0f, 0f, 0f);
        return true;
    }

    /// <summary>아이템 사용음 등 클립을 그대로 한 번 재생 (2D).</summary>
    public static void PlayClip(AudioClip clip, float volume = 1f)
    {
        Play(clip, volume, 0f, 0f, 0f);
    }

    public static void PlayPaperFlutter()
    {
        GameSfx sfx = Instance;
        if (sfx != null)
        {
            Play(sfx.paperFlutter, sfx.paperVolume, sfx.paperStartOffset, sfx.paperPlaySeconds, sfx.paperFadeOutSeconds);
        }
    }

    public static void PlayKeyUnlock()
    {
        GameSfx sfx = Instance;
        if (sfx != null)
        {
            Play(sfx.keyUnlock, sfx.keyUnlockVolume, 0f, 0f, 0f);
        }
    }

    public static void PlayDoorRattle()
    {
        GameSfx sfx = Instance;
        if (sfx != null && sfx.doorRattle != null)
        {
            Play(sfx.doorRattle, sfx.doorRattleVolume, sfx.doorRattleStartOffset, 0f, 0f);
        }
    }

    public static void PlayKeyDrop()
    {
        // 자물쇠가 여러 개여도 소리는 한 번만
        if (Time.unscaledTime - lastKeyDropTime < 0.4f)
        {
            return;
        }

        GameSfx sfx = Instance;
        if (sfx != null && sfx.keyDrop != null)
        {
            lastKeyDropTime = Time.unscaledTime;
            Play(sfx.keyDrop, sfx.keyDropVolume, 0.07f, 0f, 0f); // 클립 앞 무음 약 0.09초 건너뜀
        }
    }

    /// <summary>seconds가 0이면 클립 끝까지. offset·seconds·fade는 초 단위.</summary>
    private static void Play(AudioClip clip, float volume, float offset, float seconds, float fade)
    {
        if (clip == null)
        {
            return;
        }

        GameObject go = new GameObject("SFX_" + clip.name);
        SfxVoice voice = go.AddComponent<SfxVoice>();
        voice.Run(clip, volume, offset, seconds, fade);
    }

    private class SfxVoice : MonoBehaviour
    {
        public void Run(AudioClip clip, float volume, float offset, float seconds, float fade)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = volume;
            source.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clip.length - 0.05f));
            source.Play();
            StartCoroutine(Routine(source, clip, volume, offset, seconds, fade));
        }

        private IEnumerator Routine(AudioSource source, AudioClip clip, float volume, float offset, float seconds, float fade)
        {
            float total = seconds > 0f ? seconds : Mathf.Max(0.05f, clip.length - offset);
            float solid = Mathf.Max(0f, total - fade);
            yield return new WaitForSecondsRealtime(solid);

            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                source.volume = volume * (1f - t / fade);
                yield return null;
            }

            if (seconds <= 0f)
            {
                // 전체 재생이면 남은 꼬리까지 기다림
                while (source != null && source.isPlaying)
                {
                    yield return null;
                }
            }

            Destroy(gameObject);
        }
    }
}
