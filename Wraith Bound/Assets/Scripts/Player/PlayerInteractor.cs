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
    public Camera InteractionCamera => playerCamera;

    /// <summary>지금 조준 중인 IInteractable (없으면 null). 크로스헤어 링·행동 문구는 InteractionAimIndicator가 이걸 읽어 표시.</summary>
    public IInteractable CurrentTarget { get; private set; }
    /// <summary>조준 대상의 안내 문구 ("[E] 손전등 획득" 등, 없으면 null).</summary>
    public string CurrentLabel { get; private set; }
    public Camera ViewCamera => playerCamera;

    public static float GetTargetRange(IInteractable target)
    {
        if (target is DoorClick door) return Mathf.Max(0f, door.interactDistance);
        if (target is WardrobeDoorInteract wardrobe) return Mathf.Max(0f, wardrobe.interactDistance);
        return float.PositiveInfinity;
    }

    public static bool CanInteractWith(GameObject player, IInteractable target, float range)
    {
        if (player == null || range <= 0f) return false;
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        Camera camera = interactor != null ? interactor.InteractionCamera : player.GetComponentInChildren<Camera>();
        return camera != null && TryGetInteractionHit(new Ray(camera.transform.position, camera.transform.forward),
            range, player.transform, out RaycastHit hit) && ReferenceEquals(ResolveTarget(hit.collider), target);
    }

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (interactionUI == null) interactionUI = GetComponent<InteractionCrosshairUI>();
        if (interactionUI == null) interactionUI = gameObject.AddComponent<InteractionCrosshairUI>();
        // 조준 링·행동 문구 (아이템·문·숨는 곳 등 모든 상호작용 대상)
        if (GetComponent<InteractionAimIndicator>() == null) gameObject.AddComponent<InteractionAimIndicator>();
    }

    // First gather interaction candidates, then test visibility to their actual hit surface.
    // Movement collision, target detection and occlusion are separate responsibilities.
    public static bool TryGetInteractionHit(Ray ray, float range, Transform playerRoot, out RaycastHit closest)
    {
        RaycastHit[] hits = CastInteractionRay(ray, range, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        closest = default;
        RaycastHit surface = default;

        foreach (RaycastHit hit in hits)
        {
            if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
            if (!hit.collider.isTrigger && surface.collider == null) surface = hit;

            IInteractable candidate = ResolveTarget(hit.collider);
            if (candidate == null || hit.distance > GetTargetRange(candidate)) continue;
            Component owner = candidate as Component;
            if (owner != null && HasClearPath(ray.origin, hit.point, owner.transform, playerRoot))
            {
                closest = hit;
                return true;
            }
        }

        // Keep a solid-surface fallback for hiding-spot detection and useful diagnostics.
        closest = surface;
        return closest.collider != null;
    }

    private static RaycastHit[] CastInteractionRay(Ray ray, float range, QueryTriggerInteraction triggers)
    {
        bool backfaces = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            return Physics.RaycastAll(ray, Mathf.Max(0f, range), ~0, triggers);
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
    }

    public static bool HasClearPath(Vector3 origin, Vector3 destination, Transform target, Transform playerRoot)
    {
        Vector3 delta = destination - origin;
        if (delta.sqrMagnitude < 0.000001f) return true;
        // Stop just before the visible surface, not at a switch pivot buried in a wall.
        RaycastHit[] hits = CastInteractionRay(new Ray(origin, delta.normalized),
            Mathf.Max(0f, delta.magnitude - 0.001f), QueryTriggerInteraction.Ignore);
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

    private void OnDisable()
    {
        CurrentTarget = null;
        CurrentLabel = null;
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
            if (hit.distance > GetTargetRange(target)) target = null;
            if (target != null)
            {
                label = target.GetInteractPrompt();
                if (string.IsNullOrEmpty(label) &&
                    InteractableItemNameUtility.TryGetItemName(hit.collider, out string itemName))
                    label = itemName;
            }
        }

        // 표시는 InteractionAimIndicator가 함 (여기서 SetItemName을 부르면 실행 순서에 따라 링 문구를 지움)
        CurrentTarget = target;
        CurrentLabel = label;
        if (target != null && Input.GetButtonDown("Interact"))
            target.Interact(gameObject);
        else if (logInteractionFailures && Input.GetButtonDown("Interact"))
        {
            string reason = CurrentHitCollider != null
                ? $"No usable target selected. Crosshair surface: {GetObjectPath(CurrentHitCollider.transform)} ({CurrentHitCollider.GetType().Name}, distance {CurrentHitDistance:F2}m). Aim, target range, or occlusion prevented interaction."
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
