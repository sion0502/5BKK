using UnityEngine;

public class DoorNavMesh : MonoBehaviour
{
    DoorClick doorClick;

    Collider[] cols;

    void Start()
    {
        doorClick = GetComponent<DoorClick>();

        cols = GetComponents<Collider>();
    }

    void Update()
    {
        if (doorClick == null)
            return;

        bool open = doorClick.IsOpen();

        foreach (Collider col in cols)
        {
            col.enabled = !open;
        }
    }
}