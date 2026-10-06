using System.Collections;
using UnityEngine;

public class PlayerHidingController : MonoBehaviour
{
    // ���� �� ���� üũ
    public static bool JustEnteredHiding;

    public Transform playerCamera;

    PlayerController playerController;
    MonoBehaviour mouseLook;
    PlayerAudioMixerController playerAudioMixerController;

    public float interactionDistance = 2.5f;
    public LayerMask interactableLayer;

    public float transitionDuration = 0.5f;
    public float mouseSensitivity = 2f;

    public float maxFrontAngle = 45f;

    [Header("Hold Breath")]
    // 숨은 상태에서 이 키를 누르고 있는 동안만 괴물에게 숨은 것으로 인정됩니다.
    [SerializeField] private KeyCode holdBreathKey = KeyCode.LeftShift;
    // 숨 참기 게이지의 최대값입니다. HUD는 이 값을 기준으로 비율을 표시합니다.
    [SerializeField] private float maxBreath = 100f;
    // 숨 참기 중 초당 감소량입니다.
    [SerializeField] private float breathDrainRate = 18f;
    // 숨는 장소 밖에 있을 때 초당 회복량입니다.
    [SerializeField] private float breathRecoverRate = 40f;
    // false로 두면 숨 참기 게이지가 자동 회복되지 않습니다.
    [SerializeField] private bool recoverBreathOutsideHiding = true;

    CharacterController characterController;
    HidingSpot currentSpot;
    RearviewCamera rearviewCamera;
    [SerializeField]AudioSource hideAudio;

    public bool isHiding = false;
    bool isTransitioning = false;
    bool isHoldingBreath = false;

    float currentYaw = 0f;
    float currentPitch = 0f;
    float currentBreath;

    // EnemyBase가 숨 참기 상태를 확인할 때 사용하는 읽기 전용 상태값입니다.
    public bool IsHoldingBreath => isHiding && isHoldingBreath && currentBreath > 0f;
    // 숨 참기 시스템 비활성화: 은신 여부만 사용합니다. 목격 판정은 EnemySense가 유지합니다.
    // public bool IsHiddenFromEnemies => isHiding && IsHoldingBreath;
    public bool IsHiddenFromEnemies => isHiding;
    public bool IsBreathDepleted => currentBreath <= 0f;
    public bool IsTransitioning => isTransitioning;
    public float CurrentBreath => currentBreath;
    public float MaxBreath => maxBreath;
    // HUD에서 숨 게이지를 0~1 비율로 표시하기 위한 값입니다.
    public float BreathRatio => maxBreath > 0f ? Mathf.Clamp01(currentBreath / maxBreath) : 0f;

    void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        playerController =
            GetComponent<PlayerController>();

        playerAudioMixerController = GetComponent<PlayerAudioMixerController>();

        mouseLook =
            playerCamera.GetComponent<MouseLook>();

        rearviewCamera = playerCamera.GetComponent<RearviewCamera>();

        currentBreath =
            Mathf.Max(0f, maxBreath);
    }

    void Update()
    {
        if (isTransitioning)
            return;

        if (!isHiding)
        {
            DetectHidingSpot();
            // RecoverBreathOutsideHiding(); // 숨 참기 시스템 비활성화
        }

        if (Input.GetButtonDown("Interact"))
        {
            if (isHiding)
            {
                StartCoroutine(
                    ExitHidingRoutine());
            }
            else if (currentSpot != null)
            {
                playerController.isCrouching =
                    false;

                playerController.isRun =
                    false;

                StartCoroutine(
                    EnterHidingRoutine());
            }
        }

        if (isHiding &&
            !isTransitioning)
        {
            // HandleHoldBreath(); // 숨 참기 시스템 비활성화
            HandleRestrictedLook();
        }
    }

    void HandleHoldBreath()
    {
        // 숨은 상태에서 키를 누르고 게이지가 남아 있어야 숨 참기 상태가 유지됩니다.
        bool wantsHoldBreath =
            Input.GetKey(holdBreathKey);

        if (!wantsHoldBreath ||
            currentBreath <= 0f)
        {
            isHoldingBreath = false;
            return;
        }

        isHoldingBreath = true;

        currentBreath -=
            breathDrainRate *
            Time.deltaTime;

        // 게이지를 모두 쓰면 숨 참기가 강제로 풀립니다.
        if (currentBreath <= 0f)
        {
            currentBreath = 0f;
            isHoldingBreath = false;
        }
    }

    void RecoverBreathOutsideHiding()
    {
        // 숨는 장소 밖에서만 회복되도록 하여, 숨어있는 동안은 게이지 관리가 필요하게 합니다.
        if (!recoverBreathOutsideHiding ||
            currentBreath >= maxBreath)
            return;

        currentBreath =
            Mathf.Min(
                currentBreath +
                breathRecoverRate *
                Time.deltaTime,
                maxBreath);
    }

    void DetectHidingSpot()
    {
        Ray ray =
            new Ray(
                playerCamera.position,
                playerCamera.forward);

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance,
            interactableLayer,
            QueryTriggerInteraction.Ignore))
        {
            HidingSpot spot =
                hit.collider.GetComponent<HidingSpot>();

            if (spot != null)
            {
                float hitAngle =
                    Vector3.Angle(
                        hit.normal,
                        spot.transform.forward);

                if (hitAngle <= maxFrontAngle)
                {
                    currentSpot = spot;
                    return;
                }
            }
        }

        currentSpot = null;
    }

    IEnumerator EnterHidingRoutine()
    {
        isTransitioning = true;

        // ���� �� ����
        JustEnteredHiding = true;

        isHiding = true;
        isHoldingBreath = false;

        if (playerController != null)
            playerController.enabled = false;

        characterController.enabled = false;

        if (playerAudioMixerController != null)
            playerAudioMixerController.enabled = false;

        if (rearviewCamera != null)
            rearviewCamera.enabled = false;

        if (mouseLook != null)
            mouseLook.enabled = false;

        Vector3 startPos =
            transform.position;

        Quaternion startRot =
            transform.rotation;

        Quaternion startCamRot =
            playerCamera.rotation;

        Vector3 targetPos =
            currentSpot.hideCameraPosition.position -
            (playerCamera.position - transform.position);

        Quaternion targetRot =
            currentSpot.hideCameraPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t =
                elapsedTime /
                transitionDuration;

            t =
                t * t * (3f - 2f * t);

            transform.position =
                Vector3.Lerp(
                    startPos,
                    targetPos,
                    t);

            transform.rotation =
                Quaternion.Slerp(
                    startRot,
                    targetRot,
                    t);

            playerCamera.rotation =
                Quaternion.Slerp(
                    startCamRot,
                    targetRot,
                    t);

            elapsedTime +=
                Time.deltaTime;

            yield return null;
        }

        transform.position =
            targetPos;

        transform.rotation =
            targetRot;

        playerCamera.rotation =
            targetRot;

        currentYaw = 0f;
        currentPitch = 0f;

        isTransitioning = false;
    }

    IEnumerator ExitHidingRoutine()
    {
        isTransitioning = true;

        Vector3 startPos =
            transform.position;

        Quaternion startCamRot =
            playerCamera.rotation;

        Vector3 targetPos =
            currentSpot.exitPosition.position;

        Quaternion targetRot =
            currentSpot.exitPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t =
                elapsedTime /
                transitionDuration;

            t =
                t * t * (3f - 2f * t);

            transform.position =
                Vector3.Lerp(
                    startPos,
                    targetPos,
                    t);

            playerCamera.rotation =
                Quaternion.Slerp(
                    startCamRot,
                    targetRot,
                    t);

            elapsedTime +=
                Time.deltaTime;

            yield return null;
        }

        transform.position =
            targetPos;

        playerCamera.localRotation =
            Quaternion.identity;

        transform.rotation =
            targetRot;

        if (playerAudioMixerController != null)
            playerAudioMixerController.enabled = true;

        
        characterController.enabled = true;

        if (rearviewCamera != null)
            rearviewCamera.enabled = true;

        if (playerController != null)
            playerController.enabled = true;

        if (mouseLook != null)
            mouseLook.enabled = true;

        isHiding = false;
        isTransitioning = false;
    }

    void HandleRestrictedLook()
    {
        float mouseX =
            Input.GetAxisRaw("Mouse X") *
            mouseSensitivity;

        float mouseY =
            Input.GetAxisRaw("Mouse Y") *
            mouseSensitivity;

        currentYaw += mouseX;
        currentPitch -= mouseY;

        currentYaw =
            Mathf.Clamp(
                currentYaw,
                -currentSpot.lookLimitX,
                currentSpot.lookLimitX);

        currentPitch =
            Mathf.Clamp(
                currentPitch,
                -currentSpot.lookLimitY,
                currentSpot.lookLimitY);

        Quaternion localRotation =
            Quaternion.Euler(
                currentPitch,
                currentYaw,
                0f);

        playerCamera.rotation =
            currentSpot.hideCameraPosition.rotation *
            localRotation;
    }
}
