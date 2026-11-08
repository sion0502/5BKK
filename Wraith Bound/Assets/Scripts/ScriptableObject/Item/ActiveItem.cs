using UnityEngine;

public enum ActiveEffectType
{
    None,
    Health,
    Stamina
}

[CreateAssetMenu(fileName = "NewActiveItem", menuName = "Custom/Items/ActiveItem")]
public class ActiveItem : Items
{
    [Header("소모/설치 데이터")]
    public ActiveEffectType effectType; // value가 적용될 대상(체력/스태미나)
    public float value; // 회복량, 충전량 등
    public float duration; // 설치 아이템 지속시간
    public bool isInstantUse; // 랜덤박스 맵에서 즉시실행
    public GameObject deployPrefab; // 가시덤불, 라디오 등 설치 아이템 사용 시 생성될 모델

    [Header("투척 (야광봉 등)")]
    public bool isThrowable; // 포물선으로 던지는 아이템인지
    public float throwForce = 9f; // 던지는 힘
    public float throwUpwardAngle = 25f; // 던지는 각도(도)

    /// <summary>
    /// 우클릭 홀드로 사용할 수 있는지. 회복 효과·투척·설치 중 하나라도 있어야 함.
    /// 열쇠(잠긴 문 좌클릭)·건전지(R 충전)처럼 다른 경로로만 소모되는 아이템은 false — 홀드로 쓰면 효과 없이 사라졌음.
    /// </summary>
    /// <summary>음식 — 기 '런닝맨'·해금 카운터용. 체력 회복형 중 응급약(Drug)은 음식이 아님.</summary>
    public bool IsFood => effectType == ActiveEffectType.Health && name != "Drug";

    public bool IsHoldUsable => effectType != ActiveEffectType.None || isThrowable || deployPrefab != null;

    public override void Use(PlayerController player)
    {
        // 1. 설치형 아이템 로직
        if (deployPrefab != null)
        {
            Instantiate(deployPrefab, player.transform.position + player.transform.forward, Quaternion.identity);
        }

        // 2. 투척형 아이템 로직
        if (isThrowable)
        {
            GlowStickController.Throw(player, throwForce, throwUpwardAngle);
        }

        // 3. 효과 적용 (체력/스태미나 회복)
        ApplyRecoveryEffect(player);

        // 4. 부모의 파괴/소모 로직 실행
        base.Use(player);
    }

    private void ApplyRecoveryEffect(PlayerController player)
    {
        if (effectType == ActiveEffectType.None)
        {
            Debug.Log($"{itemName} 사용함. 수치: {value}");
            return;
        }

        PlayerConditions conditions = player.GetComponent<PlayerConditions>();
        if (conditions == null)
        {
            return;
        }

        int amount = Mathf.RoundToInt(value);
        ActiveEffectType effect = effectType;
        if (IsFood)
        {
            TraitProgress.Add(TraitCatalog.FoodEaten);
            if (TraitLoadout.IsEquipped(TraitId.RunningMan))
            {
                effect = ActiveEffectType.Stamina; // 기 '런닝맨': 음식이 체력 대신 스태미나 회복
            }
        }

        switch (effect)
        {
            case ActiveEffectType.Health:
                conditions.RecoverHealth(amount);
                break;
            case ActiveEffectType.Stamina:
                conditions.RecoverStamina(amount);
                break;
        }

        Debug.Log($"{itemName} 사용함. {effect} {amount} 회복.");
    }
}