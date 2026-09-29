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

        // Release navigation immediately, even on doors without DoorNavMesh.
        DoorNavMeshUtility.SetNavMeshBlocked(transform, false);
        DoorNavMesh navigation = GetComponent<DoorNavMesh>();
        if (navigation != null) navigation.enabled = false;
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        transform.SetParent(null);

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;

        Vector3 dir = transform.position - attackerPosition;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            dir = transform.forward;

        dir.Normalize();

        StartCoroutine(AnimateDebris(dir));
        StartCoroutine(FadeAndDestroy());
    }

    IEnumerator AnimateDebris(Vector3 direction)
    {
        Vector3 start = transform.position;
        Vector3 end = start + direction * Mathf.Clamp(breakImpulse * 0.015f, 0.5f, 2f);
        if (Physics.Raycast(end + Vector3.up * 1.5f, Vector3.down, out RaycastHit ground,
            5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            end.y = ground.point.y + 0.05f;
        Quaternion initial = transform.rotation;
        Quaternion landed = Quaternion.Euler(90f, transform.eulerAngles.y + torqueImpulse, 0f);
        float elapsed = 0f;
        const float duration = 0.5f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.SetPositionAndRotation(
                Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f),
                Quaternion.Slerp(initial, landed, t));
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.SetPositionAndRotation(end, landed);
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
