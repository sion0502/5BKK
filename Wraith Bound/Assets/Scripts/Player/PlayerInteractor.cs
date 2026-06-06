using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] public float interactRange = 3f;
    [SerializeField] private InteractionCrosshairUI interactionUI;

    [Tooltip("상호작용 레이에서 무시할 레이어. 비어 있으면 기본값(PickupItem, Player, UI 등)을 사용합니다.")]
    [SerializeField] private LayerMask raycastIgnoreLayers;

    private LayerMask interactionRayMask;
    private IInteractable currentTarget;

    void Awake()
    {
        if (raycastIgnoreLayers.value == 0)
        {
            raycastIgnoreLayers = LayerMask.GetMask(
                "PickupItem",
                "Player",
                "UI",
                "Ignore Raycast",
                "TransparentFX",
                "RaderIcon",
                "NightVision",
                "Floor");
        }

        interactionRayMask = ~raycastIgnoreLayers;

        if (interactionUI == null)
        {
            interactionUI = GetComponent<InteractionCrosshairUI>();
        }

        if (interactionUI == null)
        {
            interactionUI = gameObject.AddComponent<InteractionCrosshairUI>();
        }
    }

    void Update()
    {
        HandleRaycast();
        HandleInput();
    }

    private void HandleRaycast()
    {
        if (playerCamera == null)
        {
            ClearTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactionRayMask, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                currentTarget = interactable;

                if (interactionUI != null)
                {
                    interactionUI.SetItemName(ResolveInteractionLabel(interactable, hit.collider));
                }

                return;
            }
        }

        ClearTarget();
    }

    private void HandleInput()
    {
        if (currentTarget != null && Input.GetButtonDown("Interact"))
        {
            currentTarget.Interact(gameObject);
            Debug.Log("E키 입력 감지 및 상호작용 실행!");
        }
    }

    private void ClearTarget()
    {
        currentTarget = null;

        if (interactionUI != null)
        {
            interactionUI.SetItemName(null);
        }
    }

    private static string ResolveInteractionLabel(IInteractable interactable, Collider hitCollider)
    {
        string prompt = interactable.GetInteractPrompt();
        if (!string.IsNullOrEmpty(prompt))
        {
            return prompt;
        }

        if (InteractableItemNameUtility.TryGetItemName(hitCollider, out string itemName))
        {
            return itemName;
        }

        return null;
    }
}
