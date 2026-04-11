using UnityEngine;

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Custom/Items/Equip")]
public class Equipment : Items
{
    [Header("장비/내구도 데이터")]
    public float maxEnergy;    // 최대 배터리/필름 수
    public float consumeRate;  // 사용 시 소모되는 양
    public float range;        // 카메라 스턴 범위, 나침반 탐색 범위 등

    public override void Use(PlayerController player)
    {
        // 장비 고유의 에너지 체크 로직 등을 여기에 추가할 수 있습니다.
        base.Use(player); // 껐다 켜기 및 확률 파괴 로직 실행
    }
}