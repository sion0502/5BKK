using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상황형 팁 표시 중 플레이어 입력(시점·이동·상호작용·아이템 사용·슬롯 전환·숨기)을 잠급니다.
/// timeScale=0이어도 Update의 키 입력은 들어오기 때문에 필요합니다.
/// 자신이 끈 컴포넌트만 다시 켜므로 일시정지 메뉴의 잠금과 겹쳐도 안전합니다.
/// </summary>
public class TutorialInputBlocker
{
    private readonly List<Behaviour> targets = new List<Behaviour>();
    private readonly List<Behaviour> disabledByMe = new List<Behaviour>();

    public bool IsBlocking { get; private set; }

    public TutorialInputBlocker(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        AddTarget(player.GetComponent<PlayerController>());
        AddTarget(player.GetComponent<PlayerInteractor>());
        AddTarget(player.GetComponent<SelectedItemUseController>());
        AddTarget(player.GetComponent<InventoryManager>());
        AddTarget(player.GetComponent<PlayerHidingController>());
        AddTarget(player.GetComponentInChildren<MouseLook>(true));
    }

    private void AddTarget(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            targets.Add(behaviour);
        }
    }

    public void Block()
    {
        if (IsBlocking)
        {
            return;
        }

        IsBlocking = true;
        disabledByMe.Clear();

        foreach (Behaviour behaviour in targets)
        {
            if (behaviour != null && behaviour.enabled)
            {
                behaviour.enabled = false;
                disabledByMe.Add(behaviour);
            }
        }
    }

    public void Unblock()
    {
        if (!IsBlocking)
        {
            return;
        }

        IsBlocking = false;

        foreach (Behaviour behaviour in disabledByMe)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledByMe.Clear();
    }
}
