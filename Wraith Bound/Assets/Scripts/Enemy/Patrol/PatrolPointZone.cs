using UnityEngine;

/// <summary>
/// 한 구역의 순찰 포인트 묶음. 자식 PatrolPoint를 자동 수집한다.
/// </summary>
public class PatrolPointZone : MonoBehaviour
{
    [SerializeField] PatrolPoint[] points;

    public PatrolPoint[] Points
    {
        get
        {
            if (points == null || points.Length == 0)
                points = GetComponentsInChildren<PatrolPoint>();

            return points;
        }
    }

    public bool HasPoints
    {
        get
        {
            PatrolPoint[] list = Points;
            return list != null && list.Length > 0;
        }
    }
}
