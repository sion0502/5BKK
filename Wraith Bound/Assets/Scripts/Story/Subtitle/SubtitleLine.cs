using System;
using UnityEngine;

/// <summary>
/// 자막 한 줄. speaker가 비어 있으면 화자 이름 없이 문장만 표시합니다(내레이션·글씨 등).
/// holdSeconds가 0 이하이면 글자 수로 표시 시간을 자동 계산합니다.
/// </summary>
[Serializable]
public class SubtitleLine
{
    public string speaker;

    [TextArea(2, 5)]
    public string text;

    [Tooltip("타자 효과가 끝난 뒤 이 줄을 유지할 시간(초). 0 이하 = 글자 수로 자동 계산")]
    public float holdSeconds;

    public SubtitleLine()
    {
    }

    public SubtitleLine(string speaker, string text, float holdSeconds = 0f)
    {
        this.speaker = speaker;
        this.text = text;
        this.holdSeconds = holdSeconds;
    }
}
