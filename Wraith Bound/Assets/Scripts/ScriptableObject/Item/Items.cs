using UnityEngine;

public enum ItemType
{
    Active,  // 소모품 (체력 회복 등)
    Equip,   // 장비 (스마트폰, 손전등 등)
    Passive  // 패시브 (능력치 강화 등)
}

public abstract class Items : ScriptableObject // 추상 클래스
{
    [Header("기본 데이터")]
    public int id; // 아이템 아이디
    public string itemName; // 아이템 이름
    public ItemType type; // Active, Equip, Passive
    [TextArea] public string description; // 설명
    public Sprite icon; // 아이콘
    public int maxCount; // 최대 소지량

    [Header("시스템 설정")]
    public bool destroyOnUse; // 사용 시 인벤토리에서 제거 여부
    [Range(0, 100)] public float breakageChance; // 사용 시 고장/파괴될 확률

    [Header("시각적 요소")]
    public GameObject itemPrefab; // 획득 시 생성할 프리팹 (손에 들 모델 등)

    // [추가] 이 아이템을 획득했을 때 ItemHolder에 생성할지 여부
    public bool showInHand;

    // 런타임에 생성된 실제 오브젝트 보관용
    [HideInInspector] public GameObject spawnedInstance;

    // 아이템 사용 함수 (플레이어 정보를 인자로 받음)
    public virtual void Use(PlayerController player)
    {
        // 1. 시각적 실체(프리팹) 토글
        if (spawnedInstance != null)
        {
            bool isActive = spawnedInstance.activeSelf;
            spawnedInstance.SetActive(!isActive);
            Debug.Log($"{itemName} 상태 변경: {!isActive}");
        }

        // 2. [수정] 팀원분의 인벤토리 매니저 가져오기
        var inv = player.GetComponent<InventroyManager>();
        if (inv == null) return;

        // 3. 확률적 파괴 체크
        if (breakageChance > 0 && Random.Range(0f, 100f) < breakageChance)
        {
            Debug.Log($"{itemName}이(가) 사용 중 파괴되었습니다.");
            // 팀원분의 인벤토리 매니저에서 아이템 제거 호출
            inv.RemoveItem(this, 1);
            return;
        }

        // 4. 즉시 소모 체크
        if (destroyOnUse)
        {
            // 팀원분의 인벤토리 매니저에서 아이템 제거 호출
            inv.RemoveItem(this, 1);
        }
    }
}