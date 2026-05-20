using UnityEngine;

public class HeldItemSway : MonoBehaviour
{
    [Header("Sway")]
    [SerializeField] private float positionAmount = 0.025f;
    [SerializeField] private float rotationAmount = 3f;
    [SerializeField] private float smooth = 10f;

    [Header("Limit")]
    [SerializeField] private float maxPosition = 0.06f;
    [SerializeField] private float maxRotation = 6f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private void OnEnable()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }

    private void OnDisable()
    {
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
    }

    private void LateUpdate()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector3 targetPosition = new Vector3(
            Mathf.Clamp(-mouseX * positionAmount, -maxPosition, maxPosition),
            Mathf.Clamp(-mouseY * positionAmount, -maxPosition, maxPosition),
            0f
        );

        Quaternion targetRotation = Quaternion.Euler(
            Mathf.Clamp(mouseY * rotationAmount, -maxRotation, maxRotation),
            Mathf.Clamp(-mouseX * rotationAmount, -maxRotation, maxRotation),
            Mathf.Clamp(mouseX * rotationAmount, -maxRotation, maxRotation)
        );

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            initialLocalPosition + targetPosition,
            Time.deltaTime * smooth
        );

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            initialLocalRotation * targetRotation,
            Time.deltaTime * smooth
        );
    }
}
