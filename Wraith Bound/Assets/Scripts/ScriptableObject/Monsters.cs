using UnityEngine;

public enum MonsterType
{
    Monster // 괴물
    ,Ghost // 귀신
}

[CreateAssetMenu(fileName = "NewMonster", menuName = "Custom/Monsters")]
public class Monsters : ScriptableObject
{
    [Header("기본 정보")]
    public int id; // 아이디
    public MonsterType monsterType; // Monster, Ghost
    public string description; // 설명
    
    [Header("수치 데이터")]
    public float hp;
    public float moveSpeed; // 이동 속도
    public float detectRange;
    [Range(0, 360)]
    public float viewAngle;
    public float checkInterval;

    public LayerMask obstacleLayer; // 벽 판정 레이어 (벽 뒤에 숨었는지)
    public LayerMask playerLayer; // 플레이어 판정 레이어

    public float targetLostTIme; // 플레이어가 시야에서 사라진 후 얼마나 더 추적하는 지
    public float hearingRange; // 청각 범위
}
