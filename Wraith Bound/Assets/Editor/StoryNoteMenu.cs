using UnityEditor;

/// <summary>스토리 쪽지 수집 기록 초기화 (에디터) — 부스에선 관람객이 바뀌어도 일부러 안 지움, 운영자가 필요할 때만.</summary>
public static class StoryNoteMenu
{
    [MenuItem("Tools/Story/Reset Story Notes")]
    public static void Reset()
    {
        StoryNoteProgress.ResetAll();
    }
}
