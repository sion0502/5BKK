using UnityEngine;

/// <summary>
/// 인벤토리 교체로 빠진 아이템을 방금 주운 아이템 자리에 놓습니다.
/// 아이템마다 크기·콜라이더가 달라서, 바닥 높이를 맞춘 뒤 주변 콜라이더(선반·벽·다른 아이템)와
/// 겹치면 밀어내고, 공중에 뜨면 아래 표면까지 내려 앉힙니다.
/// 월드 아이템 프리팹은 Rigidbody가 없고 일부는 non-convex MeshCollider라서,
/// 겹침 계산은 아이템 AABB 크기의 임시 BoxCollider(프로브)로 합니다.
/// </summary>
public static class SwappedItemPlacement
{
    public struct Anchor
    {
        public bool valid;
        public Vector3 bottomCenter; // 놓일 표면 위 지점 (아이템 바닥 중앙)
        public float yaw;
    }

    private const float SurfaceGap = 0.003f;     // 표면과 살짝 띄우는 간격
    private const float Skin = 0.002f;           // 겹침 검사 시 상자를 살짝 줄여 표면 접촉은 무시
    private const int MaxPushIterations = 8;
    private const float MaxPushDistance = 0.6f;  // 원래 자리에서 이보다 멀리 밀려나면 실패 처리
    private const float MaxSettleDistance = 1.0f;
    private const float GroundProbeHeight = 0.5f;
    private const float GroundProbeDistance = 3f;

    private static readonly Collider[] OverlapBuffer = new Collider[32];
    private static readonly RaycastHit[] CastBuffer = new RaycastHit[16];

    /// <summary>
    /// 주운 아이템의 자리(바닥 중앙·Y 회전)를 기록하고, 아직 파괴 전인 그 콜라이더를 꺼서 배치 검사에 걸리지 않게 합니다.
    /// </summary>
    public static Anchor CaptureAndClear(GameObject picked)
    {
        Anchor anchor = default;
        if (picked == null)
        {
            return anchor;
        }

        if (TryGetWorldBounds(picked, out Bounds b))
        {
            anchor.valid = true;
            anchor.bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            anchor.yaw = picked.transform.eulerAngles.y;
        }

        foreach (Collider c in picked.GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }

        return anchor;
    }

    /// <summary>주운 자리 정보가 없을 때: 플레이어 앞 바닥을 레이캐스트로 찾아 기준점으로 씁니다.</summary>
    public static Anchor AnchorInFrontOf(Vector3 position, Vector3 forward, float distance)
    {
        Anchor anchor = default;
        Vector3 dir = forward;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        Vector3 origin = position + dir * distance + Vector3.up * GroundProbeHeight;
        if (TryRaycastDown(origin, GroundProbeDistance, out RaycastHit hit))
        {
            anchor.valid = true;
            anchor.bottomCenter = hit.point;
            anchor.yaw = Quaternion.LookRotation(dir).eulerAngles.y;
        }

        return anchor;
    }

    /// <summary>
    /// instance를 anchor 위치에 겹치지 않게 놓습니다. 실패하면 false (instance 위치는 건드린 상태일 수 있음).
    /// </summary>
    public static bool TryPlace(GameObject instance, Anchor anchor)
    {
        if (instance == null || !anchor.valid)
        {
            return false;
        }

        Transform t = instance.transform;
        t.SetPositionAndRotation(anchor.bottomCenter, Quaternion.Euler(0f, anchor.yaw, 0f));
        Physics.SyncTransforms();

        if (!TryGetWorldBounds(instance, out Bounds b))
        {
            return false;
        }

        Vector3 pivotToCenter = b.center - t.position;
        Vector3 halfExtents = Vector3.Max(b.extents, Vector3.one * 0.01f);
        Vector3 start = anchor.bottomCenter + Vector3.up * (halfExtents.y + SurfaceGap);
        Vector3 center = start;

        // 자기 콜라이더가 검사에 잡히지 않게 잠시 끔
        Collider[] own = instance.GetComponentsInChildren<Collider>();
        bool[] ownEnabled = new bool[own.Length];
        for (int i = 0; i < own.Length; i++)
        {
            ownEnabled[i] = own[i].enabled;
            own[i].enabled = false;
        }

        GameObject probeObj = new GameObject("SwappedItemPlacementProbe");
        probeObj.layer = 2; // Ignore Raycast
        probeObj.transform.position = new Vector3(0f, -10000f, 0f);
        BoxCollider probe = probeObj.AddComponent<BoxCollider>();
        probe.size = halfExtents * 2f;

        bool placed = false;
        try
        {
            // 아래에 받쳐 줄 표면이 없으면(선반 밖 허공으로 밀려남 등) 실패 처리해 대체 위치를 쓰게 함
            if (ResolvePenetration(probe, ref center, halfExtents, start)
                && Settle(ref center, halfExtents))
            {
                placed = !IsBlocked(center, halfExtents);
            }
        }
        finally
        {
            probe.enabled = false;
            Object.Destroy(probeObj);
            for (int i = 0; i < own.Length; i++)
            {
                if (own[i] != null)
                {
                    own[i].enabled = ownEnabled[i];
                }
            }
        }

        if (placed)
        {
            t.position = center - pivotToCenter;
            Physics.SyncTransforms();
        }

        return placed;
    }

    private static bool ResolvePenetration(BoxCollider probe, ref Vector3 center, Vector3 halfExtents, Vector3 start)
    {
        Vector3 queryHalf = ShrinkedHalf(halfExtents);

        for (int iter = 0; iter < MaxPushIterations; iter++)
        {
            int count = Physics.OverlapBoxNonAlloc(center, queryHalf, OverlapBuffer, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            bool pushed = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = OverlapBuffer[i];
                if (ShouldIgnore(other, probe))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        probe, center, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist) && dist > 0f)
                {
                    center += dir * (dist + Skin);
                    pushed = true;
                }
            }

            if ((center - start).sqrMagnitude > MaxPushDistance * MaxPushDistance)
            {
                return false;
            }

            if (!pushed)
            {
                return true;
            }
        }

        return !IsBlocked(center, halfExtents);
    }

    /// <summary>밀려나서 공중에 떴으면 아래 표면까지 내립니다. MaxSettleDistance 안에 표면이 없으면 false.</summary>
    private static bool Settle(ref Vector3 center, Vector3 halfExtents)
    {
        int count = Physics.BoxCastNonAlloc(center, ShrinkedHalf(halfExtents), Vector3.down, CastBuffer,
            Quaternion.identity, MaxSettleDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = CastBuffer[i];
            if (ShouldIgnore(hit.collider, null) || hit.distance <= 0f)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, hit.distance);
        }

        if (nearest == float.MaxValue)
        {
            return false;
        }

        // ShrinkedHalf로 캐스트했으므로 그만큼 보정해 표면 위 SurfaceGap에 놓음
        float drop = nearest - Skin - SurfaceGap;
        if (drop > 0f)
        {
            center += Vector3.down * drop;
        }

        return true;
    }

    private static bool IsBlocked(Vector3 center, Vector3 halfExtents)
    {
        int count = Physics.OverlapBoxNonAlloc(center, ShrinkedHalf(halfExtents), OverlapBuffer, Quaternion.identity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!ShouldIgnore(OverlapBuffer[i], null))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRaycastDown(Vector3 origin, float distance, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, CastBuffer, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (ShouldIgnore(CastBuffer[i].collider, null))
            {
                continue;
            }

            if (CastBuffer[i].distance < nearest)
            {
                nearest = CastBuffer[i].distance;
                best = CastBuffer[i];
            }
        }

        return nearest < float.MaxValue;
    }

    private static bool ShouldIgnore(Collider c, Collider probe)
    {
        // 플레이어·적의 CharacterController는 장애물로 보지 않음 (아이템이 밀려나 엉뚱한 곳에 가지 않게)
        return c == null || c == probe || c is CharacterController;
    }

    private static Vector3 ShrinkedHalf(Vector3 halfExtents)
    {
        return Vector3.Max(halfExtents - Vector3.one * Skin, Vector3.one * 0.001f);
    }

    /// <summary>활성 non-trigger 콜라이더 합산 bounds, 없으면 렌더러 bounds.</summary>
    private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool has = false;

        foreach (Collider c in go.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger)
            {
                continue;
            }

            if (!has)
            {
                bounds = c.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        if (has)
        {
            return true;
        }

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer)
            {
                continue;
            }

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return has;
    }
}
