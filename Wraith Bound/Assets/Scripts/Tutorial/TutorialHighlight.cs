using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 대상(데스크 위 손전등, 로비 캐비닛 등)에 외곽선을 상시 표시합니다.
/// 대상의 SpotOutline(조준 시에만 표시)은 하이라이트 동안 잠시 꺼 두고, 끝나면 되돌립니다.
/// 대상 하위에 Outline이 여러 개면(캐비닛 양쪽 문 등) 전부 표시합니다.
/// 외곽선이 없는 대상엔 실행 중 붙이되, 메시 Read/Write가 꺼져 있으면 붙이지 않음
/// (빌드에서 QuickOutline이 그런 메시를 고쳐 쓰다 물체가 투명해짐) — 그땐 Glow(맥박치는 작은 빛)만으로 강조.
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    [Tooltip("비워두면 플레이어와 가장 가까운 손전등(ItemObject)을 자동으로 찾습니다.")]
    [SerializeField] private Transform target;

    [Header("Outline")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private float minWidth = 3f;
    [SerializeField] private float maxWidth = 7f;
    [SerializeField] private float pulseSpeed = 3f;
    [Tooltip("켜면 벽·문 너머에서도 외곽선이 보임 (숨을 곳으로 유도할 때)")]
    [SerializeField] private bool showThroughWalls;

    [Header("Glow (작은 빛)")]
    [Tooltip("켜면 대상 위에 맥박치는 작은 빛 — 책상 위 열쇠·서류처럼 작은 물건을 눈에 띄게")]
    [SerializeField] private bool glow;
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.45f, 1f);
    [SerializeField] private float glowRange = 1.6f;
    [SerializeField] private float glowMinIntensity = 0.5f;
    [SerializeField] private float glowMaxIntensity = 1.6f;

    private Light glowLight;

    /// <summary>대상 위에 맥박치는 작은 빛도 켤지 (Begin 전에 정해 둠).</summary>
    public bool Glow
    {
        get => glow;
        set => glow = value;
    }

    /// <summary>벽 너머에서도 보이게 (Begin 전에 정해 둠).</summary>
    public bool ShowThroughWalls
    {
        get => showThroughWalls;
        set => showThroughWalls = value;
    }

    private struct OutlineState
    {
        public Outline outline;
        public bool added;
        public bool wasEnabled;
        public Color color;
        public float width;
        public Outline.Mode mode;
    }

    private readonly List<OutlineState> outlines = new List<OutlineState>();
    private readonly List<SpotOutline> pausedSpotOutlines = new List<SpotOutline>();
    private bool isActive;

    /// <summary>아이템 하이라이트. 인스펙터 target이 비어 있으면 가장 가까운 해당 아이템을 찾습니다.</summary>
    public void Begin(Items itemData, Transform player)
    {
        Begin(target != null ? target : FindNearestItem(itemData, player));
    }

    /// <summary>지정한 오브젝트 하이라이트.</summary>
    public void Begin(Transform resolved)
    {
        End();

        if (resolved == null)
        {
            Debug.LogWarning("[Tutorial] 하이라이트 대상을 찾지 못했습니다.");
            return;
        }

        foreach (SpotOutline spot in resolved.GetComponentsInChildren<SpotOutline>())
        {
            if (spot.enabled)
            {
                spot.enabled = false;
                pausedSpotOutlines.Add(spot);
            }
        }

        bool useGlow = glow;
        Outline[] existing = resolved.GetComponentsInChildren<Outline>(true);
        if (existing.Length == 0)
        {
            if (AllMeshesReadable(resolved))
            {
                AddOutline(resolved.gameObject.AddComponent<Outline>(), true);
            }
            else
            {
                useGlow = true; // 외곽선 대신 빛으로만
                Debug.Log("[Tutorial] " + resolved.name + ": 메시 Read/Write가 꺼져 있어 외곽선 대신 빛으로 강조");
            }
        }
        else
        {
            foreach (Outline outline in existing)
            {
                AddOutline(outline, false);
            }
        }

        if (useGlow)
        {
            CreateGlow(resolved);
        }

        isActive = true;
    }

    private static bool AllMeshesReadable(Transform root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null && !filter.sharedMesh.isReadable)
            {
                return false;
            }
        }

        foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh != null && !skinned.sharedMesh.isReadable)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateGlow(Transform resolved)
    {
        Bounds bounds = new Bounds(resolved.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in resolved.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        glowLight = new GameObject("TutorialHighlightGlow").AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = glowRange;
        glowLight.intensity = glowMaxIntensity;
        glowLight.shadows = LightShadows.None;
        glowLight.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.35f);
        glowLight.transform.SetParent(resolved, true); // 물건을 집어 사라지면 같이 사라짐
    }

    private void AddOutline(Outline outline, bool added)
    {
        outlines.Add(new OutlineState
        {
            outline = outline,
            added = added,
            wasEnabled = outline.enabled,
            color = outline.OutlineColor,
            width = outline.OutlineWidth,
            mode = outline.OutlineMode
        });

        if (showThroughWalls)
        {
            outline.OutlineMode = Outline.Mode.OutlineAll;
        }

        outline.OutlineColor = highlightColor;
        outline.enabled = true;
    }

    public void End()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;

        if (glowLight != null)
        {
            Destroy(glowLight.gameObject);
            glowLight = null;
        }

        // 손전등을 주우면 오브젝트가 파괴되므로 null 체크
        foreach (OutlineState state in outlines)
        {
            if (state.outline == null)
            {
                continue;
            }

            if (state.added)
            {
                Destroy(state.outline);
            }
            else
            {
                state.outline.OutlineColor = state.color;
                state.outline.OutlineWidth = state.width;
                state.outline.OutlineMode = state.mode;
                state.outline.enabled = state.wasEnabled;
            }
        }

        outlines.Clear();

        foreach (SpotOutline spot in pausedSpotOutlines)
        {
            if (spot != null)
            {
                spot.enabled = true;
            }
        }

        pausedSpotOutlines.Clear();
    }

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float width = Mathf.Lerp(minWidth, maxWidth, t);
        if (glowLight != null)
        {
            glowLight.intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, t);
        }

        foreach (OutlineState state in outlines)
        {
            if (state.outline != null)
            {
                state.outline.OutlineWidth = width;
            }
        }
    }

    void OnDisable()
    {
        End();
    }

    private static Transform FindNearestItem(Items itemData, Transform player)
    {
        if (itemData == null)
        {
            return null;
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        Transform nearest = null;
        float nearestSqr = float.MaxValue;

        foreach (ItemObject item in FindObjectsByType<ItemObject>(FindObjectsSortMode.None))
        {
            if (item.itemData != itemData)
            {
                continue;
            }

            float sqr = (item.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = item.transform;
            }
        }

        return nearest;
    }
}
