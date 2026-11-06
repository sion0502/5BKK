using UnityEngine;

/// <summary>
/// 팝업(튜토리얼 팁 등)이 ESC를 쓰는 동안 일시정지 메뉴가 ESC로 열리지 않게 막는 플래그.
/// PauseMenuController.Update가 IsSuppressed를 확인함. 여러 곳에서 동시에 막을 수 있게 횟수로 관리.
/// 마지막 Release가 일어난 프레임까지 막힌 것으로 봄 — 팝업을 닫은 ESC가 같은 프레임에 메뉴를 열지 않도록(Update 순서 무관).
/// </summary>
public static class PauseMenuSuppressor
{
    private static int count;
    private static int releasedFrame = -1;

    public static bool IsSuppressed => count > 0 || Time.frameCount == releasedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // 도메인 리로드 끈 에디터 설정 대비
    {
        count = 0;
        releasedFrame = -1;
    }

    public static void Suppress()
    {
        count++;
    }

    public static void Release()
    {
        if (count == 0)
        {
            return;
        }

        count--;
        if (count == 0)
        {
            releasedFrame = Time.frameCount;
        }
    }
}
