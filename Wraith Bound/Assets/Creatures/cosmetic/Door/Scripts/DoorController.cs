using UnityEngine;
using UnityEngine.AI;

public class DoorController : MonoBehaviour
{
    public GameObject brokenDoor;

    private bool isBroken;

    void Start()
    {
        // 🔥 Player만 문 통과 가능하게 설정
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            Collider playerCol = player.GetComponent<Collider>();
            Collider doorCol = GetComponent<Collider>();

            if (playerCol != null && doorCol != null)
            {
                Physics.IgnoreCollision(playerCol, doorCol, true);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        BreakDoor();
    }

    void BreakDoor()
    {
        isBroken = true;

        // 기존 문 제거
        gameObject.SetActive(false);

        // 부서진 문 활성화
        brokenDoor.SetActive(true);

        // 부모 막힘 제거
        var parentCol = brokenDoor.GetComponent<Collider>();
        if (parentCol) Destroy(parentCol);

        var parentObs = brokenDoor.GetComponent<NavMeshObstacle>();
        if (parentObs) parentObs.enabled = false;

        foreach (Transform piece in brokenDoor.transform)
        {
            var rb = piece.GetComponent<Rigidbody>();
            var col = piece.GetComponent<Collider>();
            var obs = piece.GetComponent<NavMeshObstacle>();

            if (obs) obs.enabled = false;

            // 🔥 Player만 조각도 통과하게
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && col != null)
            {
                Collider playerCol = player.GetComponent<Collider>();
                if (playerCol != null)
                {
                    Physics.IgnoreCollision(playerCol, col, true);
                }
            }

            if (!rb) continue;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;

            Vector3 pushDir = (piece.forward + Vector3.down * 0.2f).normalized;

            rb.AddForce(pushDir * 5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
        }
    }
}