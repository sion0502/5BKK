/// <summary>
/// 허브(귀문의 방)에 어떻게 들어왔는지 — 씬을 넘어가며 남기는 표시.
/// 프롤로그에서 죽고 넘어오면 FromPrologue=true → 허브의 HubAwakening이 '바닥에서 깨어나는' 연출을 재생하고 지움.
/// (챕터에서 돌아올 때의 페이드 인은 팀원 HubSceneWakeUp/ScreenFader.ShouldWakeUpInHub가 따로 맡음)
/// </summary>
public static class HubArrival
{
    public static bool FromPrologue { get; set; }

    /// <summary>
    /// 메인 메뉴가 허브를 배경으로 Additive 로드한 상태 — 깨어남 연출은 건너뛰고,
    /// 허브 독백·재촉(HubStoryDirector)은 '이어서 하기'로 플레이가 시작될 때까지 대기.
    /// </summary>
    public static bool MenuBackdrop { get; set; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        FromPrologue = false;
        MenuBackdrop = false;
    }
}
