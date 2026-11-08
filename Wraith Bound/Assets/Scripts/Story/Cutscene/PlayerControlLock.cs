using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연출 중 플레이어 조작을 잠깐 막고 되돌림. 플레이어 스크립트는 수정하지 않고 enabled만 끄고 켬(자기가 끈 것만 복구).
/// 사용: var lockHandle = PlayerControlLock.Lock(player); … lockHandle.Release();
/// </summary>
public sealed class PlayerControlLock
{
    // 타입 이름으로 찾아 의존성 최소화
    private static readonly string[] InputComponentNames =
    {
        "MouseLook", "PlayerController", "PlayerInteractor", "InventoryManager", "SelectedItemUseController",
        "PlayerHidingController", "SmartPhoneHolderToggle", RearviewSuppressor.RearviewTypeName,
    };

    private readonly List<Behaviour> disabled = new List<Behaviour>();

    public static PlayerControlLock Lock(GameObject player)
    {
        PlayerControlLock handle = new PlayerControlLock();
        if (player == null)
        {
            return handle;
        }

        foreach (Behaviour behaviour in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour.enabled && System.Array.IndexOf(InputComponentNames, behaviour.GetType().Name) >= 0)
            {
                behaviour.enabled = false;
                handle.disabled.Add(behaviour);
            }
        }

        return handle;
    }

    public void Release()
    {
        foreach (Behaviour behaviour in disabled)
        {
            if (RearviewSuppressor.IsRearview(behaviour))
            {
                RearviewSuppressor.Enable(behaviour); // 일지 등을 Space로 닫았으면 뗄 때까지 뒤돌아보기 꺼 둠
            }
            else if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabled.Clear();
    }

    /// <summary>MouseLook의 상하 각도(xRotation, private)를 맞춤 — 카메라를 코드로 돌린 뒤 조작을 돌려줄 때 튀지 않게.</summary>
    public static void SyncMouseLookPitch(Camera playerCamera, float pitch)
    {
        if (playerCamera == null)
        {
            return;
        }

        MonoBehaviour look = playerCamera.GetComponent("MouseLook") as MonoBehaviour;
        var field = look != null
            ? look.GetType().GetField("xRotation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            : null;
        field?.SetValue(look, pitch);
    }
}
