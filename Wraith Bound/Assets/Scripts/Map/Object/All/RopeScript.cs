using UnityEngine;

public class RopeScript : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] private float pushForce = 5f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody가 없습니다!");
            return;
        }

        rb.sleepThreshold = 0f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (rb == null) return;

        Rigidbody otherRb = other.GetComponent<Rigidbody>();

        Vector3 pushDir;

        if (otherRb != null && otherRb.linearVelocity.magnitude > 0.1f)
        {
            pushDir = otherRb.linearVelocity.normalized;
        }
        else
        {
            pushDir = (transform.position - other.transform.position).normalized;
        }

        pushDir.y *= 0.5f;

        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}