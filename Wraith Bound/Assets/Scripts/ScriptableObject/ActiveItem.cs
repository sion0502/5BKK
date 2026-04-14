using UnityEngine;

[CreateAssetMenu(fileName = "NewActiveItem", menuName = "Custom/Items/ActiveItem")]
public class ActiveItem : Items
{
    [Header("소모/설치 데이터")]
    public float value; // 회복량, 충전량 등
    public float duration; // 설치 아이템 지속시간
    public bool isInstantUse; // 랜덤박스 맵에서 즉시실행
    public GameObject deployPrefab; // 가시덤불, 라디오 등 설치 아이템 사용 시 생성될 모델

    public override void Use(PlayerController player)
    {
        // 1. 설치형 아이템 로직
        if (deployPrefab != null)
        {
            Instantiate(deployPrefab, player.transform.position + player.transform.forward, Quaternion.identity);
        }

        // 2. 효과 적용 (여기서 value 등을 활용한 로직 수행)
        Debug.Log($"{itemName} 사용함. 수치: {value}");

        // 3. 부모의 파괴/소모 로직 실행
        base.Use(player);
    }
}