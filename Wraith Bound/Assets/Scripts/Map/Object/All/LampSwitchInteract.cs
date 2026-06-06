using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LampSwitchInteract : MonoBehaviour, IInteractable
{
    [Header("Lamp Targets")]
    [SerializeField] private GameObject[] lamps;
    [SerializeField] private Light[] targetLights;
    [SerializeField] private Transform autoSearchRoot;
    [SerializeField] private string lampNameContains = "[Lamp] Lamp";

    [Header("Switch")]
    [SerializeField] private Transform button;
    [SerializeField] private float pressedButtonYAngle = -10f;

    [Header("Light Pulse")]
    [SerializeField] private float offDuration = 0.25f;
    [SerializeField] private bool restoreOriginalLightState = true;

    [Header("Sound")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private float switchSoundVolume = 1f;
    [SerializeField] private Transform soundOrigin;

    [Header("Fluorescent Startup")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip finalClickSound;
    [SerializeField] private float lampSoundVolume = 1f;
    [SerializeField] private float[] tickDelays = { 0f, 0.35f, 0.12f };
    [SerializeField] private float finalClickDelay = 0.22f;
    [SerializeField] private float tickLightOnDuration = 0.06f;
    [SerializeField] private float tickLightOffDuration = 0.04f;

    [Header("Prompt")]
    [SerializeField] private string interactPrompt = "[E] Switch";
    [SerializeField] private string usedPrompt = "";

    private bool hasActivated;

    private void Awake()
    {
        if (button == null)
        {
            button = FindChildByName(transform, "Button");
        }
    }

    public void Interact(GameObject interactor)
    {
        if (hasActivated)
        {
            return;
        }

        hasActivated = true;
        PlaySwitchSound();
        PressButton();
        StartCoroutine(StartupLights());
    }

    public string GetInteractPrompt()
    {
        return hasActivated ? usedPrompt : interactPrompt;
    }

    private IEnumerator StartupLights()
    {
        Light[] lights = ResolveLights();
        if (lights.Length == 0)
        {
            yield break;
        }

        bool[] originalEnabled = new bool[lights.Length];
        float[] originalIntensity = new float[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            originalEnabled[i] = light.enabled;
            originalIntensity[i] = light.intensity;
            light.enabled = false;
        }

        yield return new WaitForSeconds(offDuration);

        if (tickDelays != null && tickDelays.Length > 0)
        {
            foreach (float delay in tickDelays)
            {
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                PlayLampSound(tickSound, lights);
                SetLightsEnabled(lights, true, originalIntensity);
                yield return new WaitForSeconds(tickLightOnDuration);

                SetLightsEnabled(lights, false, originalIntensity);
                yield return new WaitForSeconds(tickLightOffDuration);
            }
        }

        if (finalClickDelay > 0f)
        {
            yield return new WaitForSeconds(finalClickDelay);
        }

        PlayLampSound(finalClickSound, lights);
        SetLightsEnabled(lights, !restoreOriginalLightState, originalIntensity);

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            if (restoreOriginalLightState)
            {
                light.enabled = originalEnabled[i];
            }

            light.intensity = originalIntensity[i];
        }
    }

    private Light[] ResolveLights()
    {
        List<Light> lights = new List<Light>();
        AddLights(targetLights, lights);

        if (lamps != null)
        {
            foreach (GameObject lamp in lamps)
            {
                if (lamp == null)
                {
                    continue;
                }

                AddLights(lamp.GetComponentsInChildren<Light>(true), lights);
            }
        }

        if (lights.Count == 0)
        {
            Transform root = autoSearchRoot != null ? autoSearchRoot : transform.root;
            AddAutoFoundLampLights(root, lights);
        }

        return lights.ToArray();
    }

    private void AddAutoFoundLampLights(Transform root, List<Light> lights)
    {
        if (root == null)
        {
            return;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.Contains(lampNameContains))
            {
                continue;
            }

            AddLights(child.GetComponentsInChildren<Light>(true), lights);
        }
    }

    private void AddLights(IReadOnlyList<Light> source, List<Light> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Light light = source[i];
            if (light != null && !destination.Contains(light))
            {
                destination.Add(light);
            }
        }
    }

    private void PressButton()
    {
        if (button == null)
        {
            return;
        }

        Vector3 euler = button.localEulerAngles;
        euler.y = pressedButtonYAngle;
        button.localEulerAngles = euler;
    }

    private void PlaySwitchSound()
    {
        if (switchSound == null)
        {
            return;
        }

        Vector3 position = soundOrigin != null
            ? soundOrigin.position
            : transform.position;

        AudioSource.PlayClipAtPoint(switchSound, position, switchSoundVolume);
    }

    private void SetLightsEnabled(Light[] lights, bool enabled, float[] intensities)
    {
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            light.enabled = enabled;
            if (intensities != null && i < intensities.Length)
            {
                light.intensity = intensities[i];
            }
        }
    }

    private void PlayLampSound(AudioClip clip, Light[] lights)
    {
        if (clip == null)
        {
            return;
        }

        if (lights == null || lights.Length == 0)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, lampSoundVolume);
            return;
        }

        foreach (Light light in lights)
        {
            if (light != null)
            {
                AudioSource.PlayClipAtPoint(
                    clip,
                    light.transform.position,
                    lampSoundVolume
                );
            }
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
