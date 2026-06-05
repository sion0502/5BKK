using UnityEngine;

public class FloatingFragment : MonoBehaviour
{
    [Header("Movement Range")]
    public Vector3 movementRange = new Vector3(0.2f, 0.2f, 0.2f);

    [Header("Speed")]
    public float moveSpeed = 1f;

    private Vector3 startPos;
    private Vector3 targetPos;

    private void Start()
    {
        startPos = transform.localPosition;
        SetNewTarget();
    }

    private void Update()
    {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, targetPos) < 0.01f)
        {
            SetNewTarget();
        }
    }

    private void SetNewTarget()
    {
        targetPos = startPos + new Vector3(
            Random.Range(-movementRange.x, movementRange.x),
            Random.Range(-movementRange.y, movementRange.y),
            Random.Range(-movementRange.z, movementRange.z)
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 center = Application.isPlaying
            ? transform.parent.TransformPoint(startPos)
            : transform.position;

        Gizmos.DrawWireCube(
            center,
            movementRange * 2f
        );
    }
}