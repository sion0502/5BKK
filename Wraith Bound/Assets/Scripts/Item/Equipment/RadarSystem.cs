using UnityEngine;
using System.Collections.Generic;

public class RadarSystem : MonoBehaviour
{
    public Transform player;
    // public float radarRange; // 이제 이 변수 대신 itemData.Range를 사용합니다.
    public RectTransform sonarUI;
    public GameObject dotPrefab;
    public RectTransform scanLine;
    public Equipment itemData; // 연결하신 SO 데이터

    private Dictionary<Transform, GameObject> enemyIcons = new Dictionary<Transform, GameObject>();

    void Update()
    {
        // 안전장치: 데이터가 연결되지 않았으면 실행하지 않음
        if (itemData == null || player == null) return;

        // SO에서 현재 장비의 탐지 범위를 가져옵니다.
        float currentRange = itemData.range;

        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");

        foreach (GameObject ghost in ghosts)
        {
            // 1. 플레이어 기준 적의 상대적 위치 계산 (회전 반영)
            Vector3 relativePos = player.InverseTransformPoint(ghost.transform.position);

            // 2D 평면 거리 (X와 Z축만 사용)
            float distance = new Vector2(relativePos.x, relativePos.z).magnitude;

            // SO의 Range 값을 기준으로 감지 여부 판별
            if (distance <= currentRange)
            {
                if (!enemyIcons.ContainsKey(ghost.transform))
                {
                    GameObject icon = Instantiate(dotPrefab, transform);
                    enemyIcons.Add(ghost.transform, icon);
                }

                // 2. 좌표 변환 (반지름 145 기준)
                // 분모에 radarRange 대신 SO에서 가져온 currentRange를 넣어야 정확한 비율로 찍힙니다.
                float xPos = (relativePos.x / currentRange) * 145f;
                float yPos = (relativePos.z / currentRange) * 145f;

                enemyIcons[ghost.transform].GetComponent<RectTransform>().localPosition = new Vector2(xPos, yPos);

                // 3. 스캔 효과 각도 계산
                float angleToEnemy = Mathf.Atan2(relativePos.x, relativePos.z) * Mathf.Rad2Deg;
                float currentScanAngle = scanLine.eulerAngles.z;

                if (Mathf.Abs(Mathf.DeltaAngle(-angleToEnemy, currentScanAngle)) < 10f)
                {
                    // 점의 ShowDot 함수 실행 (반짝임 효과)
                    if (enemyIcons[ghost.transform].TryGetComponent<RadarDot>(out RadarDot dot))
                    {
                        dot.ShowDot();
                    }
                }
            }
            else
            {
                // 범위 밖으로 나가면 삭제
                if (enemyIcons.ContainsKey(ghost.transform))
                {
                    Destroy(enemyIcons[ghost.transform]);
                    enemyIcons.Remove(ghost.transform);
                }
            }
        }
    }
}