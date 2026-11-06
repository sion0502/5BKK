using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Blend 클립. 클립 구간 동안 대상을 value로 만듭니다(오버레이 = 알파, 손에 든 문서 = 들어 올림).
/// 클립의 Ease In/Out(또는 클립끼리 겹치기)으로 서서히 바뀌게 합니다.
/// </summary>
[System.Serializable]
public class CutsceneBlendClip : PlayableAsset, ITimelineClipAsset
{
    [Range(0f, 1f)] public float value = 1f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CutsceneBlendBehaviour> playable = ScriptPlayable<CutsceneBlendBehaviour>.Create(graph);
        playable.GetBehaviour().value = value;
        return playable;
    }
}
