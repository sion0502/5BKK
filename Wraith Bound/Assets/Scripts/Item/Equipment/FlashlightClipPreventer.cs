using UnityEngine;

/// <summary>
/// 손전등 SpotLight가 벽/바닥을 관통하지 않도록 local forward 오프셋만 조정하고,
/// 표면이 가까울 때 밝기를 줄여 근접 과노출(흰 원)을 완화한다.
/// </summary>
public class FlashlightClipPreventer : MonoBehaviour
{
    [Header("Clip (local forward)")]
    [SerializeField] private float minLocalForward = 0.03f;
    [SerializeField] private float surfaceOffset = 0.12f;
    [SerializeField] private float sphereCastRadius = 0.04f;
    [SerializeField] private float probeExtra = 0.25f;
    [SerializeField] private float positionSmooth = 18f;

    [Header("Proximity intensity fade")]
    [SerializeField] private float fadeStartDistance = 0.35f;
    [SerializeField] private float fadeEndDistance = 0.1f;
    [SerializeField] private float intensitySmooth = 12f;

    private Transform lightPivot;
    private Light spotLight;
    private Vector3 baseLocalPos;
    private float defaultLocalForward;
    private float baseIntensity;
    private int blockMask;
    private float currentForward;
    private float currentIntensity;

    private void Awake()
    {
        blockMask = ~(LayerMask.GetMask("Player", "PickupItem"));
        TryAutoSetup();
    }

    private void OnEnable()
    {
        TryAutoSetup();
        currentForward = defaultLocalForward;
        currentIntensity = baseIntensity;
    }

    private void TryAutoSetup()
    {
        if (lightPivot == null)
        {
            Transform found = FindDeepChild(transform, "Flashlight_SpotLight");
            if (found != null)
            {
                lightPivot = found;
                baseLocalPos = lightPivot.localPosition;
                defaultLocalForward = Mathf.Abs(baseLocalPos.x) > 0.001f
                    ? Mathf.Abs(baseLocalPos.x)
                    : baseLocalPos.magnitude;
            }
        }

        if (spotLight == null && lightPivot != null)
            spotLight = lightPivot.GetComponent<Light>();

        if (spotLight != null && baseIntensity <= 0f)
            baseIntensity = spotLight.intensity;
    }

    private void LateUpdate()
    {
        if (lightPivot == null)
        {
            TryAutoSetup();
            return;
        }

        if (spotLight == null)
            spotLight = lightPivot.GetComponent<Light>();

        Transform mount = lightPivot.parent;
        if (mount == null)
            return;

        Vector3 origin = mount.position;
        Vector3 beamDir = lightPivot.forward;
        float maxProbe = defaultLocalForward + probeExtra;

        float targetForward = defaultLocalForward;
        float surfaceDistance = maxProbe;

        if (Physics.SphereCast(
                origin,
                sphereCastRadius,
                beamDir,
                out RaycastHit hit,
                maxProbe,
                blockMask,
                QueryTriggerInteraction.Ignore))
        {
            float allowed = hit.distance - surfaceOffset;
            targetForward = Mathf.Clamp(allowed, minLocalForward, defaultLocalForward);
            surfaceDistance = hit.distance;
        }

        currentForward = Mathf.Lerp(currentForward, targetForward, Time.deltaTime * positionSmooth);

        Vector3 targetLocal = baseLocalPos;
        if (Mathf.Abs(baseLocalPos.x) > 0.001f)
            targetLocal.x = Mathf.Sign(baseLocalPos.x) * currentForward;
        else
            targetLocal = baseLocalPos.normalized * currentForward;

        lightPivot.localPosition = targetLocal;

        if (spotLight != null && baseIntensity > 0f)
        {
            if (!spotLight.enabled)
            {
                currentIntensity = baseIntensity;
                return;
            }

            float fade = Mathf.SmoothStep(fadeEndDistance, fadeStartDistance, surfaceDistance);
            float targetIntensity = baseIntensity * fade;
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.deltaTime * intensitySmooth);
            spotLight.intensity = currentIntensity;
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
