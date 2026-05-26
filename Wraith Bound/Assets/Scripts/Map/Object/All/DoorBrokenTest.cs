using UnityEngine;
using System.Collections;

public class DoorBrokenTest : MonoBehaviour
{
    [SerializeField] private int hitsToBreak = 5;
    [SerializeField] private float breakForce = 1500f;
    [SerializeField] private float upwardForce = 3f;

    private int currentHits = 0;
    private bool isBroken = false;

    private Rigidbody rb;
    private DoorClick doorScript;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        doorScript = GetComponent<DoorClick>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            HitDoor();
        }
    }

    private void HitDoor()
    {
        if (isBroken) return;

        currentHits++;

        Debug.Log($"타격 횟수 : {currentHits}");

        if (currentHits >= hitsToBreak)
        {
            BreakDoor();
        }
    }

    private void BreakDoor()
    {
        isBroken = true;

        if (doorScript != null)
        {
            doorScript.enabled = false;
        }

        BoxCollider col = GetComponent<BoxCollider>();

        if (col != null)
        {
            col.size = new Vector3(1f, 1f, 0.3f);
        }

        // 부모 분리
        transform.SetParent(null);

        // 물리 활성화
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            Vector3 dir =
                (transform.position - player.transform.position).normalized;

            rb.AddForce(
                (dir + Vector3.up * 0.05f) * 100f,
                ForceMode.Impulse
            );

            rb.AddTorque(
                Random.insideUnitSphere * 50f,
                ForceMode.Impulse
            );
        }

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(5f);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        // Transparent 설정
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                mat.SetFloat("_Surface", 1);

                mat.SetInt(
                    "_SrcBlend",
                    (int)UnityEngine.Rendering.BlendMode.SrcAlpha
                );

                mat.SetInt(
                    "_DstBlend",
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                );

                mat.SetInt("_ZWrite", 0);

                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                mat.renderQueue = 3000;
            }
        }

        float duration = 2f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, time / duration);

            foreach (Renderer r in renderers)
            {
                foreach (Material mat in r.materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        Color color = mat.color;
                        color.a = alpha;
                        mat.color = color;
                    }
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}