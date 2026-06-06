using UnityEngine;

[RequireComponent(typeof(Outline))]
public class SpotOutline : MonoBehaviour
{
    public float maxDistance = 3f;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private Outline outline;
    private Camera viewCamera;

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;
        viewCamera = Camera.main;
    }

    void Update()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                return;
            }
        }

        bool shouldShow = false;
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, HitBuffer, maxDistance);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsTargetCollider(HitBuffer[i].collider))
            {
                shouldShow = true;
                break;
            }
        }

        outline.enabled = shouldShow;
    }

    private bool IsTargetCollider(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        Transform hitTransform = col.transform;
        if (hitTransform == transform)
        {
            return true;
        }

        if (hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform))
        {
            return true;
        }

        return false;
    }
}