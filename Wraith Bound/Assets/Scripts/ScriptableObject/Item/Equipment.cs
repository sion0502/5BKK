using UnityEngine;

public enum EquipmentUseMode
{
    PassiveOnSelect, // 슬롯을 선택하면 별도 클릭 없이 자동으로 작동하는 장비입니다. 예: 나침반
    ToggleOnClick,   // 슬롯을 선택하면 손에 들고, 마우스 클릭으로 기능을 켜고 끄는 장비입니다. 예: 손전등
    ShowOnClick      // 슬롯 선택만으로는 보이지 않고, 마우스 클릭으로 꺼내거나 활성화하는 장비입니다. 예: 스마트폰
}

[CreateAssetMenu(fileName = "NewEquipment", menuName = "Custom/Items/Equip")]
public class Equipment : Items
{
    [Header("장비 사용 방식")]
    public EquipmentUseMode useMode = EquipmentUseMode.ToggleOnClick;

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