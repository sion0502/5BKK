using System.Collections.Generic;

/// <summary>
/// 튜토리얼 기본 문구. 프리팹 빌더와 TutorialManager(목록이 비어 있을 때)가 사용합니다.
/// 실제 문구 수정은 프리팹 인스펙터에서 하면 됩니다.
/// </summary>
public static class TutorialDefaults
{
    public static List<TutorialStep> CreateSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep(TutorialCondition.Move, "WASD", "주변을 걸어서 이동하세요", 3f),
            new TutorialStep(TutorialCondition.Sprint, "Shift", "Shift를 누른 채 앞으로 달리세요", 1f),
            new TutorialStep(TutorialCondition.Crouch, "Ctrl", "Ctrl을 누른 채 웅크리세요", 0.75f),
            new TutorialStep(TutorialCondition.CamcorderHold, "F", "F를 눌러 캠코더를 꺼내세요"),
            new TutorialStep(TutorialCondition.CamcorderViewfinder, "좌클릭", "좌클릭으로 뷰파인더를 켜세요\n어둠 속을 볼 수 있습니다"),
            new TutorialStep(TutorialCondition.CamcorderZoom, "휠", "마우스 휠로 줌을 조절하세요", 2f),
            new TutorialStep(TutorialCondition.CamcorderStow, "F", "F를 눌러 캠코더를 집어넣으세요"),
            new TutorialStep(TutorialCondition.PickUpFlashlight, "E", "앞의 데스크에 놓인 손전등을 주우세요", 0f, true),
            new TutorialStep(TutorialCondition.SelectFlashlight, "1~3 / 휠", "숫자키나 마우스 휠로 손전등을 선택하세요"),
            new TutorialStep(TutorialCondition.FlashlightOn, "좌클릭", "좌클릭으로 손전등을 켜세요"),
            new TutorialStep(TutorialCondition.ExpandInventory, "Tab", "Tab으로 인벤토리를 펼쳐 보세요"),
        };
    }

    public static List<TutorialTip> CreateTips()
    {
        return new List<TutorialTip>
        {
            new TutorialTip(TutorialTipTrigger.AcquireUsableItem, "아이템 사용",
                "숫자키나 마우스 휠로 아이템을 선택한 뒤\n<b>우클릭을 길게</b> 누르면 사용합니다."),
            new TutorialTip(TutorialTipTrigger.AcquireBattery, "건전지",
                "손전등을 선택한 상태에서 <b>R</b>을 누르면\n건전지로 손전등을 충전합니다."),
            new TutorialTip(TutorialTipTrigger.AimHidingSpot, "숨기",
                "숨을 수 있는 곳입니다. <b>E</b>로 들어가 숨고\n다시 <b>E</b>를 누르면 나옵니다.\n숨어 있으면 적에게 들키지 않습니다."),
        };
    }
}
