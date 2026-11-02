using UnityEngine;

[RequireComponent(typeof(Outline))]
public class SpotOutline : MonoBehaviour
{
    public float maxDistance = 3f;

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
        Transform playerRoot = null;
        PlayerInteractor interactor = viewCamera.GetComponentInParent<PlayerInteractor>();
        if (interactor != null) playerRoot = interactor.transform;
        if (PlayerInteractor.TryGetInteractionHit(ray, maxDistance, playerRoot, out RaycastHit hit))
        {
            shouldShow = IsTargetCollider(hit.collider);
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

        if (hitTransform.IsChildOf(transform))
        {
            return true;
        }

        return false;
    }
}
