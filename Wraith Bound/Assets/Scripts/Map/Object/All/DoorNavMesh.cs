using UnityEngine;
using UnityEngine.AI;

public class DoorNavMesh : MonoBehaviour
{
    private DoorClick doorClick;
    private Collider[] cols;

    private void Start()
    {
        doorClick = GetComponent<DoorClick>();
        cols = GetComponents<Collider>();
        DoorNavMeshUtility.EnsureCarveObstacle(transform);
    }

    private void Update()
    {
        if (doorClick == null)
            return;

        bool blocked = !doorClick.IsOpen() && !doorClick.IsBroken();

        if (cols != null)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = blocked;
            }
        }

        DoorNavMeshUtility.SetNavMeshBlocked(transform, blocked);
    }
}
