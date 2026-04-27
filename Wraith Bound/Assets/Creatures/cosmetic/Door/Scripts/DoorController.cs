using UnityEngine;
using UnityEngine.AI;

public class DoorController : MonoBehaviour
{
    public GameObject brokenDoor;

    NavMeshObstacle obstacle;
    Collider doorCollider;

    bool isBroken = false;

    void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
        doorCollider = GetComponent<Collider>();
    }

    void Start()
    {
        IgnorePlayerCollision();
    }

    void IgnorePlayerCollision()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            Collider[] playerCols = player.GetComponentsInChildren<Collider>();

            foreach (Collider col in playerCols)
            {
                Physics.IgnoreCollision(col, doorCollider);
            }
        }
    }

    public bool IsBroken()
    {
        return isBroken;
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        Break();
    }

    void Break()
    {
        isBroken = true;

        if (brokenDoor != null)
        {
            RaycastHit hit;
            Vector3 startPos = transform.position + Vector3.up * 2f;

            if (Physics.Raycast(startPos, Vector3.down, out hit, 5f))
            {
                Collider col = brokenDoor.GetComponent<Collider>();

                float height = 0.5f;

                if (col != null)
                    height = col.bounds.extents.y;

                // 🔥 바닥보다 살짝 위에서 시작 (핵심)
                Vector3 spawnPos = hit.point + Vector3.up * (height + 0.05f);

                brokenDoor.transform.position = spawnPos;
                brokenDoor.transform.rotation = transform.rotation;
            }
            else
            {
                brokenDoor.transform.position = transform.position + Vector3.up;
                brokenDoor.transform.rotation = transform.rotation;
            }

            brokenDoor.SetActive(true);

            // 🔥 물리 초기 안정화 (중요)
            Rigidbody rb = brokenDoor.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        gameObject.SetActive(false);

        if (obstacle != null)
            obstacle.enabled = false;
    }
}