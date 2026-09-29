using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// DoorClick을 잠그거나 잠금 해제하는 보조 컴포넌트입니다.
/// 문을 여닫는 동작은 잠금 해제 후 DoorClick이 전부 담당합니다.
/// </summary>
[RequireComponent(typeof(DoorClick))]
public class LockedDoor : MonoBehaviour
{
    public enum UnlockType
    {
        Key,
        Lever
    }

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    [Header("Lock")]
    [SerializeField] private UnlockType unlockType = UnlockType.Key;
    [SerializeField] private bool isLocked = true;

    [Header("Key Unlock")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float keyUseDistance = 3f;

    [Tooltip("InventoryManager에 HashKey/UseKey가 추가되기 전 테스트용입니다.")]
    [SerializeField] private bool temporaryHasKey;

    [Tooltip("아이템 연동 전 잠금 해제를 시험할 키입니다. 문을 바라본 상태에서 누르세요.")]
    [SerializeField] private KeyCode temporaryUnlockKey = KeyCode.K;

    [Header("Padlock Drop")]
    [Tooltip("비워 두면 자식 중 이름이 'Lock'으로 시작하는 오브젝트를 자동으로 찾습니다.")]
    [SerializeField] private Transform[] padlocks;
    [SerializeField] private float padlockFadeDelay = 3f;
    [SerializeField] private float padlockFadeDuration = 0.75f;
    [SerializeField] private float padlockDropForce = 0.6f;
    [SerializeField] private float padlockTorque = 1.5f;

    private DoorClick doorClick;

    public bool IsLocked => isLocked;

    private void Awake()
    {
        doorClick = GetComponent<DoorClick>();
        FindViewCamera();
        FindPadlocks();
        ApplyLockState();
    }

    private void Update()
    {
        if (!isLocked || unlockType != UnlockType.Key)
        {
            return;
        }

        FindViewCamera();

        if (Input.GetKeyDown(temporaryUnlockKey) && IsAimedAtThisDoor())
        {
            UnlockWithTemporaryInput();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Mouse0) && IsAimedAtThisDoor())
        {
            TryUnlockWithKey();
        }
    }

    /// <summary>레버나 잠금 해제 스위치에서 호출하는 외부 잠금 해제 함수입니다.</summary>
    public void UnlockDoor()
    {
        SetLocked(false);
    }

    /// <summary>레버의 bool 이벤트에 직접 연결할 때 사용합니다.</summary>
    public void SetLeverUnlocked(bool unlocked)
    {
        if (unlockType == UnlockType.Lever)
        {
            SetLocked(!unlocked);
        }
    }

    private void TryUnlockWithKey()
    {
        InventoryManager inventory = FindPlayerInventory();

        if (inventory == null)
        {
            TryUseTemporaryKey();
            return;
        }

        MethodInfo hashKey = typeof(InventoryManager).GetMethod(
            "HashKey",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            System.Type.EmptyTypes,
            null);
        MethodInfo useKey = typeof(InventoryManager).GetMethod(
            "UseKey",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            System.Type.EmptyTypes,
            null);

        // 아이템 담당자의 API가 들어오기 전에는 인스펙터 테스트 값을 사용합니다.
        if (hashKey == null || useKey == null)
        {
            TryUseTemporaryKey();
            return;
        }

        object result = hashKey.Invoke(inventory, null);
        if (!(result is bool hasKey) || !hasKey)
        {
            Debug.Log($"[{name}] 현재 들고 있는 열쇠가 없습니다.");
            return;
        }

        useKey.Invoke(inventory, null);
        SetLocked(false);
        Debug.Log($"[{name}] 열쇠로 잠금을 해제했습니다.");
    }

    private void TryUseTemporaryKey()
    {
        if (!temporaryHasKey)
        {
            Debug.Log($"[{name}] 열쇠가 필요합니다.");
            return;
        }

        temporaryHasKey = false;
        SetLocked(false);
        Debug.Log($"[{name}] 임시 열쇠로 잠금을 해제했습니다.");
    }

    private void SetLocked(bool locked)
    {
        bool wasLocked = isLocked;
        isLocked = locked;
        ApplyLockState();

        if (wasLocked && !isLocked && unlockType == UnlockType.Key)
        {
            DropPadlocks();
        }
    }

    private void ApplyLockState()
    {
        if (doorClick == null)
        {
            doorClick = GetComponent<DoorClick>();
        }

        if (doorClick != null)
        {
            doorClick.enabled = !isLocked;
        }
    }

    private bool IsAimedAtThisDoor()
    {
        if (viewCamera == null)
        {
            return false;
        }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            HitBuffer,
            keyUseDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        Collider nearestCollider = null;

        for (int i = 0; i < hitCount; i++)
        {
            if (HitBuffer[i].collider != null && HitBuffer[i].distance < nearestDistance)
            {
                nearestDistance = HitBuffer[i].distance;
                nearestCollider = HitBuffer[i].collider;
            }
        }

        if (nearestCollider == null)
        {
            return false;
        }

        Transform hitTransform = nearestCollider.transform;
        return hitTransform == transform
            || hitTransform.IsChildOf(transform)
            || transform.IsChildOf(hitTransform);
    }

    private InventoryManager FindPlayerInventory()
    {
        if (viewCamera == null)
        {
            return null;
        }

        InventoryManager inventory = viewCamera.GetComponentInParent<InventoryManager>();
        if (inventory == null)
        {
            inventory = FindFirstObjectByType<InventoryManager>();
        }

        return inventory;
    }

    private void FindViewCamera()
    {
        if (viewCamera != null && viewCamera.isActiveAndEnabled)
        {
            return;
        }

        viewCamera = Camera.main;
        if (viewCamera == null)
        {
            viewCamera = FindFirstObjectByType<Camera>();
        }
    }

    private void UnlockWithTemporaryInput()
    {
        SetLocked(false);
        Debug.Log($"[{name}] 테스트 키 {temporaryUnlockKey}로 잠금을 해제했습니다.");
    }

    private void FindPadlocks()
    {
        if (padlocks != null && padlocks.Length > 0)
        {
            return;
        }

        List<Transform> found = new List<Transform>();
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child != transform && child.name.StartsWith("Lock", System.StringComparison.OrdinalIgnoreCase))
            {
                found.Add(child);
            }
        }

        padlocks = found.ToArray();
    }

    private void DropPadlocks()
    {
        FindPadlocks();

        if (padlocks == null)
        {
            return;
        }

        foreach (Transform padlock in padlocks)
        {
            if (padlock == null)
            {
                continue;
            }

            padlock.SetParent(null, true);

            Rigidbody body = padlock.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = padlock.gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.AddForce((Vector3.down + transform.forward * 0.2f) * padlockDropForce, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * padlockTorque, ForceMode.Impulse);

            StartCoroutine(FadeAndDestroyPadlock(padlock.gameObject));
        }
    }

    private IEnumerator FadeAndDestroyPadlock(GameObject padlock)
    {
        if (padlockFadeDelay > 0f)
        {
            yield return new WaitForSeconds(padlockFadeDelay);
        }

        if (padlock == null)
        {
            yield break;
        }

        Renderer[] renderers = padlock.GetComponentsInChildren<Renderer>(true);
        List<Material> materials = new List<Material>();
        List<Color> initialColors = new List<Color>();

        foreach (Renderer targetRenderer in renderers)
        {
            foreach (Material material in targetRenderer.materials)
            {
                if (!material.HasProperty("_Color") && !material.HasProperty("_BaseColor"))
                {
                    continue;
                }

                PrepareTransparentMaterial(material);
                materials.Add(material);
                initialColors.Add(GetMaterialColor(material));
            }
        }

        float duration = Mathf.Max(0.01f, padlockFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration && padlock != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < materials.Count; i++)
            {
                Color color = initialColors[i];
                color.a *= alpha;
                SetMaterialColor(materials[i], color);
            }

            yield return null;
        }

        if (padlock != null)
        {
            Destroy(padlock);
        }
    }

    private static void PrepareTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
    }

    private static Color GetMaterialColor(Material material)
    {
        return material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.color;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }
}
