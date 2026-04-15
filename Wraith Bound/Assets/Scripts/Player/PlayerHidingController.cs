using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHidingController : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    private PlayerController playerController; // 기존 1인칭 이동 스크립트 (비활성화 용도)
    private MouseLook mouseLook; // 기존 1인칭 카메라 스크립트 (비활성화 용도)

    [Header("Raycast Settings")]
    [Tooltip("상호작용 가능한 최대 거리")]
    public float interactionDistance = 2.5f;
    [Tooltip("상호작용 레이캐스트가 감지할 레이어 (최적화를 위해 설정 권장)")]
    public LayerMask interactableLayer;

    [Header("Settings")]
    public float transitionDuration = 0.5f; // 들어갈 때/나올 때 걸리는 시간
    public float mouseSensitivity = 2f;

    [Header("Hiding Settings")]
    [Tooltip("캐비넷 정면에서 얼마나 벗어난 각도까지 상호작용을 허용할지")]
    public float maxFrontAngle = 45f;

    private CharacterController characterController;
    private HidingSpot currentSpot;

    private bool isHiding = false;
    private bool isTransitioning = false; // 이동 중 중복 입력 방지

    // 숨어있을 때의 시야각 계산용
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
        mouseLook = playerCamera.GetComponent<MouseLook>();
    }

    private void Update()
    {
        if (isTransitioning) return;

        // 숨어있지 않을 때 매 프레임 레이캐스트 타겟 감지
        if (!isHiding)
        {
            DetectHidingSpot();
        }

        // 상호작용 입력
        if (Input.GetButtonDown("Interact"))
        {
            if (isHiding)
            {
                StartCoroutine(ExitHidingRoutine());
            }
            else if (currentSpot != null)
            {
                playerController.isCrouching = false;
                playerController.isRun = false;
                StartCoroutine(EnterHidingRoutine());
            }
        }
    }

    private void DetectHidingSpot()
    {
        // 카메라 위치에서 정면으로 향하는 레이 생성
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        // 화면 중앙에서 레이캐스트 발사
        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            // 부딪힌 오브젝트에서 HidingSpot 컴포넌트 찾기
            HidingSpot spot = hit.collider.GetComponent<HidingSpot>();
            if (spot != null)
            {
                // hit.normal: 레이저가 부딪힌 표면이 바라보는 수직 방향
                // spot.transform.forward: 캐비넷 오브젝트의 파란색 화살표(정면) 방향
                // 두 방향 사이의 각도를 계산합니다.
                float hitAngle = Vector3.Angle(hit.normal, spot.transform.forward);

                // 각도 차이가 설정한 허용 각도(maxFrontAngle) 이내일 때만 정면으로 인정
                if (hitAngle <= maxFrontAngle)
                {
                    currentSpot = spot;
                    Debug.Log("숨기 가능한 오브젝트 감지됨!");
                    //TODO: 이곳에서 UI 매니저를 호출해 화면에 "E: 숨기" 같은 텍스트 띄우기
                    return;
                }
            }
        }

        // 부딪힌게 없거나 HidingSpot이 아니면 타겟 해제
        currentSpot = null;
        //TODO: UI 텍스트 숨김 처리
    }

    private IEnumerator EnterHidingRoutine()
    {
        isTransitioning = true;
        isHiding = true;

        // 1. 물리 충돌 및 기존 조작 스크립트, 카메라 회전 스크립트 비활성화
        characterController.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // 2. 초기 위치/회전값 저장
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion startCamRot = playerCamera.rotation;

        // 3. 목표 위치/회전값 설정 (캐비넷 안)
        // 플레이어 몸체는 카메라 XZ 위치로, 높이는 캐비넷 바닥 기준으로 맞춤 (필요시 조정)
        Vector3 targetPos = currentSpot.hideCameraPosition.position - (playerCamera.position - transform.position);
        Quaternion targetRot = currentSpot.hideCameraPosition.rotation;

        float elapsedTime = 0f;

        // 4. 부드러운 보간 (Lerp) 이동
        while (elapsedTime < transitionDuration)
        {
            float t = elapsedTime / transitionDuration;
            // SmoothStep으로 더 자연스러운 감속 효과 부여 가능함
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            playerCamera.rotation = Quaternion.Slerp(startCamRot, targetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 5. 최종 위치 고정 및 시야각 초기화
        transform.position = targetPos;
        transform.rotation = targetRot;
        playerCamera.rotation = targetRot;

        currentYaw = 0f;
        currentPitch = 0f;

        isTransitioning = false;

        // 숨어있는 동안 제한된 시야 제어
        if (isHiding && !isTransitioning)
        {
            HandleRestrictedLook();
        }
    }

    private IEnumerator ExitHidingRoutine()
    {
        isTransitioning = true;

        Vector3 startPos = transform.position;
        Quaternion startCamRot = playerCamera.rotation;

        Vector3 targetPos = currentSpot.exitPosition.position;
        Quaternion targetRot = currentSpot.exitPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t = elapsedTime / transitionDuration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            playerCamera.rotation = Quaternion.Slerp(startCamRot, targetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        playerCamera.localRotation = Quaternion.identity; // 부모-자식 회전 초기화
        transform.rotation = targetRot;

        // 기존 조작 및 물리 충돌 재활성화
        characterController.enabled = true;
        if (playerController != null) playerController.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        isHiding = false;
        isTransitioning = false;
    }

    private void HandleRestrictedLook()
    {
        Debug.Log("제한된 시야 함수 실행 중!");
        
        try
        {
        // 마우스 입력 받기
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        currentYaw += mouseX;
        currentPitch -= mouseY;

        // HidingSpot에 정의된 각도만큼만 고개를 돌릴 수 있도록 Clamp 처리
        currentYaw = Mathf.Clamp(currentYaw, -currentSpot.lookLimitX, currentSpot.lookLimitX);
        currentPitch = Mathf.Clamp(currentPitch, -currentSpot.lookLimitY, currentSpot.lookLimitY);

        // 숨는 장소의 정면 기준 회전값에 현재 마우스 조작 각도를 더함
        Quaternion localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        playerCamera.rotation = currentSpot.hideCameraPosition.rotation * localRotation;

        Debug.Log("에러 없이 회전 로직 통과 완료!"); // 이 로그가 안 뜨면 위 코드 중 어딘가 터진 것입니다.
        }
        catch (System.Exception e)
        {
            // 에러가 발생해도 스크립트가 정지하지 않고 원인을 알려줍니다.
            Debug.LogError("<color=red>카메라 회전 중 에러가 발생했습니다:</color> " + e.Message);
        }
    }
}
