using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 팁·단계용 월드 오브젝트(문·잠긴 문·전등 스위치·캐비닛 문) 판별.
/// 대상 스크립트의 공개 상태값을 읽기만 합니다.
/// </summary>
public static class TutorialWorldObjects
{
    private const float AimDistance = 3f;

    public enum AimTarget
    {
        None,
        Door,
        LockedDoorKey,
        LockedDoorLever,
        LampSwitch
    }

    private static int aimMask;

    /// <summary>카메라 정면에서 가장 가까운 상호작용 대상 종류.</summary>
    public static AimTarget GetAimTarget(Camera camera)
    {
        if (camera == null)
        {
            return AimTarget.None;
        }

        if (aimMask == 0)
        {
            aimMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI", "PickupItem", "RaderIcon", "NightVision");
        }

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, AimDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            return AimTarget.None;
        }

        Collider col = hit.collider;

        LampSwitchInteract lampSwitch = col.GetComponentInParent<LampSwitchInteract>();
        if (lampSwitch != null)
        {
            // 켠 뒤에는 안내 문구가 비워지므로 아직 안 켠 스위치만
            return string.IsNullOrEmpty(lampSwitch.GetInteractPrompt()) ? AimTarget.None : AimTarget.LampSwitch;
        }

        // DoorClick도 문 자신·자식·부모 collider를 모두 자기 문으로 취급함
        DoorClick door = col.GetComponentInParent<DoorClick>();
        if (door == null)
        {
            door = col.GetComponentInChildren<DoorClick>();
        }

        if (door == null || IsCabinetDoor(door) || door.IsBroken())
        {
            return AimTarget.None;
        }

        LockedDoor lockedDoor = door.GetComponent<LockedDoor>();
        if (lockedDoor != null && lockedDoor.IsLocked)
        {
            return lockedDoor.UnlockMethod == LockedDoor.UnlockType.Key
                ? AimTarget.LockedDoorKey
                : AimTarget.LockedDoorLever;
        }

        return door.enabled && !door.IsOpen() ? AimTarget.Door : AimTarget.None;
    }

    /// <summary>씬의 캐비닛(가구) 문 중 현재 닫혀 있는 것.</summary>
    public static void CollectClosedCabinetDoors(List<DoorClick> result)
    {
        result.Clear();
        foreach (DoorClick door in Object.FindObjectsByType<DoorClick>(FindObjectsSortMode.None))
        {
            if (door.enabled && !door.IsOpen() && IsCabinetDoor(door))
            {
                result.Add(door);
            }
        }
    }

    /// <summary>
    /// 상위에 Cabinet 태그가 있거나, 상위 오브젝트의 직계 자식에 RandomItemSpawner가 있으면 캐비닛 문.
    /// (Cabinet (ItemCreate) 프리팹은 루트 바로 아래에 ItemSpawner가 있음.
    ///  하위 전체를 뒤지면 타일 루트에서 같은 타일의 캐비닛까지 잡혀 일반 문도 캐비닛으로 오판함)
    /// </summary>
    public static bool IsCabinetDoor(DoorClick door)
    {
        return GetCabinetRoot(door) != null;
    }

    /// <summary>캐비닛 문이면 캐비닛(가구) 루트, 아니면 null.</summary>
    public static Transform GetCabinetRoot(DoorClick door)
    {
        for (Transform current = door.transform.parent; current != null; current = current.parent)
        {
            if (current.CompareTag("Cabinet") || HasDirectChildSpawner(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool HasDirectChildSpawner(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.GetComponent<RandomItemSpawner>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
