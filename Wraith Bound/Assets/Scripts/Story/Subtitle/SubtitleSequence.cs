using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순서대로 재생할 자막 묶음. 컷씬·연출마다 에셋 하나씩 만들어 인스펙터에서 문구를 고칩니다.
/// </summary>
[CreateAssetMenu(fileName = "SubtitleSequence", menuName = "Story/Subtitle Sequence")]
public class SubtitleSequence : ScriptableObject
{
    public List<SubtitleLine> lines = new List<SubtitleLine>();
}
