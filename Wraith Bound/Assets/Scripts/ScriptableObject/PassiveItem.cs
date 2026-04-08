using UnityEngine;

[CreateAssetMenu(fileName = "NewPassiveItem", menuName = "Custom/Items/PassiveItem")]
public class PassiveItem : Items
{
    [Header("패시브 효과 데이터")]
    public float statModifier; // 이동속도 증가량, 회복 속도 등
    public int extraSlots;     // 가방 전용 (인벤토리 확장)
    // 특수 효과(발각 저하, 소리 안남 등)는 플레이어 스크립트에서 ID로 판별하거나 bool 변수 추가
}