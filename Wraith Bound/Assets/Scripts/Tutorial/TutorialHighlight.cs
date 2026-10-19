using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 대상(데스크 위 손전등, 로비 캐비닛 등)에 외곽선을 상시 표시합니다.
/// 대상의 SpotOutline(조준 시에만 표시)은 하이라이트 동안 잠시 꺼 두고, 끝나면 되돌립니다.
/// 대상 하위에 Outline이 여러 개면(캐비닛 양쪽 문 등) 전부 표시합니다.
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

    private struct OutlineState
    {
        public Outline outline;
        public bool added;
        public bool wasEnabled;
        public Color color;
        public float width;
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

        Outline[] existing = resolved.GetComponentsInChildren<Outline>(true);
        if (existing.Length == 0)
        {
            AddOutline(resolved.gameObject.AddComponent<Outline>(), true);
        }
        else
        {
            foreach (Outline outline in existing)
            {
                AddOutline(outline, false);
            }
        }

        isActive = true;
    }

    private void AddOutline(Outline outline, bool added)
    {
        outlines.Add(new OutlineState
        {
            outline = outline,
            added = added,
            wasEnabled = outline.enabled,
            color = outline.OutlineColor,
            width = outline.OutlineWidth
        });

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
