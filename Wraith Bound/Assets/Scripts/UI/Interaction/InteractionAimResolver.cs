using System.Reflection;
using UnityEngine;

/// <summary>
/// IInteractable을 쓰지 않고 각자 E키를 처리하는 월드 오브젝트(문·잠긴 문·숨는 곳·옷장 문·레버)를
/// 조준했는지 판별해 안내 문구를 만듭니다. 대상 스크립트는 수정하지 않고 공개 상태값(일부는 리플렉션)을 읽기만 함.
/// 판정 거리·조건은 각 스크립트의 실제 동작 조건과 맞춤 (문 = DoorClick.interactDistance, 숨기 = PlayerHidingController 설정).
/// </summary>
public static class InteractionAimResolver
{
    private const float MaxDistance = 5f;
    private const float LeverDistance = 3f;

    private static readonly RaycastHit[] DoorHits = new RaycastHit[16];

    private static int aimMask;
    private static FieldInfo wardrobeOpenField;
    private static FieldInfo leverActivatedField;

    public static InteractionPrompt Resolve(Camera camera, GameObject player)
    {
        if (camera == null)
        {
            return InteractionPrompt.None;
        }

        PlayerHidingController hiding = player != null ? player.GetComponent<PlayerHidingController>() : null;
        if (hiding != null && hiding.isHiding)
        {
            return InteractionPrompt.E("나오기");
        }

        Ray ray = new Ray(camera.transform.position, camera.transform.forward);

        InteractionPrompt hide = ResolveHidingSpot(ray, hiding);
        if (hide.IsValid)
        {
            return hide;
        }

        InteractionPrompt door = ResolveDoor(ray, player);
        if (door.IsValid)
        {
            return door;
        }

        if (aimMask == 0)
        {
            aimMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "UI", "PickupItem", "RaderIcon", "NightVision");
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, MaxDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            return InteractionPrompt.None;
        }

        Collider col = hit.collider;
        InteractionPrompt wardrobe = ResolveWardrobe(col, hit.distance);
        return wardrobe.IsValid ? wardrobe : ResolveLever(col, hit.distance);
    }

    // PlayerHidingController.DetectHidingSpot과 같은 조건 (자기 레이어 마스크·거리·정면 각도)
    private static InteractionPrompt ResolveHidingSpot(Ray ray, PlayerHidingController hiding)
    {
        if (hiding == null || !hiding.enabled)
        {
            return InteractionPrompt.None;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, hiding.interactionDistance, hiding.interactableLayer, QueryTriggerInteraction.Ignore))
        {
            return InteractionPrompt.None;
        }

        HidingSpot spot = hit.collider.GetComponent<HidingSpot>();
        if (spot == null || Vector3.Angle(hit.normal, spot.transform.forward) > hiding.maxFrontAngle)
        {
            return InteractionPrompt.None;
        }

        return InteractionPrompt.E("숨기");
    }

    // DoorClick.IsAimedAtDoor와 같은 방식: 레이어 마스크 없이 트리거까지 포함한 레이에 맞은 것 중
    // (가까운 순으로) 문 자신·자식·부모 콜라이더가 있으면 그 문. 캐비닛·옷장 문짝 콜라이더는 트리거라 트리거를 빼면 못 찾음.
    private static InteractionPrompt ResolveDoor(Ray ray, GameObject player)
    {
        int count = Physics.RaycastNonAlloc(ray, DoorHits, MaxDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(DoorHits, 0, count, Comparer.Instance);

        for (int i = 0; i < count; i++)
        {
            Collider col = DoorHits[i].collider;
            if (col == null || (player != null && col.transform.IsChildOf(player.transform)))
            {
                continue;
            }

            DoorClick door = col.GetComponentInParent<DoorClick>();
            if (door == null)
            {
                door = col.GetComponentInChildren<DoorClick>();
            }

            if (door != null)
            {
                InteractionPrompt prompt = ResolveDoor(door, DoorHits[i].distance, player);
                if (prompt.IsValid)
                {
                    return prompt;
                }
            }
        }

        return InteractionPrompt.None;
    }

    private sealed class Comparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly Comparer Instance = new Comparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private static InteractionPrompt ResolveDoor(DoorClick door, float distance, GameObject player)
    {
        if (door.IsBroken())
        {
            return InteractionPrompt.None;
        }

        LockedDoor locked = door.GetComponent<LockedDoor>();
        if (locked != null && locked.IsLocked)
        {
            if (distance > 3f)
            {
                return InteractionPrompt.None;
            }

            if (locked.UnlockMethod == LockedDoor.UnlockType.Key)
            {
                InventoryManager inventory = player != null ? player.GetComponent<InventoryManager>() : null;
                return inventory != null && inventory.HasKey()
                    ? new InteractionPrompt("좌클릭", "열쇠로 열기")
                    : new InteractionPrompt(null, "잠겨 있다 · 열쇠 필요");
            }

            return new InteractionPrompt(null, "잠겨 있다");
        }

        if (!door.enabled || distance > door.interactDistance)
        {
            return InteractionPrompt.None;
        }

        return InteractionPrompt.E(door.IsOpen() ? "닫기" : "열기");
    }

    // WardrobeDoorInteract: E 누를 때 화면 중앙 레이가 이름에 "Door"가 들어간 오브젝트에 닿으면 토글
    private static InteractionPrompt ResolveWardrobe(Collider col, float distance)
    {
        WardrobeDoorInteract wardrobe = col.GetComponentInParent<WardrobeDoorInteract>();
        if (wardrobe == null || !wardrobe.enabled || distance > wardrobe.interactDistance || !col.transform.name.Contains("Door"))
        {
            return InteractionPrompt.None;
        }

        wardrobeOpenField ??= typeof(WardrobeDoorInteract).GetField("isOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        bool open = wardrobeOpenField != null && (bool)wardrobeOpenField.GetValue(wardrobe);
        return InteractionPrompt.E(open ? "닫기" : "열기");
    }

    private static InteractionPrompt ResolveLever(Collider col, float distance)
    {
        Lever lever = col.GetComponentInParent<Lever>();
        if (lever == null || distance > LeverDistance)
        {
            return InteractionPrompt.None;
        }

        leverActivatedField ??= typeof(Lever).GetField("isActivated", BindingFlags.Instance | BindingFlags.NonPublic);
        bool activated = leverActivatedField != null && (bool)leverActivatedField.GetValue(lever);
        return activated ? InteractionPrompt.None : InteractionPrompt.E("레버 당기기");
    }
}
