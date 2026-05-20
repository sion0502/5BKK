using UnityEngine;

[CreateAssetMenu(menuName = "Custom/ObstacleData", fileName = "Obstacle Data")]
public class ObstacleData : ScriptableObject
{
    // 방해물의 데미지
    public int damage = 5;

    // 방해물의 공격 주기(0.5초)
    public float timeBetAttack = 0.5f;
}