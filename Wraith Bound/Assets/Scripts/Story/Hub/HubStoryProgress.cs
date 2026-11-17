using UnityEngine;

/// <summary>
/// 허브 첫 진행(깨어난 뒤 쪽지 → 캠코더 → 열린 문) 단계 저장 (PlayerPrefs).
/// 허브에서 진행 도중 게임을 껐다가 '이어서 하기'로 돌아와도 남은 단계부터 재촉하고,
/// 이미 한 번 나온 반응 독백("문이… 세 개?" 등)은 다시 나오지 않게 합니다.
/// 새로운 게임(GameProgress.Clear)에서 같이 지워집니다.
/// </summary>
public static class HubStoryProgress
{
    private const string Prefix = "WB_HubStory_";

    public const string DoorsLine = "DoorsLine";
    public const string OpenDoorLine = "OpenDoorLine";
    public const string CamcorderLine = "CamcorderLine";

    private const string StartedKey = Prefix + "Started";
    private const string NoteReadKey = Prefix + "NoteRead";
    private const string CompleteKey = Prefix + "Complete";

    /// <summary>허브에서 처음 깨어나 진행이 시작된 적 있음.</summary>
    public static bool Started => PlayerPrefs.GetInt(StartedKey, 0) == 1;

    /// <summary>열린 문까지 가서 허브 첫 진행이 끝남 — 이후 허브 입장은 조용히.</summary>
    public static bool Complete => PlayerPrefs.GetInt(CompleteKey, 0) == 1;

    public static bool NoteRead => PlayerPrefs.GetInt(NoteReadKey, 0) == 1;

    public static void MarkStarted() => Set(StartedKey);
    public static void MarkNoteRead() => Set(NoteReadKey);
    public static void MarkComplete() => Set(CompleteKey);

    public static bool WasLineSaid(string lineId) => PlayerPrefs.GetInt(Prefix + lineId, 0) == 1;
    public static void MarkLineSaid(string lineId) => Set(Prefix + lineId);

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(StartedKey);
        PlayerPrefs.DeleteKey(NoteReadKey);
        PlayerPrefs.DeleteKey(CompleteKey);
        PlayerPrefs.DeleteKey(Prefix + DoorsLine);
        PlayerPrefs.DeleteKey(Prefix + OpenDoorLine);
        PlayerPrefs.DeleteKey(Prefix + CamcorderLine);
        PlayerPrefs.Save();
    }

    private static void Set(string key)
    {
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }
}
