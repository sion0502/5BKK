using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>카메라 경로 클립 — 클립 길이 동안 CutsceneCameraPath의 startProgress→endProgress 구간을 걷게 합니다.</summary>
[System.Serializable]
public class CameraPathClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("출발·도착 때 가속/감속하는 시간(초)")]
    public float rampSeconds = 1.2f;

    [Tooltip("이 클립이 걷는 구간 (경로 전체 0~1 중). 클립을 나눠 중간에 멈춰 설 수 있음")]
    [Range(0f, 1f)] public float startProgress;
    [Range(0f, 1f)] public float endProgress = 1f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CameraPathBehaviour> playable = ScriptPlayable<CameraPathBehaviour>.Create(graph);
        CameraPathBehaviour behaviour = playable.GetBehaviour();
        behaviour.rampSeconds = rampSeconds;
        behaviour.startProgress = startProgress;
        behaviour.endProgress = endProgress;
        return playable;
    }
}
