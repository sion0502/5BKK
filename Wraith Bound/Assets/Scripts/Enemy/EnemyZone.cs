using UnityEngine;

public class EnemyZone : MonoBehaviour
{
    public enum ZoneType
    {
        Lobby,
        Floor1,
        Floor2
    }

    [SerializeField] private ZoneType zoneType;

    public ZoneType GetZoneType()
    {
        return zoneType;
    }
}
