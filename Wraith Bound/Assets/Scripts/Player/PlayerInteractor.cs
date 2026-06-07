using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    private const float BlockDistanceEpsilon = 0.01f;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];
    private static readonly HitDistanceComparer DistanceComparer = new HitDistanceComparer();

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
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            interactRange,
            interactionRayMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            ClearTarget();
            return;
        }

        if (hitCount > 1)
        {
            Array.Sort(HitBuffer, 0, hitCount, DistanceComparer);
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider == null)
            {
                continue;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                continue;
            }

            if (IsInteractionBlocked(HitBuffer, hitCount, hit))
            {
                continue;
            }

            currentTarget = interactable;

            if (interactionUI != null)
            {
                interactionUI.SetItemName(ResolveInteractionLabel(interactable, hit.collider));
            }

            return;
        }

        ClearTarget();
    }

    /// <summary>
    /// 아이템보다 가까운 히트 중, 닫힌 문·외부 벽 등만 차단합니다.
    /// 같은 캐비넷/서랍 내부 선반·측면 collider는 무시합니다.
    /// </summary>
    private static bool IsInteractionBlocked(RaycastHit[] hits, int hitCount, RaycastHit interactableHit)
    {
        Transform furnitureRoot = GetFurnitureRoot(interactableHit.collider.transform);
        float targetDistance = interactableHit.distance;

        for (int i = 0; i < hitCount; i++)
        {
            if (hits[i].distance >= targetDistance - BlockDistanceEpsilon)
            {
                break;
            }

            Collider blocker = hits[i].collider;
            if (blocker == null)
            {
                continue;
            }

            if (IsHardInteractionBlocker(blocker, furnitureRoot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHardInteractionBlocker(Collider blocker, Transform furnitureRoot)
    {
        DoorClick door = blocker.GetComponentInParent<DoorClick>();
        if (door != null)
        {
            return !door.IsOpen();
        }

        if (furnitureRoot != null && IsTransformUnderRoot(blocker.transform, furnitureRoot))
        {
            return false;
        }

        return true;
    }

    private static Transform GetFurnitureRoot(Transform from)
    {
        Transform current = from;
        while (current != null)
        {
            // 스폰 아이템은 SpawnPoint 아래, RandomItemSpawner는 형제라 GetComponentInParent로는 못 찾음
            if (current.GetComponentInChildren<RandomItemSpawner>(true) != null)
            {
                return current;
            }

            if (current.CompareTag("Cabinet"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool IsTransformUnderRoot(Transform t, Transform root)
    {
        if (t == null || root == null)
        {
            return false;
        }

        return t == root || t.IsChildOf(root);
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

    private sealed class HitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }
}
