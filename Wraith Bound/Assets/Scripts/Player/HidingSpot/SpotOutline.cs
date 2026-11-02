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

    // Read the central selection after PlayerInteractor.Update, not before it.
    void LateUpdate()
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
        if (interactor != null && interactor.isActiveAndEnabled)
        {
            shouldShow = interactor.CurrentHitDistance <= maxDistance &&
                IsTargetCollider(interactor.CurrentHitCollider);
        }
        else if (PlayerInteractor.TryGetInteractionHit(ray, maxDistance, playerRoot, out RaycastHit hit))
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
        // Some assets put the mesh/outline below the collider's door object.
        // Match only that same interaction owner, never an arbitrary common tile root.
        IInteractable owner = GetComponentInParent<IInteractable>();
        if (owner != null && ReferenceEquals(owner, PlayerInteractor.ResolveTarget(col)))
            return true;
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
