using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct HubAbyssFallConfig
{
    public float WaitTime;
    public float FallSpeed;
    public float FallAcceleration;
    public float FallDuration;
    public float LookUpMaxDegreesPerSecond;
    public float MaxLookUpPitch;
    public float FadeStartProgress;
    public float ShaftHideFallDistance;
    public float LookUpStartFallDistance;
    public Transform ShaftVisualRoot;
    public bool HasEntryCameraState;
    public Quaternion EntryCameraLocalRotation;
    public Quaternion EntryPlayerRotation;
    public AnimationCurve FallFadeCurve;
    public Image FadeImage;
}

public static class HubAbyssFallSequence
{
    public static IEnumerator Run(GameObject player, HubAbyssFallConfig config)
    {
        if (player == null)
        {
            yield break;
        }

        Transform cameraTransform = ResolveCameraTransform(player);
        DisableCameraControl(cameraTransform);
        LockEntryCamera(player.transform, cameraTransform, config);

        LockEntryCamera(player.transform, cameraTransform, config);

        Image fade = config.FadeImage != null
            ? config.FadeImage
            : ScreenFader.Prepare(null);
        ScreenFader.SetAlpha(fade, 0f);

        if (config.WaitTime > 0f)
        {
            float waitTimer = 0f;
            while (waitTimer < config.WaitTime)
            {
                waitTimer += Time.unscaledDeltaTime;
                LockEntryCamera(player.transform, cameraTransform, config);
                yield return null;
            }
        }

        float timer = 0f;
        float duration = Mathf.Max(config.FallDuration, 0.01f);
        float fallSpeed = config.FallSpeed;
        float maxLookDegreesPerSecond = Mathf.Max(config.LookUpMaxDegreesPerSecond, 1f);
        float targetLookUpPitch = Mathf.Clamp(config.MaxLookUpPitch, -89f, 0f);
        float fallStartY = player.transform.position.y;
        bool shaftHidden = false;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float fallenDistance = fallStartY - player.transform.position.y;

            fallSpeed += config.FallAcceleration * Time.unscaledDeltaTime;
            player.transform.position += Vector3.down * fallSpeed * Time.unscaledDeltaTime;

            if (!shaftHidden && fallenDistance >= config.ShaftHideFallDistance)
            {
                HideShaftVisuals(config.ShaftVisualRoot);
                shaftHidden = true;
            }

            if (fallenDistance < config.LookUpStartFallDistance)
            {
                LockEntryCamera(player.transform, cameraTransform, config);
            }
            else
            {
                player.transform.rotation = config.EntryPlayerRotation;
                ApplyCameraPitchUp(
                    cameraTransform,
                    config.EntryCameraLocalRotation,
                    targetLookUpPitch,
                    maxLookDegreesPerSecond
                );
            }

            float fadeProgress = RemapProgress(progress, config.FadeStartProgress);
            ScreenFader.SetAlpha(fade, SampleCurve(config.FallFadeCurve, fadeProgress));

            yield return null;
        }

        ScreenFader.SetAlpha(fade, 1f);
    }

    private static void LockEntryCamera(
        Transform playerRoot,
        Transform cameraTransform,
        HubAbyssFallConfig config)
    {
        if (!config.HasEntryCameraState || playerRoot == null || cameraTransform == null)
        {
            return;
        }

        playerRoot.rotation = config.EntryPlayerRotation;
        cameraTransform.localRotation = config.EntryCameraLocalRotation;
    }

    private static void ApplyCameraPitchUp(
        Transform cameraTransform,
        Quaternion entryCameraLocalRotation,
        float targetPitch,
        float maxDegreesPerSecond)
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 entryEuler = entryCameraLocalRotation.eulerAngles;
        float entryPitch = entryEuler.x;
        if (entryPitch > 180f)
        {
            entryPitch -= 360f;
        }

        float goalPitch = Mathf.Min(entryPitch, targetPitch);
        float currentPitch = cameraTransform.localEulerAngles.x;
        if (currentPitch > 180f)
        {
            currentPitch -= 360f;
        }

        float nextPitch = Mathf.MoveTowards(
            currentPitch,
            goalPitch,
            maxDegreesPerSecond * Time.unscaledDeltaTime
        );
        cameraTransform.localRotation = Quaternion.Euler(nextPitch, 0f, 0f);
    }

    private static void DisableCameraControl(Transform cameraTransform)
    {
        if (cameraTransform == null)
        {
            return;
        }

        MouseLook mouseLook = cameraTransform.GetComponent<MouseLook>();
        if (mouseLook != null)
        {
            mouseLook.enabled = false;
        }

        MonoBehaviour[] cameraBehaviours = cameraTransform.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in cameraBehaviours)
        {
            if (behaviour == null || behaviour is MouseLook)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "RearviewCamera")
            {
                behaviour.enabled = false;
            }
        }
    }

    private static Transform ResolveCameraTransform(GameObject player)
    {
        Camera camera = player.GetComponentInChildren<Camera>(true);
        if (camera != null)
        {
            return camera.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private static float SampleCurve(AnimationCurve curve, float time)
    {
        if (curve == null || curve.length == 0)
        {
            return time;
        }

        return curve.Evaluate(time);
    }

    private static float RemapProgress(float progress, float startAt)
    {
        if (startAt >= 1f)
        {
            return 0f;
        }

        return Mathf.Clamp01((progress - startAt) / (1f - startAt));
    }

    private static void HideShaftVisuals(Transform shaftRoot)
    {
        if (shaftRoot == null)
        {
            GameObject found = GameObject.Find("Chapter1 In");
            shaftRoot = found != null ? found.transform : null;
        }

        if (shaftRoot == null)
        {
            return;
        }

        Renderer[] renderers = shaftRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }
}
