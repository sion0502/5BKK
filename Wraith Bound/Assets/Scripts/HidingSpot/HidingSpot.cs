using UnityEngine;

public class HidingSpot : MonoBehaviour
{
    [Tooltip("플레이어 카메라가 위치할 트랜스폼 (캐비넷 내부의 눈높이)")]
    public Transform hideCameraPosition;

    [Tooltip("플레이어가 밖으로 나왔을 때 발이 닿을 트랜스폼")]
    public Transform exitPosition;

    [Tooltip("숨어있을 때 좌우로 고개를 돌릴 수 있는 최대 각도")]
    public float lookLimitX;

    [Tooltip("숨어있을 때 상하로 고개를 돌릴 수 있는 최대 각도")]
    public float lookLimitY;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
