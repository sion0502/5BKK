using UnityEngine;

public class GhostEnemy : EnemyBase
{
    DoorController currentDoor;

    protected override void HandleDoor(
        DoorController door)
    {
        if (currentState != State.Chase)
            return;

        if (door == null)
            return;

        // 같은 문 중복 처리 방지
        if (currentDoor == door)
            return;

        currentDoor = door;

        // 문을 길로 변경
        currentDoor.OpenPath();

        // 기존 경로 초기화 후 다시 추격
        Agent.ResetPath();

        Agent.SetDestination(
            Player.position);
    }

    void LateUpdate()
    {
        // 추격 끝났으면 문 다시 막기
        if (currentState == State.Chase)
            return;

        if (currentDoor == null)
            return;

        currentDoor.ClosePath();

        currentDoor = null;
    }
}