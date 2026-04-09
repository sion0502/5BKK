using Unity.VisualScripting;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private IInteractable currentTarget;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
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

            Debug.Log($"Ray에 맞은 오브젝트: {hit.collider.name}");

            if (interactable != null)
            {
                Debug.Log("상호작용 가능한 오브젝트 감지됨!");
                if (currentTarget != interactable)
                {
                    currentTarget = interactable;
                    // TODO: UI 매니저를 호출하여 currentTarget.GetInteractPrompt() 텍스트를 화면 중앙에 표시
                }
                return;
            }
            else
            {
                Debug.LogWarning("맞은 오브젝트에 IInteractable 컴포넌트가 없습니다.");
            }
        }

        if (currentTarget != null)
        {
            currentTarget = null;
            // TODO: UI 텍스트 숨김 처리
        }
    }

    private void HandleInput()
    {
        if (currentTarget != null && Input.GetKeyDown(interactKey))
        {
            // 인터페이스를 통해 다형성 호출. 자기 자신(플레이어)의 게임 오브젝트를 넘겨줍니다.
            currentTarget.Interact(this.gameObject);

            Debug.Log("E키 입력 감지 및 상호작용 실행!");
        }
    }
}
