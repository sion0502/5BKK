using UnityEngine;

[CreateAssetMenu(fileName = "NewActiveItem", menuName = "Custom/Items/ActiveItem")]
public class ActiveItem : Items
{
    [Header("소모/설치 데이터")]
    public float value; // 회복량, 충전량 등
    public float duration; // 설치 아이템 지속시간
    public bool isInstantUse; // 랜덤박스 맵에서 즉시실행
    public GameObject prefab; // 가시덤불, 라디오 등 설치 아이템 사용 시 생성될 모델
}
