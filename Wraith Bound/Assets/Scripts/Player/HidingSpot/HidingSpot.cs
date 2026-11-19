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

    [Tooltip("켜면 앞면이 아니라 어느 방향에서 조준해도 숨을 수 있음 (트롤리 — 2026-11-19 사용자 요청). 끄면 앞면 maxFrontAngle 안에서만")]
    public bool allowAnySide;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
