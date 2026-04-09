using UnityEngine;

public class DoorController : MonoBehaviour
{
    public GameObject fallingDoorPrefab; // 쓰러지는 문
    public int hp = 3;

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

        // 🔥 쓰러지는 문 생성
        GameObject fallDoor = Instantiate(
            fallingDoorPrefab,
            transform.position,
            transform.rotation
        );

        // 🔥 앞으로 쓰러지게 힘
        Rigidbody rb = fallDoor.GetComponent<Rigidbody>();
        rb.AddForce(transform.forward * 8f, ForceMode.Impulse);

        Destroy(gameObject);
    }
}