using UnityEngine;
using System.Collections;

public class DoorBrokenTest : MonoBehaviour
{
    [SerializeField] private int hitsToBreak = 5;
    [SerializeField] private float breakImpulse = 100f;
    [SerializeField] private float torqueImpulse = 50f;
    [SerializeField] private float fadeStartDelay = 5f;
    [SerializeField] private float fadeDuration = 2f;

    private int currentHits;
    private bool isBroken;

    private Rigidbody rb;
    private DoorClick doorScript;

    public bool IsBroken()
    {
        return isBroken;
    }

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
        if (Input.GetKeyDown(KeyCode.Y))
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 attackerPosition = player != null ? player.transform.position : transform.position - transform.forward;
            HitDoor(attackerPosition);
        }
    }

    public void HitDoor(Vector3 attackerPosition)
    {
        if (isBroken) return;

        currentHits++;

        Debug.Log($"Door Hit Count : {currentHits} / {hitsToBreak}");

        if (currentHits >= hitsToBreak)
            BreakDoor(attackerPosition);
    }

    public void BreakByEnemy(Vector3 attackerPosition)
    {
        if (isBroken) return;

        Debug.Log("Enemy Force Break Door");

        currentHits = hitsToBreak;
        BreakDoor(attackerPosition);
    }

    private void BreakDoor(Vector3 attackerPosition)
    {
        if (isBroken) return;

        isBroken = true;

        if (doorScript != null)
            doorScript.enabled = false;

        BoxCollider col = GetComponent<BoxCollider>();

        if (col != null)
            col.size = new Vector3(1f, 1f, 0.3f);

        transform.SetParent(null);

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 dir = transform.position - attackerPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            dir = transform.forward;

        dir.Normalize();

        rb.AddForce((dir + Vector3.up * 0.05f) * breakImpulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueImpulse, ForceMode.Impulse);

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        yield return new WaitForSeconds(fadeStartDelay);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
        }

        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, time / fadeDuration);

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
