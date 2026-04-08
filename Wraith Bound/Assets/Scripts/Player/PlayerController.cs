using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;

    public float moveSpeed;
    float h;
    float v;

    [Header("Rotate")]
    public float mouseSpeed;
    float xRotation;
    float yRotation;
    Camera cam;

    // --- 추가된 변수 (삭제 금지) ---
    [Header("Interaction")]
    public float interactDistance = 3f; // 아이템을 주울 수 있는 거리
    // ----------------------------

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked; // 마우스 커서를 화면 안에서 고정
        UnityEngine.Cursor.visible = false; // 마우스 커서를 보이지 않도록 설정

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Rigidbody의 회전을 고정하여 물리 연산에 영향을 주지 않도록 설정

        cam = Camera.main; // 메인 카메라를 할당
    }

    void Update()
    {
        Rotate();
        Move();

        // --- 추가된 로직: E키 입력 시 상호작용 시도 ---
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
        // ------------------------------------------
    }

    // --- 추가된 함수: 앞의 아이템을 감지하고 줍는 기능 ---
    void TryInteract()
    {
        // 카메라의 정중앙(0.5, 0.5)에서 앞으로 레이(광선)를 쏩니다.
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // 설정한 사거리(interactDistance) 내에 물체가 부딪혔는지 확인
        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            // 맞은 오브젝트에 ItemObject 컴포넌트가 있는지 확인
            ItemObject targetItem = hit.collider.GetComponent<ItemObject>();

            if (targetItem != null)
            {
                // 아이템 오브젝트의 획득 함수 호출
                targetItem.OnPickedUp();
            }
        }
    }
    // -----------------------------------------------

    void Rotate()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSpeed * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSpeed * Time.deltaTime;

        yRotation += mouseX; // 마우스 x축 입력에 따라 수평 회전 값을 조정
        xRotation -= mouseY; // 마우스 y축 입력에 따라 수직 회전 값을 조정

        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // 수직 회전 값을 -90도에서 90도 사이로 제한

        cam.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0); // 카메라의 회전을 조절
        transform.rotation = Quaternion.Euler(0, yRotation, 0); // 플레이어 캐릭터의 회전을 조절
    }

    void Move()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        // 입력에 따라 이동 방향 벡터 계산
        Vector3 moveVec = transform.forward * v + transform.right * h;

        // 이동 벡터를 정규화하여 이동 속도와 시간 간격을 곱한 후 현재 위치에 더함
        // (moveVec.normalized를 사용하여 대각선 이동 시 속도가 빨라지는 것을 방지)
        if (moveVec.magnitude > 0)
        {
            transform.position += moveVec.normalized * moveSpeed * Time.deltaTime;
        }
    }
}