using UnityEngine;

/// <summary>
/// 플레이어 설정 (PlayerPrefs에 저장, 게임 시작 시 자동 적용).
/// - EnvVolume: 환경음 (0~1) — AudioListener.volume. 배경음(BGM)을 뺀 모든 소리(발소리·괴물·문·효과음 등)
/// - BgmVolume: 배경음 (0~1) — BGM 스크립트가 직접 곱함. BGM AudioSource는 ignoreListenerVolume으로 환경음 영향을 받지 않음
/// - MouseSensitivity: 시점 감도 배율 (MouseLook·숨기 시점에 곱함, 1 = 기본)
/// - Fullscreen: 전체 화면 여부
/// </summary>
public static class GameSettings
{
    // 예전 '전체 음량' 키를 그대로 환경음으로 이어 씀 (저장된 값 유지)
    private const string VolumeKey = "WB_Settings_MasterVolume";
    private const string BgmVolumeKey = "WB_Settings_BgmVolume";
    private const string SensitivityKey = "WB_Settings_MouseSensitivity";
    private const string FullscreenKey = "WB_Settings_Fullscreen";

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    private static float envVolume = 1f;
    private static float bgmVolume = 1f;
    private static float mouseSensitivity = 1f;

    public static float EnvVolume
    {
        get => envVolume;
        set
        {
            envVolume = Mathf.Clamp01(value);
            AudioListener.volume = envVolume;
            PlayerPrefs.SetFloat(VolumeKey, envVolume);
        }
    }

    public static float BgmVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        }
    }

    public static float MouseSensitivity
    {
        get => mouseSensitivity;
        set
        {
            mouseSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, mouseSensitivity);
        }
    }

    public static bool Fullscreen
    {
        get => Screen.fullScreen;
        set
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        envVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
        AudioListener.volume = envVolume;

#if !UNITY_EDITOR
        if (PlayerPrefs.HasKey(FullscreenKey))
        {
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        }
#endif
    }
}
