using UnityEngine;

public enum ItemType
{
    Active // 소모품
    ,Equip // 장비 (카메라, 손전등, 나침반, 핸드폰)
    ,Passive // 패시브
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Custom/Items")]
public class Items : ScriptableObject
{
    [Header("기본 데이터")]
    public int id; // 아이템 아이디
    public string itemName; // 아이템 이름
    public ItemType type; // Active, Equip, Passive
    [TextArea] public string description; // 설명
    public Sprite icon; // 아이콘
    public bool isConsumable;

    [Header("수치 데이터")]
    public float value; // 회복량, 지속시간, 스테미나 수치 등
    public int maxCount; // 최대 소지량

}
