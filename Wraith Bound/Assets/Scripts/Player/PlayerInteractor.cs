using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] public float interactRange = 3f;
    [SerializeField] private InteractionCrosshairUI interactionUI;
    [SerializeField] private bool logInteractionFailures = true;

    public Collider CurrentHitCollider { get; private set; }
    public float CurrentHitDistance { get; private set; }

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (interactionUI == null) interactionUI = GetComponent<InteractionCrosshairUI>();
        if (interactionUI == null) interactionUI = gameObject.AddComponent<InteractionCrosshairUI>();
    }

    // World geometry, including Floor, always blocks interaction.
    // RaycastAll avoids losing the nearest wall when a fixed buffer fills up.
    public static bool TryGetInteractionHit(Ray ray, float range, Transform playerRoot, out RaycastHit closest)
    {
        // Do not exclude world objects by their layer: imported walls can use any layer.
        RaycastHit[] hits;
        bool backfaces = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Ignore);
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        closest = default;
        float distance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
            if (hit.distance >= distance) continue;
            distance = hit.distance;
            closest = hit;
        }
        return closest.collider != null;
    }

    public static bool HasClearPath(Vector3 origin, Vector3 destination, Transform target, Transform playerRoot)
    {
        Vector3 delta = destination - origin;
        if (delta.sqrMagnitude < 0.000001f) return true;
        bool backfaces = Physics.queriesHitBackfaces;
        RaycastHit[] hits;
        try
        {
            Physics.queriesHitBackfaces = true;
            hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(target)) continue;
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
            return false;
        }
        return true;
    }

    public static IInteractable ResolveTarget(Collider collider)
    {
        foreach (MonoBehaviour component in collider.GetComponentsInParent<MonoBehaviour>())
        {
            if (component is IInteractable target && component.isActiveAndEnabled)
                return target;
        }
        return null;
    }

    void Update()
    {
        CurrentHitCollider = null;
        CurrentHitDistance = float.PositiveInfinity;
        IInteractable target = null;
        string label = null;
        PlayerHidingController hiding = GetComponent<PlayerHidingController>();
        if (playerCamera != null && (hiding == null || (!hiding.isHiding && !hiding.IsTransitioning)) &&
            TryGetInteractionHit(new Ray(playerCamera.transform.position, playerCamera.transform.forward),
                interactRange, transform, out RaycastHit hit))
        {
            CurrentHitCollider = hit.collider;
            CurrentHitDistance = hit.distance;
            target = ResolveTarget(hit.collider);
            if (target != null)
            {
                label = target.GetInteractPrompt();
                if (string.IsNullOrEmpty(label) &&
                    InteractableItemNameUtility.TryGetItemName(hit.collider, out string itemName))
                    label = itemName;
            }
        }

        if (interactionUI != null) interactionUI.SetItemName(label);
        if (target != null && Input.GetButtonDown("Interact"))
            target.Interact(gameObject);
        else if (logInteractionFailures && Input.GetButtonDown("Interact"))
        {
            string reason = CurrentHitCollider != null
                ? $"Blocked by {GetObjectPath(CurrentHitCollider.transform)} ({CurrentHitCollider.GetType().Name}, layer {LayerMask.LayerToName(CurrentHitCollider.gameObject.layer)})"
                : "No visible collider in interaction range, or player is hiding/transitioning";
            Debug.Log($"[Interaction] {reason}", CurrentHitCollider);
        }
    }

    private static string GetObjectPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }
}
