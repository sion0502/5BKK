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

        // 레이 시작점(카메라)이 문 등의 조준 판정 트리거 안에 들어가 있으면 그 트리거엔 레이가 맞지 않음
        // → 문에 바짝 붙으면 '열기'가 사라지던 문제. 트리거 안이면 실제 문짝 쪽을 보고 있는지로 따로 판정.
        if (TryGetEnclosingTargetHit(ray, range, playerRoot, out RaycastHit enclosed))
        {
            closest = enclosed;
            return true;
        }

        // Keep a solid-surface fallback for hiding-spot detection and useful diagnostics.
        closest = surface;
        return closest.collider != null;
    }

    // 트리거 안에서 판정할 때: 레이가 문짝(실제 콜라이더) 범위에서 이만큼 벗어나도 조준한 것으로 침
    private const float EnclosedAimPadding = 0.45f;
    // 문짝 위쪽은 더 넉넉히 — 1.3배 플레이어는 눈높이가 문 꼭대기와 비슷해서 정면을 보면 문 위 벽을 봄
    private const float EnclosedAimHeadroom = 0.6f;
    // 수평으로 이 각도 안쪽에 문이 있어야 함 (등지고 서 있을 때 뜨지 않게)
    private const float EnclosedMaxYaw = 75f;
    private static readonly Collider[] EnclosingBuffer = new Collider[16];

    private static bool TryGetEnclosingTargetHit(Ray ray, float range, Transform playerRoot, out RaycastHit result)
    {
        result = default;
        float bestDistance = float.PositiveInfinity;
        int count = Physics.OverlapSphereNonAlloc(ray.origin, 0.01f, EnclosingBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider trigger = EnclosingBuffer[i];
            if (trigger == null || !trigger.isTrigger) continue;
            if (playerRoot != null && trigger.transform.IsChildOf(playerRoot)) continue;

            IInteractable candidate = ResolveTarget(trigger);
            if (!(candidate is Component owner)) continue;
            if (!TryAimAtSolidPart(ray, range, owner.transform, candidate, out RaycastHit hit)) continue;
            if (hit.distance > GetTargetRange(candidate) || hit.distance >= bestDistance) continue;
            if (!HasClearPath(ray.origin, hit.point, owner.transform, playerRoot)) continue;

            bestDistance = hit.distance;
            result = hit;
        }

        return result.collider != null;
    }

    /// <summary>대상의 실제(트리거 아닌) 콜라이더 중 레이가 가깝게 지나가는 것을 골라, 그 콜라이더 표면의 실제 RaycastHit을 얻음.</summary>
    private static bool TryAimAtSolidPart(Ray ray, float range, Transform owner, IInteractable candidate, out RaycastHit best)
    {
        best = default;
        float bestDistance = float.PositiveInfinity;
        foreach (Collider solid in owner.GetComponentsInChildren<Collider>())
        {
            if (!solid.enabled || solid.isTrigger || !ReferenceEquals(ResolveTarget(solid), candidate)) continue;

            Bounds bounds = solid.bounds;
            Vector3 toCenter = bounds.center - ray.origin;
            float along = Mathf.Clamp(Vector3.Dot(toCenter, ray.direction), 0f, range);
            Vector3 nearestOnRay = ray.GetPoint(along);
            Vector3 onBounds = bounds.ClosestPoint(nearestOnRay);

            // 수평 방향: 문이 앞쪽에 있어야 함
            Vector3 flatForward = new Vector3(ray.direction.x, 0f, ray.direction.z);
            Vector3 flatTo = new Vector3(onBounds.x - ray.origin.x, 0f, onBounds.z - ray.origin.z);
            if (flatForward.sqrMagnitude > 0.0001f && flatTo.sqrMagnitude > 0.0001f &&
                Vector3.Angle(flatForward, flatTo) > EnclosedMaxYaw) continue;

            // 레이가 문짝 범위(+여유, 위쪽은 더 여유)를 지나가는지
            Vector3 miss = nearestOnRay - onBounds;
            if (miss.y > 0f) miss.y = Mathf.Max(0f, miss.y - EnclosedAimHeadroom);
            if (miss.magnitude > EnclosedAimPadding) continue;

            // 회전된 문은 bounds가 실제보다 커서 표면점을 놓칠 수 있음 → 중심 쪽으로 조금 당긴 점, 안 되면 중심
            Vector3 aim = Vector3.Lerp(onBounds, bounds.center, 0.15f);
            if (!RaycastSolid(solid, ray.origin, aim, range, out RaycastHit hit) &&
                !RaycastSolid(solid, ray.origin, bounds.center, range, out hit)) continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            best = hit;
        }

        return best.collider != null;
    }

    private static bool RaycastSolid(Collider solid, Vector3 origin, Vector3 target, float range, out RaycastHit hit)
    {
        Vector3 delta = target - origin;
        if (delta.sqrMagnitude < 0.000001f)
        {
            hit = default;
            return false;
        }

        return solid.Raycast(new Ray(origin, delta.normalized), out hit, range);
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
