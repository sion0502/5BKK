using System.Reflection;
using UnityEngine;

/// <summary>
/// 시연 맵: 시작할 때 지정한 타일 안의 문(DoorClick)을 열어 둠.
/// 팀원 순찰 AI(PatrolPointSelector)는 '닫힌 문이 하나'일 때만 지나갈 수 있다고 판단해서, 문이 많은 북쪽 복도·서쪽 수술실에선
/// Xenosaurid·탈춤귀신이 갈 곳을 못 골라 멈춰 있었음. AI 코드는 건드리지 않고 문을 열어 둠(플레이어가 닫으면 원래 규칙대로 열고 지나감).
/// 잠긴 문(LockedDoor)은 그대로.
/// </summary>
public class DemoDoorOpener : MonoBehaviour
{
    [SerializeField] private Transform[] tiles = { };

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private void Start()
    {
        int opened = 0;
        foreach (Transform tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            foreach (DoorClick door in tile.GetComponentsInChildren<DoorClick>(true))
            {
                if (door.GetComponentInParent<LockedDoor>() != null || door.IsOpen())
                {
                    continue;
                }

                Open(door);
                opened++;
            }
        }

        Debug.Log("[Demo] 문 " + opened + "개 열어 둠");
    }

    // E로 연 것과 같은 상태로 (프롤로그 PrologueDirector.ForceOpen과 같은 방식)
    public static void Open(DoorClick door, bool withSound = false)
    {
        System.Type type = typeof(DoorClick);
        type.GetMethod("SetDoorDirection", Flags)?.Invoke(door, null);
        type.GetField("open", Flags)?.SetValue(door, true);
        type.GetMethod("SyncNavMeshObstacle", Flags)?.Invoke(door, new object[] { true });
        if (withSound)
        {
            type.GetMethod("PlayDoorSound", Flags)?.Invoke(door, null); // 문 자체 열림 소리 (저절로 열리는 연출용)
        }
    }
}
