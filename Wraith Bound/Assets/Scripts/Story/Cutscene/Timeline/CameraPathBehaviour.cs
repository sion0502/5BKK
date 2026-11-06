using UnityEngine;
using UnityEngine.Playables;

/// <summary>클립 시간 → 경로 진행도(사다리꼴 속도: 가속 → 등속 → 감속)를 계산해 CutsceneCameraPath에 넣습니다.</summary>
public class CameraPathBehaviour : PlayableBehaviour
{
    public float rampSeconds = 1.2f;
    public float startProgress;
    public float endProgress = 1f;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneCameraPath path = playerData as CutsceneCameraPath;
        if (path == null)
        {
            return;
        }

        float total = (float)playable.GetDuration();
        float time = Mathf.Clamp((float)playable.GetTime(), 0f, total);
        path.SetProgress(Mathf.Lerp(startProgress, endProgress, Progress(time, total, rampSeconds)));
    }

    private static float Progress(float t, float total, float ramp)
    {
        if (total <= 0f)
        {
            return 1f;
        }

        ramp = Mathf.Clamp(ramp, 0f, total * 0.5f);
        if (ramp <= 0f)
        {
            return t / total;
        }

        float speed = 1f / (total - ramp);
        if (t < ramp)
        {
            return speed * t * t / (2f * ramp);
        }

        if (t > total - ramp)
        {
            float r = total - t;
            return 1f - speed * r * r / (2f * ramp);
        }

        return speed * (t - ramp * 0.5f);
    }
}
