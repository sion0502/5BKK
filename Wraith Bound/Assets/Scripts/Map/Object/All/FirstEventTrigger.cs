using UnityEngine;

public class FirstEventTrigger : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    [Header("Lamp Drop")]
    [SerializeField] private GameObject lampRoot;
    [SerializeField] private string lampName = "HH_Lamp_02";
    [SerializeField] private float dropForwardImpulse = 0.25f;
    [SerializeField] private float dropDownImpulse = 0.1f;
    [SerializeField] private float dropTorque = 1.5f;
    [SerializeField] private float lampMass = 1.2f;
    [SerializeField] private string powerOffChildName = "PowerOff";

    [Header("Sound")]
    [SerializeField] private AudioClip scareSound;
    [SerializeField] private float scareVolume = 1f;
    [SerializeField] private AudioClip crashSound;
    [SerializeField] private float crashVolume = 1f;
    [SerializeField] private float crashFallbackDelay = 0.75f;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !IsPlayer(other))
        {
            return;
        }

        hasTriggered = true;
        Play2DSound(scareSound, scareVolume);
        DropLamp();
        gameObject.SetActive(false);
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        Transform root = other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }

    private void DropLamp()
    {
        GameObject lamp = ResolveLamp();
        if (lamp == null)
        {
            return;
        }

        DisableLampEffects(lamp);
        SetPowerOffActive(lamp, true);

        Rigidbody rb = lamp.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = lamp.AddComponent<Rigidbody>();
        }

        rb.mass = lampMass;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.WakeUp();

        LampImpactSound impactSound = lamp.GetComponent<LampImpactSound>();
        if (impactSound == null)
        {
            impactSound = lamp.AddComponent<LampImpactSound>();
        }

        impactSound.Initialize(crashSound, crashVolume, crashFallbackDelay);

        Vector3 impulse =
            (transform.forward * dropForwardImpulse) + (Vector3.down * dropDownImpulse);
        rb.AddForce(impulse, ForceMode.Impulse);
        rb.AddTorque(Random.onUnitSphere * dropTorque, ForceMode.Impulse);
    }

    private GameObject ResolveLamp()
    {
        if (lampRoot != null)
        {
            return lampRoot;
        }

        Transform searchRoot = transform.parent != null ? transform.parent : transform.root;
        Transform found = FindChildByName(searchRoot, lampName);
        return found != null ? found.gameObject : null;
    }

    private void DisableLampEffects(GameObject lamp)
    {
        foreach (FlickeringLamp flickeringLamp in lamp.GetComponentsInChildren<FlickeringLamp>(true))
        {
            flickeringLamp.enabled = false;
        }

        foreach (ParticleSystem particles in lamp.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        foreach (Light light in lamp.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
        }
    }

    private void SetPowerOffActive(GameObject lamp, bool active)
    {
        Transform powerOff = FindChildByName(lamp.transform, powerOffChildName);
        if (powerOff != null)
        {
            powerOff.gameObject.SetActive(active);
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void Play2DSound(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject soundObject = new GameObject("FirstEvent_ScareSound_Runtime");
        AudioSource audioSource = soundObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;
        audioSource.Play();
        Destroy(soundObject, clip.length + 0.1f);
    }
}

internal class LampImpactSound : MonoBehaviour
{
    private AudioClip clip;
    private float volume;
    private bool hasPlayed;

    public void Initialize(AudioClip crashClip, float crashVolume, float fallbackDelay)
    {
        clip = crashClip;
        volume = crashVolume;

        if (clip != null && fallbackDelay > 0f)
        {
            Invoke(nameof(PlayAtCurrentPosition), fallbackDelay);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasPlayed || collision.relativeVelocity.sqrMagnitude < 0.25f)
        {
            return;
        }

        Vector3 position = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        Play(position);
    }

    private void PlayAtCurrentPosition()
    {
        Play(transform.position);
    }

    private void Play(Vector3 position)
    {
        if (hasPlayed || clip == null)
        {
            return;
        }

        hasPlayed = true;
        CancelInvoke(nameof(PlayAtCurrentPosition));
        AudioSource.PlayClipAtPoint(clip, position, volume);
        Destroy(this);
    }
}
