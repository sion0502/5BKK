using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("조작 설정")]
    public float moveSpeed = 5f;
    public float mouseSpeed = 200f;
    public float interactDistance = 3f;

    [Header("참조")]
    private Camera cam;
    private InventoryManager inv;
    private float xRotation = 0f;

    void Start()
    {
        cam = Camera.main;
        inv = GetComponent<InventoryManager>();

        // 마우스 커서 설정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 물리 회전 고정 (쓰러짐 방지)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.freezeRotation = true;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();

        if (Input.GetKeyDown(KeyCode.E)) TryInteract();

        // 휠/숫자키 선택 처리
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0) inv.HandleScroll(scroll);

        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) inv.SelectByIndex(i);
        }

        // [아이템 사용 및 꺼내기]
        if (Input.GetMouseButtonDown(0) && inv.currentItem != null)
        {
            // 1. 손에 아무것도 안 들고 있다면? -> 꺼내기
            if (inv.currentItem.spawnedInstance != null && !inv.currentItem.spawnedInstance.activeSelf)
            {
                inv.currentItem.spawnedInstance.SetActive(true);
                return;
            }

            // 2. 이미 들고 있다면? -> 사용하기
            inv.currentItem.Use(this);
        }

        // 우클릭하면 다시 집어넣기 (필요할 때 화면 가리는 거 치우기용)
        if (Input.GetMouseButtonDown(1))
        {
            if (inv.currentItem != null && inv.currentItem.spawnedInstance != null)
            {
                inv.currentItem.spawnedInstance.SetActive(false);
            }
        }
    }

    // 시점 회전 함수
    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSpeed * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSpeed * Time.deltaTime;

        // 좌우 회전
        transform.Rotate(Vector3.up * mouseX);

        // 상하 회전 및 각도 제한
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // 이동 함수
    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        transform.Translate(move * moveSpeed * Time.deltaTime, Space.World);
    }

    // 상호작용 함수
    void TryInteract()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // IInteractable 인터페이스를 가진 오브젝트(ItemObject 등)를 찾음
            if (hit.collider.TryGetComponent<IInteractable>(out var target))
            {
                target.Interact(this);
            }
        }
    }

    // 아이템 제거 통로 (Items SO에서 호출용)
    public void RemoveItemFromInventory(Items item)
    {
        if (inv != null) inv.RemoveItem(item);
    }
}