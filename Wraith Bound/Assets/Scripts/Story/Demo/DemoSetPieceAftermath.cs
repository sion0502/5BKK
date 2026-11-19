using UnityEngine;

/// <summary>
/// 연출 괴물(③④⑤)의 뒷정리 (2026-11-19 사용자 요청 — 연출 뒤 괴물이 플레이어가 가야 할 길로 가서 진행을 막음).
/// 연출 괴물이 따돌림을 당해 사라지면(비활성), 같은 종류 배회 괴물을 플레이어에게서 멀고·안 보이고·남은 길잡이 루트에서 먼 곳에 다시 내보냄.
/// DemoRoamingSpawner.RetireSameKind가 연출 괴물에 붙임. 사라지는 것 자체는 각 연출(DemoAmbushSequence·DemoOperatingRoomSequence)이 함.
/// </summary>
public class DemoSetPieceAftermath : MonoBehaviour
{
    private EnemyBase creature;
    private bool armed;

    public static void Track(EnemyBase setPieceCreature)
    {
        if (setPieceCreature == null)
        {
            return;
        }

        DemoSetPieceAftermath a = setPieceCreature.GetComponent<DemoSetPieceAftermath>();
        if (a == null)
        {
            a = setPieceCreature.gameObject.AddComponent<DemoSetPieceAftermath>();
        }

        a.creature = setPieceCreature;
        a.armed = true;
    }

    /// <summary>이번에 사라질 때 배회 괴물을 다시 내보내지 않게 함 (연출이 '그냥 사라지고 끝'을 원할 때).</summary>
    public static void Untrack(EnemyBase setPieceCreature)
    {
        DemoSetPieceAftermath a = setPieceCreature != null ? setPieceCreature.GetComponent<DemoSetPieceAftermath>() : null;
        if (a != null)
        {
            a.armed = false;
        }
    }

    private void OnDisable()
    {
        if (!armed || creature == null)
        {
            return;
        }

        armed = false;
        // 씬을 내릴 때도 OnDisable이 오므로, 실제 처리는 스포너가 다음 프레임에 (스포너가 살아 있을 때만) 함
        DemoRoamingSpawner.RespawnSameKindLater(creature);
    }
}
