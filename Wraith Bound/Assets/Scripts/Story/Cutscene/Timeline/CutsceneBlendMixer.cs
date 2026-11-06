using UnityEngine.Playables;

/// <summary>활성 클립들의 (가중치 × value) 합을 대상의 Blend로 적용합니다. 클립이 없는 구간은 0.</summary>
public class CutsceneBlendMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneBlendTarget target = playerData as CutsceneBlendTarget;
        if (target == null)
        {
            return;
        }

        float blend = 0f;
        int count = playable.GetInputCount();
        for (int i = 0; i < count; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f)
            {
                continue;
            }

            ScriptPlayable<CutsceneBlendBehaviour> input = (ScriptPlayable<CutsceneBlendBehaviour>)playable.GetInput(i);
            blend += weight * input.GetBehaviour().value;
        }

        target.Blend = blend;
    }
}
