using UnityEngine.Timeline;

/// <summary>자막 트랙. 바인딩 없음 — 클립을 끌어서 대사 타이밍을 맞춥니다.</summary>
[TrackColor(0.95f, 0.55f, 0.5f)]
[TrackClipType(typeof(SubtitleClip))]
public class SubtitleTrack : TrackAsset
{
}
