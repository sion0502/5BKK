using System.Collections;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHidingController : MonoBehaviour
{
    public Transform playerCamera;
    private PlayerController playerController;
    private MonoBehaviour mouseLook;

    public float interactionDistance = 2.5f;
    public LayerMask interactableLayer;

    public float transitionDuration = 0.5f;
    public float mouseSensitivity = 2f;

    public float maxFrontAngle = 45f;

    private CharacterController characterController;
    private HidingSpot currentSpot;

    private bool isHiding = false;
    private bool isTransitioning = false;

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

        if (!isHiding)
        {
            DetectHidingSpot();
        }

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

        if (isHiding && !isTransitioning)
        {
            HandleRestrictedLook();
        }
    }

    private void DetectHidingSpot()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            HidingSpot spot = hit.collider.GetComponent<HidingSpot>();
            if (spot != null)
            {
                float hitAngle = Vector3.Angle(hit.normal, spot.transform.forward);

                if (hitAngle <= maxFrontAngle)
                {
                    currentSpot = spot;
                    return;
                }
            }
        }

        currentSpot = null;
    }

    private IEnumerator EnterHidingRoutine()
    {
        isTransitioning = true;
        isHiding = true;

        characterController.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion startCamRot = playerCamera.rotation;

        Vector3 targetPos = currentSpot.hideCameraPosition.position - (playerCamera.position - transform.position);
        Quaternion targetRot = currentSpot.hideCameraPosition.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            float t = elapsedTime / transitionDuration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            playerCamera.rotation = Quaternion.Slerp(startCamRot, targetRot, t);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
        playerCamera.rotation = targetRot;

        currentYaw = 0f;
        currentPitch = 0f;

        isTransitioning = false;
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
        playerCamera.localRotation = Quaternion.identity;
        transform.rotation = targetRot;

        characterController.enabled = true;
        if (playerController != null) playerController.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        isHiding = false;
        isTransitioning = false;
    }

    private void HandleRestrictedLook()
    {
        try
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            currentYaw += mouseX;
            currentPitch -= mouseY;

            currentYaw = Mathf.Clamp(currentYaw, -currentSpot.lookLimitX, currentSpot.lookLimitX);
            currentPitch = Mathf.Clamp(currentPitch, -currentSpot.lookLimitY, currentSpot.lookLimitY);

            Quaternion localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            playerCamera.rotation = currentSpot.hideCameraPosition.rotation * localRotation;
        }
        catch (System.Exception)
        {
        }
    }
}