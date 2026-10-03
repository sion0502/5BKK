using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 대상(데스크 위 손전등)에 외곽선을 상시 표시합니다.
/// 대상의 SpotOutline(조준 시에만 표시)은 하이라이트 동안 잠시 꺼 두고, 끝나면 되돌립니다.
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

    private Outline outline;
    private bool addedOutline;
    private Color originalColor;
    private float originalWidth;
    private readonly List<SpotOutline> pausedSpotOutlines = new List<SpotOutline>();
    private bool isActive;

    public void Begin(Items itemData, Transform player)
    {
        End();

        Transform resolved = target != null ? target : FindNearestItem(itemData, player);
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

        outline = resolved.GetComponentInChildren<Outline>();
        addedOutline = outline == null;
        if (addedOutline)
        {
            outline = resolved.gameObject.AddComponent<Outline>();
        }

        originalColor = outline.OutlineColor;
        originalWidth = outline.OutlineWidth;
        outline.OutlineColor = highlightColor;
        outline.enabled = true;
        isActive = true;
    }

    public void End()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;

        // 손전등을 주우면 오브젝트가 파괴되므로 null 체크
        if (outline != null)
        {
            if (addedOutline)
            {
                Destroy(outline);
            }
            else
            {
                outline.OutlineColor = originalColor;
                outline.OutlineWidth = originalWidth;
                outline.enabled = false;
            }
        }

        foreach (SpotOutline spot in pausedSpotOutlines)
        {
            if (spot != null)
            {
                spot.enabled = true;
            }
        }

        pausedSpotOutlines.Clear();
        outline = null;
    }

    void Update()
    {
        if (!isActive || outline == null)
        {
            return;
        }

        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        outline.OutlineWidth = Mathf.Lerp(minWidth, maxWidth, t);
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
