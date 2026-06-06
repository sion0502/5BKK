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
    public float walkSpeed = 3f;
    public float walkDistance = 5f;
    public float blackHoldDuration = 0.35f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Direction")]
    public bool useNegativeForward = true;

    private bool isTransitioning;
    private BoxCollider trigger;

    private void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

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
        if (isTransitioning || !other.CompareTag("Player"))
        {
            return;
        }

        isTransitioning = true;
        StartCoroutine(ExitWalkFadeSequence(other.gameObject));
    }

    private IEnumerator ExitWalkFadeSequence(GameObject player)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
        {
            LoadHubImmediate();
            yield break;
        }

        Image fade = ScreenFader.Prepare(fadeImage);
        ScreenFader.SetAlpha(fade, 0f);

        MonoBehaviour mouseLook = player.GetComponentInChildren<Camera>(true)
            ?.GetComponent("MouseLook") as MonoBehaviour;

        SetPlayerScriptsEnabled(player, mouseLook, false);

        Vector3 walkDirection = useNegativeForward ? -transform.forward : transform.forward;
        walkDirection.y = 0f;
        walkDirection.Normalize();

        float gravity = -10f;
        float verticalVelocity = 0f;
        float walked = 0f;

        while (walked < walkDistance)
        {
            float inputZ = Input.GetAxisRaw("Vertical");
            float moveAmount = 0f;

            if (inputZ > 0.01f)
            {
                moveAmount = walkSpeed * inputZ * Time.deltaTime;
                Vector3 move = walkDirection * moveAmount;

                if (controller.isGrounded)
                {
                    verticalVelocity = -2f;
                }
                else
                {
                    verticalVelocity += gravity * Time.deltaTime;
                }

                move.y = verticalVelocity * Time.deltaTime;
                controller.Move(move);
                walked += moveAmount;
            }
            else if (controller.isGrounded)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            }

            float progress = Mathf.Clamp01(walked / walkDistance);
            float alpha = fadeCurve.Evaluate(progress);
            ScreenFader.SetAlpha(fade, alpha);

            if (progress >= 1f)
            {
                break;
            }

            yield return null;
        }

        ScreenFader.SetAlpha(fade, 1f);
        yield return new WaitForSeconds(blackHoldDuration);

        ScreenFader.ShouldWakeUpInHub = true;
        ScreenFader.PersistBlack(fade);
        SceneManager.LoadScene(targetSceneName);
    }

    private void LoadHubImmediate()
    {
        ScreenFader.ShouldWakeUpInHub = true;
        ScreenFader.PersistBlack(fadeImage);
        SceneManager.LoadScene(targetSceneName);
    }

    private static void SetPlayerScriptsEnabled(GameObject player, MonoBehaviour mouseLook, bool enabled)
    {
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
            {
                script.enabled = enabled;
            }
        }

        if (mouseLook != null)
        {
            mouseLook.enabled = enabled;
        }
    }
}
