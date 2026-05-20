using Unity.VisualScripting;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] public float interactRange = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private InteractionCrosshairUI interactionUI;

    private IInteractable currentTarget;

    void Awake()
    {
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
        // 카메라의 위치에서 전방(화면 정중앙)으로 레이 발사
        Ray ray = new Ray(playerCamera.transform.position,playerCamera.transform.forward);
        RaycastHit hit;

        if(Physics.Raycast(ray, out hit, interactRange, interactableLayer))
        {
            // 충돌한 오브젝트가 IInteractable 인터페이스를 가지고 있는지 검사
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            //Debug.Log($"Ray에 맞은 오브젝트: {hit.collider.name}");

            if (interactable != null)
            {
                currentTarget = interactable;

                if (interactionUI != null)
                {
                    if (InteractableItemNameUtility.TryGetItemName(hit.collider, out string itemName))
                    {
                        interactionUI.SetItemName(itemName);
                    }
                    else
                    {
                        interactionUI.SetItemName(null);
                    }
                }

                return;
            }
            else
            {
                //Debug.LogWarning("맞은 오브젝트에 IInteractable 컴포넌트가 없습니다.");
            }
        }

        if (currentTarget != null)
        {
            currentTarget = null;
        }

        if (interactionUI != null)
        {
            interactionUI.SetItemName(null);
        }
    }

    private void HandleInput()
    {
        if (currentTarget != null && Input.GetButtonDown("Interact"))
        {
            // 인터페이스를 통해 다형성 호출. 자기 자신(플레이어)의 게임 오브젝트를 넘겨줍니다.
            currentTarget.Interact(this.gameObject);

            Debug.Log("E키 입력 감지 및 상호작용 실행!");
        }
    }
}
