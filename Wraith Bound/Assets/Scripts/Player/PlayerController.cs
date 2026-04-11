using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine.InputSystem;
using NUnit.Framework;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    
    [SerializeField]
    public float moveSpeed; // 걷기 속도
    public float runSpeed; // 달리기 속도
    public float crouchSpeed; // 웅크리기 시 속도
    public float staminaDrainRate; // 달리기 시 소비할 스태미나
    public float applySpeed; // 초기화용 변수

    float h; // 좌우
    float v; // 상하

    [SerializeField]
    private bool isRun = false; // 달리기 상태
    private bool isCrouch = false; // 웅크리기 상태

    [SerializeField]
    private float crouchPosY; // 웅크리기 상태의 y축
    private float originPosY; // 걷기 상태의 y축 
    private float applyCrouchPosY; // 초기화용 변수

    
    PlayerConditions conditions; // PlayerConditions 컴포넌트 가져오기
    Rigidbody rigid;

    [Header("Rotate")]
    public float mouseSpeed; // 마우스 속도
    float xRotation;
    float yRotation;
    Camera cam; // 카메라 컴포넌트

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked; // 마우스 커서를 화면 안에서 고정
        UnityEngine.Cursor.visible = false; // 마우스 커서를 보이지 않도록 설정

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Rigidbody의 회전을 고정하여 물리 연산에 영향을 주지 않도록 설정

        conditions = GetComponent<PlayerConditions>();
        rigid = GetComponent<Rigidbody>();

        cam = Camera.main; // 메인 카메라를 할당

        // 초기화
        applySpeed = moveSpeed;
        originPosY = cam.transform.localPosition.y;
        applyCrouchPosY = originPosY;
    }

    void Update()
    {
        Run();
        Crouch();
        Rotate();
        Move();
    }

    // 1인칭 카메라 회전
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

    // 걷기(기본 상태)
    void Move()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        // 입력에 따라 이동 방향 벡터 계산
        Vector3 moveVec = transform.forward * v + transform.right * h;
        // 이동 벡터를 정규화하여 이동 속도와 시간 간격을 곱한 후 현재 위치에 더함
        rigid.linearVelocity = new Vector3(moveVec.x * applySpeed, rigid.linearVelocity.y, moveVec.z * applySpeed);
    }

    // 달리기
    void Run()
    {
        // 스태미나가 0보다 높을 때, 왼쪽 쉬프트를 누르면 달린다
        // 스태미나를 다 썼거나 왼쪽 쉬프트를 떼면 다시 걷는다
        if (Input.GetKey(KeyCode.LeftShift) && conditions.GetCurrentStamina() > 0f && isRun == false)
        {
            isRun = true;
            conditions.ConsumeStamina(Time.deltaTime);
            applySpeed = runSpeed;
        }

        else if (Input.GetKeyUp(KeyCode.LeftShift) || conditions.GetCurrentStamina() <= 0f)
        {
            isRun = false;
            applySpeed = moveSpeed;
            conditions.StartStaminaRegen();



            /*디버그 로그(Debug.Log)로 매 프레임마다 깎이는 스태미나 수치를 확인 중이시라면, 0.01씩 깎이는 것이 정상일 수 있습니다.

원리: 초당 10을 소비한다는 것은 1초에 걸쳐 총 10이 깎인다는 의미입니다. 만약 게임이 1000프레임(FPS)으로 돌아가고 있다면, 1프레임당 10 / 1000 = 0.01씩 깎이게 됩니다.
확인 방법: 디버그 로그가 0.01씩 찍히더라도, 스태미나 바 UI가 10초에 걸쳐 서서히 0(최대치 100 기준)으로 줄어든다면 정상적으로 작동하고 있는 것입니다.  */
            Debug.Log("현재 스태미나: " + conditions.GetCurrentStamina()); 
        }
    }

    // 웅크리기
    void Crouch()
    {
        // 왼쪽 컨트롤을 누르면 웅크린다
        // 왼쪽 컨트롤을 떼면 다시 걷는다
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouch = true;
            applySpeed = crouchSpeed;
            applyCrouchPosY = crouchPosY;
        }
        else if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            isCrouch = false;
            applySpeed = moveSpeed;
            applyCrouchPosY = originPosY;
        }

        // 코루틴 시작
        StartCoroutine(CrouchCoroutine());
    }

    // 부드러운 웅크리기 동작을 위한 코루틴
    IEnumerator CrouchCoroutine()
    {
        float _posY = cam.transform.localPosition.y;
        int count = 0;

        // 카메라의 현재 위치에서 출발하여 applyCrounchPosY와 일치할 때까지 계속해서 Lerp로 업데이트
        while(_posY != applyCrouchPosY)
        {
            count++;
            _posY = Mathf.Lerp(_posY, applyCrouchPosY, 0.2f);
            cam.transform.localPosition = new Vector3(0, _posY, 0);
            if(count > 15)
            break;

            yield return null;
        }
        cam.transform.localPosition = new Vector3(0, applyCrouchPosY, 0);
    }
  
}
