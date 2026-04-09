using UnityEngine;
using System.Collections.Generic;

public class RadarSystem : MonoBehaviour
{
    [Header("연결 설정")]
    public Transform player; // 이제 비워두셔도 자동으로 찾습니다.
    public RectTransform sonarUI;
    public GameObject dotPrefab;
    public RectTransform scanLine;
    public Equipment itemData;

    private Dictionary<Transform, GameObject> enemyIcons = new Dictionary<Transform, GameObject>();

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                // playerObj의 transform을 player 변수에 대입해야 합니다.
                player = playerObj.transform;
            }
        }
    }

    void Update()
    {
        // 안전장치
        if (itemData == null || player == null || scanLine == null) return;

        float currentRange = itemData.range;

        // 태그로 모든 Ghost를 찾음
        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");

        foreach (GameObject ghost in ghosts)
        {
            if (ghost == null) continue;

            // 1. 플레이어 기준 적의 상대적 위치 계산
            Vector3 relativePos = player.InverseTransformPoint(ghost.transform.position);
            float distance = new Vector2(relativePos.x, relativePos.z).magnitude;

            if (distance <= currentRange)
            {
                if (!enemyIcons.ContainsKey(ghost.transform))
                {
                    // 부모(Canvas/RadarUI) 아래에 점 생성
                    GameObject icon = Instantiate(dotPrefab, transform);
                    enemyIcons.Add(ghost.transform, icon);
                }

                // 2. 좌표 변환 (반지름 145 기준)
                float xPos = (relativePos.x / currentRange) * 145f;
                float yPos = (relativePos.z / currentRange) * 145f;

                enemyIcons[ghost.transform].GetComponent<RectTransform>().localPosition = new Vector2(xPos, yPos);

                // 3. 스캔 효과 각도 계산
                float angleToEnemy = Mathf.Atan2(relativePos.x, relativePos.z) * Mathf.Rad2Deg;
                float currentScanAngle = scanLine.eulerAngles.z;

                // 각도 차이 계산 (DeltaAngle 사용으로 0도-360도 경계 해결)
                if (Mathf.Abs(Mathf.DeltaAngle(-angleToEnemy, currentScanAngle)) < 10f)
                {
                    if (enemyIcons[ghost.transform].TryGetComponent<RadarDot>(out RadarDot dot))
                    {
                        dot.ShowDot();
                    }
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

        // [추가] 고스트가 파괴되었을 때 아이콘 정리 (Missing Reference 방지)
        CleanUpIcons();
    }

    void CleanUpIcons()
    {
        List<Transform> keysToRemove = new List<Transform>();
        foreach (var key in enemyIcons.Keys)
        {
            if (key == null) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            if (enemyIcons[key] != null) Destroy(enemyIcons[key]);
            enemyIcons.Remove(key);
        }
    }
}