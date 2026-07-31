using UnityEngine;

/// <summary>
/// 야광봉.
/// - 던져질 때(ActiveItem.Use → Throw())는 프리팹 없이 절차적으로 생성됩니다
///   (길쭉한 캡슐 + 발광 머티리얼 + 실시간 Point Light + Rigidbody 포물선).
/// - World_GlowStick 프리팹(맵 배치용 최초 획득 지점)은 에디터에서 보이고 배치할 수 있도록
///   메쉬·콜라이더·Outline 등이 실제로 구워져 있는 일반 프리팹이며, 이 스크립트는 회수/획득(Interact)만 담당합니다.
/// </summary>
public class GlowStickController : MonoBehaviour, IInteractable
{
    private const string GlowStickResourcePath = "ItemDatas/Active/GlowStick";

    [Header("모양 (Throw()로 절차적 생성할 때만 사용)")]
    [SerializeField] private Vector3 capsuleScale = new Vector3(0.03f, 0.18f, 0.03f);
    [SerializeField] private Color glowColor = new Color(0.4f, 1f, 0.55f, 1f);
    [SerializeField] private float emissionIntensity = 2.5f;

    [Header("빛 (Throw()로 절차적 생성할 때만 사용)")]
    [SerializeField] private float lightRange = 5f;
    [SerializeField] private float lightIntensity = 1.6f;

    [Header("획득")]
    [Tooltip("E로 상호작용했을 때 지급할 개수. 월드 배치 획득 지점(World_GlowStick)은 5, 던진 야광봉 회수는 1.")]
    [SerializeField] private int pickupAmount = 1;

    private ActiveItem itemData;

    /// <summary>플레이어 카메라 위치·방향 기준으로 야광봉을 생성하고 포물선으로 던집니다.</summary>
    public static GlowStickController Throw(PlayerController player, float throwForce, float throwUpwardAngle)
    {
        if (player == null || player.cameraTransform == null)
        {
            return null;
        }

        Transform origin = player.cameraTransform;
        Vector3 spawnPos = origin.position + origin.forward * 0.5f;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "GlowStick";
        go.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

        GlowStickController controller = go.AddComponent<GlowStickController>();
        controller.pickupAmount = 1;
        controller.BuildVisual();

        Vector3 throwDir = (Quaternion.AngleAxis(-throwUpwardAngle, origin.right) * origin.forward).normalized;
        Rigidbody body = go.GetComponent<Rigidbody>();
        body.linearVelocity = throwDir * throwForce;

        return controller;
    }

    /// <summary>Throw() 전용: 캡슐 모양·발광 머티리얼·Point Light·Outline·물리(포물선용 Rigidbody)를 절차적으로 구성합니다.</summary>
    private void BuildVisual()
    {
        transform.localScale = capsuleScale;

        Rigidbody body = gameObject.AddComponent<Rigidbody>();
        body.mass = 0.1f;
        body.linearDamping = 0.3f;
        body.angularDamping = 0.3f;
        // 캡슐이 작고 던지는 속도가 빨라서 Discrete 충돌로는 바닥을 뚫고 지나갈 수 있음(터널링) -> Continuous로 방지.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            Material glowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "GlowStick_Mat"
            };
            glowMaterial.EnableKeyword("_EMISSION");
            glowMaterial.SetColor("_BaseColor", glowColor);
            glowMaterial.SetColor("_EmissionColor", glowColor * emissionIntensity);
            meshRenderer.material = glowMaterial;
        }

        Outline outline = gameObject.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        outline.OutlineColor = Color.white;
        outline.OutlineWidth = 1f;
        gameObject.AddComponent<SpotOutline>().maxDistance = 2f;

        GameObject lightGo = new GameObject("GlowLight");
        lightGo.transform.SetParent(transform, false);

        Light glowLight = lightGo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = lightIntensity;
        glowLight.shadows = LightShadows.None;

        itemData = Resources.Load<ActiveItem>(GlowStickResourcePath);
    }

    public void Interact(GameObject interactor)
    {
        if (itemData == null)
        {
            itemData = Resources.Load<ActiveItem>(GlowStickResourcePath);
        }

        if (itemData == null)
        {
            Debug.LogWarning($"[GlowStick] 아이템 데이터를 찾을 수 없습니다: Resources/{GlowStickResourcePath}");
            return;
        }

        InventoryManager inventory = interactor.GetComponent<InventoryManager>();
        if (inventory == null)
        {
            return;
        }

        if (inventory.AddItem(itemData, pickupAmount))
        {
            Destroy(gameObject);
        }
    }

    public string GetInteractPrompt()
    {
        if (itemData == null)
        {
            itemData = Resources.Load<ActiveItem>(GlowStickResourcePath);
        }

        string name = itemData != null ? itemData.itemName : "야광봉";
        string verb = GetComponent<Rigidbody>() != null ? "회수" : "획득";
        return $"[E] {name} {verb}";
    }
}
