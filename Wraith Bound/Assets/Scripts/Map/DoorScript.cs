using UnityEngine;

public class DoorScript : MonoBehaviour
{
    public GameObject topPart;
    public GameObject bottomPart;

    public int hp = 3;

    bool isBroken = false;

    void Start()
    {
        // 시작 시 조각 비활성화
        topPart.SetActive(false);
        bottomPart.SetActive(false);

        // 혹시 Rigidbody 있으면 고정
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;
    }

    public void TakeDamage(int dmg)
    {
        if (isBroken) return;

        hp -= dmg;

        if (hp <= 0)
        {
            BreakDoor();
        }
    }

    void BreakDoor()
    {
        if (isBroken) return;
        isBroken = true;

        // 통짜 문 제거
        gameObject.SetActive(false);

        // 반토막 활성화
        topPart.SetActive(true);
        bottomPart.SetActive(true);

        Rigidbody topRb = topPart.GetComponent<Rigidbody>();
        Rigidbody bottomRb = bottomPart.GetComponent<Rigidbody>();

        // 초기 속도 제거
        topRb.linearVelocity = Vector3.zero;
        bottomRb.linearVelocity = Vector3.zero;

        // 힘 + 회전
        topRb.AddForce(Vector3.up * 300);
        bottomRb.AddForce(Vector3.down * 150);

        topRb.AddTorque(Vector3.right * 200);
        bottomRb.AddTorque(Vector3.left * 200);
    }
}