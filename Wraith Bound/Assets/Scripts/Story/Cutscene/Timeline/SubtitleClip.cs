using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 자막 클립.
/// - waitForClick(기본): 클릭할 때까지 표시. 클립 끝까지 클릭이 없으면 Timeline을 그 자리에 멈춰 기다림.
///   skipToClipEndOnClick이면 클립 도중 클릭 시 Timeline을 클립 끝으로 건너뜀(검은 화면 대사용).
///   카메라가 움직이는 구간에선 끄세요 — 건너뛰면 카메라가 순간이동합니다.
/// - waitForClick 끔: 클립 길이 동안 자동 표시.
/// </summary>
[System.Serializable]
public class SubtitleClip : PlayableAsset, ITimelineClipAsset
{
    public string speaker;

    [TextArea(2, 5)]
    public string text;

    public bool waitForClick = true;
    public bool skipToClipEndOnClick = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<SubtitleClipBehaviour> playable = ScriptPlayable<SubtitleClipBehaviour>.Create(graph);
        SubtitleClipBehaviour behaviour = playable.GetBehaviour();
        behaviour.speaker = speaker;
        behaviour.text = text;
        behaviour.waitForClick = waitForClick;
        behaviour.skipToClipEndOnClick = skipToClipEndOnClick;
        return playable;
    }
}
