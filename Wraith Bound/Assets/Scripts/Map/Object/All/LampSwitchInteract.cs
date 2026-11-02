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
    [SerializeField] private string powerOffCoverName = "PowerOff";

    [Header("Switch")]
    [SerializeField] private Transform button;
    [SerializeField] private float pressedButtonYAngle = -10f;

    [Header("Light Pulse")]
    [SerializeField] private float offDuration = 0.25f;

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


    private bool isSwitching;
    private Light[] controlledLights;
    private float[] onIntensities;
    private GameObject[] controlledCovers;
    private Quaternion originalButtonRotation;
    private Coroutine startupRoutine;

    private void Awake()
    {
        // The imported prefab used a 1.1 x 1.1 x 0.5 box, extending through walls.
        MeshFilter mesh = GetComponent<MeshFilter>();
        BoxCollider box = GetComponent<BoxCollider>();
        if (mesh != null && mesh.sharedMesh != null && box != null)
        {
            box.center = mesh.sharedMesh.bounds.center;
            box.size = mesh.sharedMesh.bounds.size;
        }

        if (button == null)
        {
            button = FindChildByName(transform, "Button");
        }
        if (button != null) originalButtonRotation = button.localRotation;
    }

    public void Interact(GameObject interactor)
    {
        if (isSwitching)
        {
            return;
        }

        Camera camera = interactor != null ? interactor.GetComponentInChildren<Camera>() : Camera.main;
        MeshFilter mesh = GetComponent<MeshFilter>();
        Vector3 point = mesh != null && mesh.sharedMesh != null
            ? transform.TransformPoint(mesh.sharedMesh.bounds.center) : transform.position;
        if (camera == null || !PlayerInteractor.HasClearPath(camera.transform.position, point,
                transform, interactor != null ? interactor.transform : null)) return;

        EnsureLightCache();
        if (controlledLights.Length == 0) return;

        bool lightsAreOn = false;
        foreach (Light light in controlledLights)
        {
            if (light != null && light.enabled && light.intensity > 0f)
            {
                lightsAreOn = true;
                break;
            }
        }

        PlaySwitchSound();
        PressButton(!lightsAreOn);
        if (lightsAreOn)
        {
            SetLightsEnabled(controlledLights, false, null);
            SetPowerOffCoversActive(controlledCovers, true);
        }
        else
        {
            isSwitching = true;
            startupRoutine = StartCoroutine(StartupLights());
        }
    }

    public string GetInteractPrompt()
    {
        return isSwitching ? "" : (string.IsNullOrEmpty(interactPrompt) ? "[E] Switch" : interactPrompt);
    }

    private void EnsureLightCache()
    {
        if (controlledLights != null) return;
        controlledLights = ResolveLights();
        controlledCovers = ResolvePowerOffCovers();
        onIntensities = new float[controlledLights.Length];
        for (int i = 0; i < controlledLights.Length; i++)
        {
            Light light = controlledLights[i];
            onIntensities[i] = light != null && light.intensity > 0f ? light.intensity : 1f;
        }
    }

    private IEnumerator StartupLights()
    {
        SetLightsEnabled(controlledLights, false, null);
        SetPowerOffCoversActive(controlledCovers, true);
        yield return new WaitForSeconds(offDuration);

        if (tickDelays != null)
        {
            foreach (float delay in tickDelays)
            {
                if (delay > 0f) yield return new WaitForSeconds(delay);
                PlayLampSound(tickSound, controlledLights);
                SetLightsEnabled(controlledLights, true, onIntensities);
                SetPowerOffCoversActive(controlledCovers, false);
                yield return new WaitForSeconds(tickLightOnDuration);
                SetLightsEnabled(controlledLights, false, null);
                SetPowerOffCoversActive(controlledCovers, true);
                yield return new WaitForSeconds(tickLightOffDuration);
            }
        }

        if (finalClickDelay > 0f) yield return new WaitForSeconds(finalClickDelay);
        PlayLampSound(finalClickSound, controlledLights);
        SetLightsEnabled(controlledLights, true, onIntensities);
        SetPowerOffCoversActive(controlledCovers, false);
        startupRoutine = null;
        isSwitching = false;
    }

    private void OnDisable()
    {
        if (!isSwitching) return;
        if (startupRoutine != null) StopCoroutine(startupRoutine);
        SetLightsEnabled(controlledLights, false, null);
        SetPowerOffCoversActive(controlledCovers, true);
        PressButton(false);
        startupRoutine = null;
        isSwitching = false;
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

    private GameObject[] ResolvePowerOffCovers()
    {
        List<GameObject> covers = new List<GameObject>();

        if (lamps != null)
        {
            foreach (GameObject lamp in lamps)
            {
                AddPowerOffCover(lamp != null ? lamp.transform : null, covers);
            }
        }

        if (covers.Count == 0)
        {
            Transform root = autoSearchRoot != null ? autoSearchRoot : transform.root;
            AddAutoFoundPowerOffCovers(root, covers);
        }

        return covers.ToArray();
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

    private void AddAutoFoundPowerOffCovers(Transform root, List<GameObject> covers)
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

            AddPowerOffCover(child, covers);
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

    private void AddPowerOffCover(Transform lampRoot, List<GameObject> covers)
    {
        if (lampRoot == null)
        {
            return;
        }

        Transform cover = FindChildByName(lampRoot, powerOffCoverName);
        if (cover != null && !covers.Contains(cover.gameObject))
        {
            covers.Add(cover.gameObject);
        }
    }

    private void PressButton(bool on)
    {
        if (button == null)
        {
            return;
        }

        button.localRotation = on
            ? originalButtonRotation * Quaternion.Euler(0f, pressedButtonYAngle, 0f)
            : originalButtonRotation;
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

    private void SetPowerOffCoversActive(GameObject[] covers, bool active)
    {
        if (covers == null)
        {
            return;
        }

        foreach (GameObject cover in covers)
        {
            if (cover != null)
            {
                cover.SetActive(active);
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
