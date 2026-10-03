using System;
using UnityEngine;

/// <summary>기본 진행 단계의 완료 조건 종류. 판정은 TutorialConditions에서 합니다.</summary>
public enum TutorialCondition
{
    Move,
    Sprint,
    Crouch,
    CamcorderHold,
    CamcorderViewfinder,
    CamcorderZoom,
    CamcorderStow,
    PickUpFlashlight,
    SelectFlashlight,
    FlashlightOn,
    ExpandInventory
}

/// <summary>상황형 팁이 뜨는 계기. 각 팁은 한 번만 표시됩니다.</summary>
public enum TutorialTipTrigger
{
    AcquireUsableItem,
    AcquireBattery,
    AimHidingSpot
}

/// <summary>좌상단에 표시되는 기본 진행 단계. 직접 해봐야 다음 단계로 넘어갑니다.</summary>
[Serializable]
public class TutorialStep
{
    public TutorialCondition condition;

    [Tooltip("키 배지에 표시할 텍스트 (예: WASD, Shift)")]
    public string keyLabel;

    [TextArea(1, 3)]
    public string message;

    [Tooltip("Move: 이동 거리(m) / Sprint·Crouch: 유지 시간(초) / CamcorderZoom: 휠 입력 횟수. 그 외 무시.")]
    public float requiredAmount;

    [Tooltip("이 단계 동안 하이라이트 대상(손전등)에 외곽선을 상시 표시")]
    public bool highlightTarget;

    public TutorialStep() { }

    public TutorialStep(TutorialCondition condition, string keyLabel, string message,
        float requiredAmount = 0f, bool highlightTarget = false)
    {
        this.condition = condition;
        this.keyLabel = keyLabel;
        this.message = message;
        this.requiredAmount = requiredAmount;
        this.highlightTarget = highlightTarget;
    }
}

/// <summary>화면 중앙에 시간을 멈추고 표시되는 상황형 팁.</summary>
[Serializable]
public class TutorialTip
{
    public TutorialTipTrigger trigger;
    public string title;

    [TextArea(2, 5)]
    public string message;

    public TutorialTip() { }

    public TutorialTip(TutorialTipTrigger trigger, string title, string message)
    {
        this.trigger = trigger;
        this.title = title;
        this.message = message;
    }
}
