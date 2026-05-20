using UnityEngine;
using System.Collections;

public class Obstacle : MonoBehaviour
{
    private PlayerConditions playerConditions;
    private Coroutine stayCoroutine; // 실행 중인 코루틴을 저장할 변수

    public ObstacleData obstacleData; // 방해물의 현재 데이터

    // 영역에 들어오는 순간 코루틴 시작
    void OnTriggerEnter(Collider other)
    {
        if (stayCoroutine != null) return;

        if (other.CompareTag("Player"))
        {
            // 충돌한 플레이어 객체에서 PlayerConditions 컴포넌트 추출 시도
            PlayerConditions playerConditions = other.GetComponent<PlayerConditions>();
            // PlayerCondtitions 컴포넌트를 추출하였다면
            if (playerConditions != null)
            {
                // PlayerConditions를 코루틴 매개변수로 전달하며 코루틴 시작
                stayCoroutine = StartCoroutine(ZoneRoutine(playerConditions));
            }
        }
    }

    // 영역에서 빠져나가는 순간 코루틴 정지
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (stayCoroutine != null)
            {
                StopCoroutine(stayCoroutine);
                stayCoroutine = null;
            }
        }
    }

    // 0.5초마다 반복될 실제 로직
    IEnumerator ZoneRoutine(PlayerConditions conditions)
    {
        while (true) // 영역에 있는 동안 무한 반복
        {
            // 플레이어가 살아있다면
            if (!conditions.dead)
            {
                // 방해물의 데이터 속 damage만큼 데미지를 줌
                conditions.onDamage(obstacleData.damage);
                Debug.Log("0.5초마다 데미지");
            }
            
            // 0.5초 대기 
            yield return new WaitForSeconds(0.5f);
        }
    }
}
