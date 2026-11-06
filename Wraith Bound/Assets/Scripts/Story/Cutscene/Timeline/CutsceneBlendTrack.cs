using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>컷씬 Blend 트랙 — 오버레이(검은 화면·캠코더 화면)·손에 든 문서 등 CutsceneBlendTarget을 0~1로 움직임.</summary>
[TrackColor(0.6f, 0.6f, 0.65f)]
[TrackClipType(typeof(CutsceneBlendClip))]
[TrackBindingType(typeof(CutsceneBlendTarget))]
public class CutsceneBlendTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutsceneBlendMixer>.Create(graph, inputCount);
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        CutsceneBlendTarget target = director.GetGenericBinding(this) as CutsceneBlendTarget;
        if (target != null)
        {
            target.GatherPreviewProperties(driver);
        }

        base.GatherProperties(director, driver);
    }
}
