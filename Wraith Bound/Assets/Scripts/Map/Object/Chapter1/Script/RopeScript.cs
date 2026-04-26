using UnityEngine;

public class RopeScript : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private float pushForce = 5f; // 힘의 세기 조절

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 멈추는 현상 방지: 수면 모드 해제
        rb.sleepThreshold = 0f;
        // 공기 저항 최소화
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 나를 건드린 물체의 이동 방향을 가져옴
        Rigidbody otherRb = other.GetComponent<Rigidbody>();
        Vector3 pushDir;

        if (otherRb != null && otherRb.linearVelocity.magnitude > 0.1f)
        {
            // 부딪힌 물체가 움직이는 방향 그대로 힘을 전달
            pushDir = otherRb.linearVelocity.normalized;
        }
        else
        {
            // 물체가 멈춰있거나 속도를 알 수 없으면, 
            // 상대적인 위치 차이를 계산해서 밀어냄 (3D 공간 전 방향)
            pushDir = (transform.position - other.transform.position).normalized;
        }

        // Y축(위아래) 힘은 너무 크면 승천하니까 살짝 줄여줌
        pushDir.y *= 0.5f;

        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}