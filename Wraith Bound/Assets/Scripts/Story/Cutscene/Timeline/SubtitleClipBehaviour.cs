using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 자막 클립 실행. 에디터 미리보기(Play 모드 아님)에선 아무것도 안 함.
/// 클릭 대기는 Timeline 루트 재생 속도를 0으로 만들어 "멈춤"을 구현합니다(director.Pause와 달리 상태 유지).
/// </summary>
public class SubtitleClipBehaviour : PlayableBehaviour
{
    public string speaker;
    public string text;
    public bool waitForClick = true;
    public bool skipToClipEndOnClick = true;

    private bool shown;
    private bool dismissed;
    private bool holding;
    private double clipEndOnDirector;
    private Playable root;
    private PlayableDirector director;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (!Application.isPlaying || shown || string.IsNullOrEmpty(text))
        {
            return;
        }

        shown = true;
        director = playable.GetGraph().GetResolver() as PlayableDirector;
        root = playable.GetGraph().GetRootPlayable(0);
        UpdateClipEnd(playable);

        if (waitForClick)
        {
            SubtitleUI.ShowUntilClick(speaker, text, OnDismissed);
        }
        else
        {
            SubtitleUI.ShowForDuration(speaker, text, (float)(playable.GetDuration() - playable.GetTime()));
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || !shown || !waitForClick || dismissed || holding)
        {
            return;
        }

        UpdateClipEnd(playable);

        // 다음 프레임에 클립을 벗어날 것 같으면 그 자리에서 멈추고 클릭을 기다림
        if (playable.GetTime() + info.deltaTime >= playable.GetDuration() && root.IsValid())
        {
            holding = true;
            root.SetSpeed(0d);
        }
    }

    private void UpdateClipEnd(Playable playable)
    {
        if (director != null)
        {
            clipEndOnDirector = director.time + (playable.GetDuration() - playable.GetTime());
        }
    }

    private void OnDismissed()
    {
        dismissed = true;

        if (holding)
        {
            holding = false;
            if (root.IsValid())
            {
                root.SetSpeed(1d);
            }

            return;
        }

        if (skipToClipEndOnClick && director != null && director.time < clipEndOnDirector)
        {
            director.time = clipEndOnDirector;
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        // 컷씬이 끝나거나 건너뛰어져 그래프가 사라질 때 멈춘 상태가 남지 않도록
        holding = false;
    }
}
