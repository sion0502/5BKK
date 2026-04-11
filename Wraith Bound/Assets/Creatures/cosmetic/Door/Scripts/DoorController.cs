using UnityEngine;

public class DoorController : MonoBehaviour
{
    public GameObject brokenDoorPrefab;
    public int hp = 10;

    private bool isBroken = false;

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        hp -= damage;

        if (hp <= 0)
        {
            BreakDoor();
        }
    }

    void BreakDoor()
    {
        isBroken = true;

        var obs = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obs != null) obs.enabled = false;

        GameObject broken = Instantiate(
            brokenDoorPrefab,
            transform.position + Vector3.up * 2f,
            transform.rotation
        );

        broken.SetActive(true);

        Rigidbody[] rbs = broken.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rbs)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // 🔥 핵심: 회전 + 랜덤 방향
            Vector3 force = transform.forward * Random.Range(4f, 7f)
                            + Vector3.up * Random.Range(3f, 5f)
                            + transform.right * Random.Range(-2f, 2f);

            rb.AddForce(force, ForceMode.Impulse);

            // 🔥 회전 힘 추가 (이게 핵심)
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }
}