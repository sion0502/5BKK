using UnityEngine;

public class SealOrb : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private LineRenderer energyLine;
    private ParticleSystem breakParticle;
    private Renderer[] renderers;

    private bool isBroken = false;

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
            Shatter();
        }
    }

    private void Shatter()
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

        // 메시 숨김
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        if (col != null)
            col.enabled = false;

        Destroy(gameObject, 0.1f);
    }
}