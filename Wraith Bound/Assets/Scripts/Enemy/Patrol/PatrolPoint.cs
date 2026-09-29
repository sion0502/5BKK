using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 맵에 배치하는 순찰 지점.
/// </summary>
public class PatrolPoint : MonoBehaviour
{
    [SerializeField] float navMeshSnapRadius = 2f;

    public Vector3 Position => transform.position;

    void Awake()
    {
        SnapToNavMesh();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
            SnapToNavMesh();
    }
#endif

    void SnapToNavMesh()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return;

        if ((hit.position - transform.position).sqrMagnitude <= 0.0001f)
            return;

        transform.position = hit.position;
    }
}
