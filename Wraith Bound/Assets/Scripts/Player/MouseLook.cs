using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity;
    [SerializeField] private Transform playerBody; // 플레이어 최상위 객체 할당

    private float xRotation = 0f;

    private void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked; // 마우스 커서를 화면 안에서 고정
        UnityEngine.Cursor.visible = false; // 마우스 커서를 보이지 않도록 설정
    }

    private void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        // 수직 회전 (상하 시점)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 카메라는 상하로만 회전 (로컬 X축)
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 플레이어 본체는 좌우로 회전 (로컬 Y축)
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
