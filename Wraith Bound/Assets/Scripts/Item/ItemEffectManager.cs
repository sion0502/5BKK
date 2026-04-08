using UnityEngine;

public class ItemEffectManager : MonoBehaviour
{
    // [아이템 담당자 영역] 플레이어의 컴포넌트들을 참조합니다.
    private PlayerController player;

    void Start()
    {
        player = GetComponent<PlayerController>();
    }

    // 아이템 종류에 따라 적절한 효과를 실행하는 핵심 함수
    public void Use(Items item)
    {
        switch (item.type)
        {
            case ItemType.Active:
                ApplyActiveEffect((ActiveItem)item);
                break;
            case ItemType.Equip:
                ApplyEquipEffect((Equipment)item);
                break;
            case ItemType.Passive:
                ApplyPassiveEffect((PassiveItem)item);
                break;
        }
    }

    // 1. 소모품 효과 (회복 등)
    void ApplyActiveEffect(ActiveItem active)
    {
        Debug.Log($"{active.itemName} 사용! {active.value}만큼 효과 발생.");
        // 예: player.Heal(active.value); 

        if (active.prefab != null)
        {
            // 설치형 아이템일 경우 플레이어 앞이나 발밑에 프리팹 생성
            Instantiate(active.prefab, transform.position + transform.forward, Quaternion.identity);
        }
    }

    // 2. 장비 장착 효과 (배터리 체크 등)
    void ApplyEquipEffect(Equipment equip)
    {
        Debug.Log($"{equip.itemName} 장착! 최대 에너지: {equip.maxEnergy}");
        // 손에 들고 있는 모델을 바꾸거나 배터리 UI를 활성화하는 로직
    }

    // 3. 패시브 효과 (능력치 영구 수정)
    void ApplyPassiveEffect(PassiveItem passive)
    {
        Debug.Log($"{passive.itemName} 획득! 이동속도 {passive.statModifier} 증가.");
        // 예: player.moveSpeed += passive.statModifier;
    }
}