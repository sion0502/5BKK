using UnityEngine;
using System.Collections.Generic;

public class RadarSystem : MonoBehaviour
{
    public Transform player;
    public float radarRange = 20f;
    public RectTransform sonarUI;
    public GameObject dotPrefab;
    public RectTransform scanLine;

    private Dictionary<Transform, GameObject> enemyIcons = new Dictionary<Transform, GameObject>();

    void Update()
    {
        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");

        foreach (GameObject ghost in ghosts)
        {
            // 1. 플레이어 기준 적의 상대적 위치 계산 (회전 반영)
            // InverseTransformPoint는 월드 좌표를 플레이어의 로컬 좌표로 바꿔줍니다.
            Vector3 relativePos = player.InverseTransformPoint(ghost.transform.position);

            // 2D 평면 거리 (X와 Z축만 사용)
            float distance = new Vector2(relativePos.x, relativePos.z).magnitude;

            if (distance <= radarRange)
            {
                if (!enemyIcons.ContainsKey(ghost.transform))
                {
                    GameObject icon = Instantiate(dotPrefab, transform);
                    enemyIcons.Add(ghost.transform, icon);
                }

                // 2. 좌표 변환 (반지름 145 기준)
                // relativePos.x가 왼쪽/오른쪽, relativePos.z가 앞/뒤를 나타냅니다.
                float xPos = (relativePos.x / radarRange) * 145f;
                float yPos = (relativePos.z / radarRange) * 145f;

                enemyIcons[ghost.transform].GetComponent<RectTransform>().localPosition = new Vector2(xPos, yPos);

                // 3. 스캔 효과 각도 계산
                // 이제 relativePos 자체가 플레이어 기준이므로 계산이 훨씬 정확해집니다.
                float angleToEnemy = Mathf.Atan2(relativePos.x, relativePos.z) * Mathf.Rad2Deg;
                float currentScanAngle = scanLine.eulerAngles.z;

                if (Mathf.Abs(Mathf.DeltaAngle(-angleToEnemy, currentScanAngle)) < 10f)
                {
                    enemyIcons[ghost.transform].GetComponent<RadarDot>().ShowDot();
                }
            }
            else
            {
                if (enemyIcons.ContainsKey(ghost.transform))
                {
                    Destroy(enemyIcons[ghost.transform]);
                    enemyIcons.Remove(ghost.transform);
                }
            }
        }
    }
}