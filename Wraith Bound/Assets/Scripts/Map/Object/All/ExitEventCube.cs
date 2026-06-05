using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class ExitEventCube : MonoBehaviour
{
    [Header("Scene")]
    public string targetSceneName = "Hub";

    [Header("Fade")]
    public Image fadeImage;
    public float pauseBeforeFade = 0.35f;
    public float fadeDuration = 2.8f;
    public float blackHoldDuration = 0.55f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera 연출")]
    public float cameraSink = 0.18f;
    public float cameraTilt = 10f;
    public float cameraRoll = 2.5f;
    public float fovShrink = 10f;

    private bool isTransitioning;

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTransitioning || !other.CompareTag("Player")) return;

        isTransitioning = true;
        StartCoroutine(ExitSequence(other.gameObject));
    }

    private IEnumerator ExitSequence(GameObject player)
    {
        Transform cameraTransform = ResolveCameraTransform(player);
        Camera camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        MonoBehaviour mouseLook = cameraTransform != null
            ? cameraTransform.GetComponent("MouseLook") as MonoBehaviour
            : null;

        DisablePlayerControl(player, mouseLook);

        Vector3 cameraStartLocalPos = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;
        Quaternion cameraStartLocalRot = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;
        float cameraStartFov = camera != null ? camera.fieldOfView : 60f;

        yield return new WaitForSeconds(pauseBeforeFade);

        Image fade = ScreenFader.Prepare(fadeImage);
        if (fade == null)
        {
            SceneManager.LoadScene(targetSceneName);
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeDuration);
            float eased = fadeCurve.Evaluate(t);

            Color color = new Color(0f, 0f, 0f, eased);
            fade.color = color;

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = Vector3.Lerp(
                    cameraStartLocalPos,
                    cameraStartLocalPos + Vector3.down * cameraSink,
                    eased
                );

                Quaternion targetRot = cameraStartLocalRot * Quaternion.Euler(cameraTilt, 0f, cameraRoll);
                cameraTransform.localRotation = Quaternion.Slerp(cameraStartLocalRot, targetRot, eased);
            }

            if (camera != null)
            {
                camera.fieldOfView = Mathf.Lerp(cameraStartFov, cameraStartFov - fovShrink, eased);
            }

            yield return null;
        }

        fade.color = new Color(0f, 0f, 0f, 1f);
        yield return new WaitForSeconds(blackHoldDuration);

        SceneManager.LoadScene(targetSceneName);
    }

    private static Transform ResolveCameraTransform(GameObject player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        Camera childCamera = player.GetComponentInChildren<Camera>(true);
        return childCamera != null ? childCamera.transform : null;
    }

    private static void DisablePlayerControl(GameObject player, MonoBehaviour mouseLook)
    {
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
            {
                script.enabled = false;
            }
        }

        if (mouseLook != null)
        {
            mouseLook.enabled = false;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }
    }
}
