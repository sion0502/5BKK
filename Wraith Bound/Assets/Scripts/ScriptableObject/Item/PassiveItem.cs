using UnityEngine;

[CreateAssetMenu(fileName = "NewPassiveItem", menuName = "Custom/Items/PassiveItem")]
public class PassiveItem : Items
{
    [Header("패시브 효과 데이터")]
    public float statModifier; // 이동속도 증가량, 회복 속도 등
    public int extraSlots;     // 가방 전용 (인벤토리 확장)

    [Header("주변 조명 (패시브)")]
    public bool providesAmbientLight;
    [Min(0.1f)] public float ambientLightRange = 2f;
    [Min(0f)] public float ambientLightIntensity = 2.5f;
    public Color ambientLightColor = new Color(1f, 0.72f, 0.38f, 1f);
    public bool flickerLight = true;

    public override void Use(PlayerController player)
    {
        // 패시브 아이템은 클릭해서 사용하는 것이 아니므로 기본 동작을 막습니다.
        Debug.Log($"{itemName}은(는) 패시브 아이템이라 직접 사용할 수 없습니다.");
    }
}