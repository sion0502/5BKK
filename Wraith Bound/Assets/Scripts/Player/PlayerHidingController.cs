using System.Collections;
using UnityEngine;

public class PlayerHidingController : MonoBehaviour
{
    // ���� �� ���� üũ
    public static bool JustEnteredHiding;

    public Transform playerCamera;

    PlayerController playerController;
    MonoBehaviour mouseLook;

    public float interactionDistance = 2.5f;
    public LayerMask interactableLayer;

    public float transitionDuration = 0.5f;
    public float mouseSensitivity = 2f;

    public float maxFrontAngle = 45f;

    CharacterController characterController;
    HidingSpot currentSpot;
    [SerializeField]AudioSource hideAudio;

    public bool isHiding = false;
    bool isTransitioning = false;

    float currentYaw = 0f;
    float currentPitch = 0f;

    void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        playerController =
            GetComponent<PlayerController>();

        mouseLook =
            playerCamera.GetComponent<MouseLook>();
    }

    void Update()
    {
        if (isTransitioning)
            return;

        if (!isHiding)
        {
            DetectHidingSpot();
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
            HandleRestrictedLook();
        }
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

        characterController.enabled =
            false;

        if (playerController != null)
            playerController.enabled = false;

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

        characterController.enabled =
            true;

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