using UnityEngine;

/// <summary>
/// 플레이어 설정 (PlayerPrefs에 저장, 게임 시작 시 자동 적용).
/// - MasterVolume: AudioListener.volume (0~1)
/// - MouseSensitivity: 시점 감도 배율 (MouseLook·숨기 시점에 곱함, 1 = 기본)
/// - Fullscreen: 전체 화면 여부
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "WB_Settings_MasterVolume";
    private const string SensitivityKey = "WB_Settings_MouseSensitivity";
    private const string FullscreenKey = "WB_Settings_Fullscreen";

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    private static float masterVolume = 1f;
    private static float mouseSensitivity = 1f;

    public static float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(VolumeKey, masterVolume);
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
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
        AudioListener.volume = masterVolume;

#if !UNITY_EDITOR
        if (PlayerPrefs.HasKey(FullscreenKey))
        {
            Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        }
#endif
    }
}
