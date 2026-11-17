using UnityEngine;

/// <summary>
/// 탈 눈구멍 안의 붉게 빛나는 점 두 개 — 추격 중과 점프스케어 중에만 켜짐.
/// 머리 뼈 자식으로 작은 발광 구 + 은은한 붉은 점광(탈 안쪽을 물들임)을 실행 중에 만듦.
/// 위치는 머리 뼈 로컬 좌표 (탈춤귀신: 정면 렌더에서 초승달 눈 위치를 찍어 구함).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CreatureEyeGlow : MonoBehaviour
{
    [SerializeField] private Vector3 leftEyeLocal = new Vector3(0.0416f, 0.1561f, 0.0864f);
    [SerializeField] private Vector3 rightEyeLocal = new Vector3(-0.0238f, 0.1575f, 0.0855f);
    [Tooltip("점 크기(월드 m)")]
    [SerializeField] private float dotSize = 0.014f;
    [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(6f, 0.15f, 0.05f); // 블룸으로 두 눈빛이 겹쳐 번지는 모습 (사용자 선택, 2026-11-07)
    [Tooltip("탈 안쪽만 살짝 물들 정도 — 세면 붉은 번짐이 점보다 커 보이고 천장까지 비침")]
    [SerializeField] private float lightIntensity = 0.06f;
    [SerializeField] private float lightRange = 0.3f;
    [SerializeField] private float fadeSpeed = 6f;
    [Tooltip("켜져 있는 동안 아주 약하게 깜빡임")]
    [SerializeField] private float flicker = 0.15f;

    private Animator animator;
    private Transform root;
    private Material material;
    private Light glowLight;
    private float level;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null)
        {
            enabled = false;
            return;
        }

        root = new GameObject("EyeGlow").transform;
        root.SetParent(head, false);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(shader);
        material.SetColor("_BaseColor", glowColor);
        CreateDot(head, leftEyeLocal);
        CreateDot(head, rightEyeLocal);

        var lightGo = new GameObject("EyeGlowLight");
        lightGo.transform.SetParent(head, false);
        lightGo.transform.localPosition = (leftEyeLocal + rightEyeLocal) * 0.5f;
        glowLight = lightGo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(1f, 0.08f, 0.04f);
        glowLight.range = lightRange;
        glowLight.shadows = LightShadows.None;
        lightGo.transform.SetParent(root, true);

        Apply(0f);
    }

    private void CreateDot(Transform head, Vector3 local)
    {
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "EyeDot";
        Destroy(dot.GetComponent<Collider>());
        dot.transform.SetParent(head, false);
        dot.transform.localPosition = local;
        dot.transform.SetParent(root, true);
        // 머리 뼈 스케일(비균등)과 무관하게 동그란 점
        Vector3 lossy = root.lossyScale;
        dot.transform.localScale = new Vector3(dotSize / Mathf.Max(0.0001f, lossy.x), dotSize / Mathf.Max(0.0001f, lossy.y), dotSize / Mathf.Max(0.0001f, lossy.z));
        var renderer = dot.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>두 눈 사이 지점(월드) — 점프스케어가 탈 얼굴을 화면 중앙에 맞출 때 기준점.</summary>
    public bool TryGetEyeMidpoint(out Vector3 world)
    {
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        world = head != null ? head.TransformPoint((leftEyeLocal + rightEyeLocal) * 0.5f) : Vector3.zero;
        return head != null;
    }

    private void Update()
    {
        bool on = CreatureChaseState.IsChasing(animator) || CreatureChaseState.IsJumpscare;
        level = Mathf.MoveTowards(level, on ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        float f = level * (1f - flicker * Mathf.PerlinNoise(Time.unscaledTime * 9f, 0.37f));
        Apply(f);
    }

    private void Apply(float amount)
    {
        if (root == null)
        {
            return;
        }

        bool visible = amount > 0.01f;
        if (root.gameObject.activeSelf != visible)
        {
            root.gameObject.SetActive(visible);
        }

        material.SetColor("_BaseColor", glowColor * amount);
        glowLight.intensity = lightIntensity * amount;
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}
