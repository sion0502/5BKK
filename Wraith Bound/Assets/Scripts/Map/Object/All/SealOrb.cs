using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SealOrb : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private LineRenderer energyLine;
    private ParticleSystem breakParticle;
    private Renderer[] renderers;

    private bool isBroken = false;
    private bool hasLanded = false;

    [Header("Roll And Fade")]
    [SerializeField] private float rollDurationBeforeFade = 2.5f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float rollImpulse = 1.8f;
    [SerializeField] private float rollTorque = 4.5f;

    [Header("Landing Sound")]
    [SerializeField] private AudioClip landingSound;
    [SerializeField] private float landingSoundVolume = 1f;
    [SerializeField] private Vector2 landingPitchRange = new Vector2(0.9f, 1.1f);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // 자식들 자동 검색
        energyLine = GetComponentInChildren<LineRenderer>();
        breakParticle = GetComponentInChildren<ParticleSystem>();
        renderers = GetComponentsInChildren<Renderer>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void BreakSeal()
    {
        if (isBroken) return;

        isBroken = true;

        // 에너지 줄기 제거
        if (energyLine != null)
            energyLine.enabled = false;

        // 낙하 시작
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddTorque(
                Random.insideUnitSphere * 5f,
                ForceMode.Impulse
            );
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isBroken) return;

        if (collision.gameObject.layer == 10)
        {
            StartRollAndFade(collision);
        }
    }

    private void StartRollAndFade(Collision collision)
    {
        if (hasLanded)
        {
            return;
        }

        hasLanded = true;
        PlayLandingSound(collision);
        PlayLandingParticle();

        if (rb != null)
        {
            Vector3 rollDirection = Vector3.ProjectOnPlane(
                Random.insideUnitSphere,
                Vector3.up
            ).normalized;

            if (rollDirection.sqrMagnitude < 0.01f && collision.contactCount > 0)
            {
                rollDirection = Vector3.ProjectOnPlane(
                    collision.GetContact(0).normal,
                    Vector3.up
                ).normalized;
            }

            rb.AddForce(rollDirection * rollImpulse, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * rollTorque, ForceMode.Impulse);
        }

        StartCoroutine(FadeOutAfterRoll());
    }

    private void PlayLandingSound(Collision collision)
    {
        if (landingSound == null)
        {
            return;
        }

        Vector3 position = transform.position;
        if (collision.contactCount > 0)
        {
            position = collision.GetContact(0).point;
        }

        GameObject soundObject = new GameObject("SealOrbLandingSound");
        soundObject.transform.position = position;

        AudioSource audioSource = soundObject.AddComponent<AudioSource>();
        audioSource.clip = landingSound;
        audioSource.volume = landingSoundVolume;
        audioSource.pitch = Random.Range(landingPitchRange.x, landingPitchRange.y);
        audioSource.spatialBlend = 1f;
        audioSource.Play();

        Destroy(soundObject, landingSound.length / Mathf.Abs(audioSource.pitch) + 0.1f);
    }

    private void PlayLandingParticle()
    {
        // 파티클 분리 후 재생
        if (breakParticle != null)
        {
            breakParticle.transform.SetParent(null);

            breakParticle.Play();

            Destroy(
                breakParticle.gameObject,
                breakParticle.main.duration + 1f
            );
        }
    }

    private IEnumerator FadeOutAfterRoll()
    {
        yield return new WaitForSeconds(rollDurationBeforeFade);

        List<Material> fadeMaterials = new List<Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null || r == energyLine)
            {
                continue;
            }

            foreach (Material material in r.materials)
            {
                if (material == null)
                {
                    continue;
                }

                PrepareMaterialForFade(material);
                fadeMaterials.Add(material);
            }
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            foreach (Material material in fadeMaterials)
            {
                SetMaterialAlpha(material, alpha);
            }

            yield return null;
        }

        if (col != null)
        {
            col.enabled = false;
        }

        Destroy(gameObject);
    }

    private void PrepareMaterialForFade(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void SetMaterialAlpha(Material material, float alpha)
    {
        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }
}