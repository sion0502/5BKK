using UnityEngine;

/// <summary>
/// 괴물·귀신에게 닿아 죽음 연출(점프스케어/데스씬)이 시작되는 순간 'Horror Impact'를 한 번 재생 (사운드 전용).
///  · PlayerDeathDebug.IsDying / IsDead 가 켜지는 순간(= 닿은 순간)을 읽기만 함 — 게임 로직은 건드리지 않음.
///  · 2D 소리(플레이어가 직접 맞는 충격음). timeScale 0 / AudioListener.pause 중에도 들리게 함.
///  · 게임오버 화면으로 넘어가면 0.3초에 걸쳐 줄이며 끝냄.
/// 프리팹·씬에 붙일 필요 없이 게임 시작 때 스스로 생성됨. 소리 파일: Assets/Resources/DeathImpact.wav
/// </summary>
[DisallowMultipleComponent]
public class DeathImpactSound : MonoBehaviour
{
    private const string ClipName = "DeathImpact";
    private const float Volume = 1f;
    private const float FadeOutSeconds = 0.3f;

    private static DeathImpactSound instance;

    private AudioClip clip;
    private AudioSource source;
    private bool wasDeathActive;
    private bool fading;
    private float fadeStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(DeathImpactSound));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DeathImpactSound>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        clip = Resources.Load<AudioClip>(ClipName);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;          // 2D — 플레이어가 직접 맞는 충격음
        source.priority = 0;
        source.ignoreListenerPause = true; // 데스씬 중 timeScale 0 / 게임오버 pause 에서도 재생
        source.volume = Volume;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (source == null)
        {
            return;
        }

        bool deathActive = PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead;

        // 닿아서 죽음 연출이 시작되는 순간 한 번
        if (deathActive && !wasDeathActive)
        {
            fading = false;
            if (clip != null)
            {
                source.volume = Volume;
                source.pitch = 1f;
                source.Stop();
                source.PlayOneShot(clip, 1f);
            }
        }

        wasDeathActive = deathActive;

        // 게임오버 화면으로 넘어가면 부드럽게 끝냄
        if (PlayerDeathDebug.IsDead && source.isPlaying && !fading)
        {
            fading = true;
            fadeStart = Time.unscaledTime;
        }

        if (fading)
        {
            float k = 1f - Mathf.Clamp01((Time.unscaledTime - fadeStart) / FadeOutSeconds);
            source.volume = Volume * k;
            if (k <= 0f)
            {
                source.Stop();
                fading = false;
            }
        }
    }
}
