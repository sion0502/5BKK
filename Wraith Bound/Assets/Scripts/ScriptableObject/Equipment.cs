using UnityEngine;

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Custom/Items/Equip")]
public class Equipment : Items
{
    [Header("장비/내구도 데이터")]
    public float maxEnergy;    // 최대 배터리/필름 수
    public float consumeRate;  // 사용 시 소모되는 양
    public float range;        // 카메라 스턴 범위, 나침반 탐색 범위 등
}
