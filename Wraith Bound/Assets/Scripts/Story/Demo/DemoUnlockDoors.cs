using UnityEngine;

/// <summary>
/// 시연 씬 시작할 때 지정한 잠긴 문을 풀고(필요하면 열어 둠).
/// 맵엔 레버가 없어 레버 문이 영영 안 열렸음 — 시작 복도 한가운데 레버 문 2개·③ 침대 방 레버 문을 시연 동선용으로 풂.
/// 팀원 LockedDoor·DoorClick은 수정하지 않고 공개 함수만 부름.
/// </summary>
public class DemoUnlockDoors : MonoBehaviour
{
    [SerializeField] private LockedDoor[] unlock = { };
    [Tooltip("풀고 나서 열어 둘 문")]
    [SerializeField] private DoorClick[] open = { };

    private void Start()
    {
        foreach (LockedDoor door in unlock)
        {
            if (door != null && door.IsLocked)
            {
                door.UnlockDoor();
            }
        }

        foreach (DoorClick door in open)
        {
            if (door != null)
            {
                DemoDoorOpener.Open(door, false);
            }
        }
    }
}
