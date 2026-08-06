using System.Reflection;
using UnityEngine;

public static class EnemyDoorUtility{
    public static void EnsureDoorsUseLayer(int doorLayer)
    {
        if (doorLayer < 0)
            return;

        DoorClick[] doors = Object.FindObjectsByType<DoorClick>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
                continue;

            SetLayerRecursively(doors[i].gameObject, doorLayer);
        }
    }

    static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public static Vector3 GetDoorWorldPosition(Transform doorTransform)
    {
        Collider col = doorTransform.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;
        return doorTransform.position;
    }

    public static bool HasClosedDoorBetween(Vector3 from, Vector3 to, LayerMask doorLayer, float doorCheckHeight, float pathDoorCheckRadius)
    {
        return FindClosedDoorBetween(from, to, doorLayer, doorCheckHeight, pathDoorCheckRadius) != null;
    }

    public static DoorClick FindClosedDoorBetween(Vector3 from, Vector3 to, LayerMask doorLayer, float doorCheckHeight, float pathDoorCheckRadius)
    {
        DoorClick door = FindClosedDoorBetweenLayer(from, to, doorLayer, doorCheckHeight, pathDoorCheckRadius);
        if (door != null)
            return door;

        return FindClosedDoorBetweenAny(from, to, doorCheckHeight, pathDoorCheckRadius);
    }

    static DoorClick FindClosedDoorBetweenLayer(Vector3 from, Vector3 to, LayerMask doorLayer, float doorCheckHeight, float pathDoorCheckRadius)
    {
        if (doorLayer.value == 0)
            return null;

        return SphereCastForDoor(from, to, doorCheckHeight, pathDoorCheckRadius, doorLayer);
    }

    static DoorClick FindClosedDoorBetweenAny(Vector3 from, Vector3 to, float doorCheckHeight, float pathDoorCheckRadius)
    {
        return SphereCastForDoor(from, to, doorCheckHeight, pathDoorCheckRadius, Physics.AllLayers);
    }

    static DoorClick SphereCastForDoor(Vector3 from, Vector3 to, float doorCheckHeight, float pathDoorCheckRadius, LayerMask mask)
    {
        Vector3 start = from + Vector3.up * doorCheckHeight;
        Vector3 end = to + Vector3.up * 0.2f;
        Vector3 dir = end - start;
        float dist = dir.magnitude;

        if (dist <= 0.1f)
            return null;

        dir.Normalize();

        RaycastHit[] hits = Physics.SphereCastAll(start, pathDoorCheckRadius, dir, dist, mask, QueryTriggerInteraction.Collide);
        DoorClick closestDoor = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].collider.GetComponentInParent<DoorClick>();
            if (door == null || door.IsOpen() || door.IsBroken())
                continue;

            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                closestDoor = door;
            }
        }

        return closestDoor;
    }

    public static DoorClick FindClosedDoorNearPosition(Vector3 position, LayerMask doorLayer, float radius)
    {
        if (doorLayer.value != 0)
        {
            DoorClick layeredDoor = FindClosedDoorInColliders(
                Physics.OverlapSphere(position, radius, doorLayer, QueryTriggerInteraction.Collide),
                position);

            if (layeredDoor != null)
                return layeredDoor;
        }

        return FindAnyClosedDoorNear(position, radius);
    }

    public static DoorClick FindAnyClosedDoorNear(Vector3 position, float radius)
    {
        return FindClosedDoorInColliders(
            Physics.OverlapSphere(position, radius, Physics.AllLayers, QueryTriggerInteraction.Collide),
            position);
    }

    static DoorClick FindClosedDoorInColliders(Collider[] hits, Vector3 referencePosition)
    {
        if (hits == null || hits.Length == 0)
            return null;

        DoorClick bestDoor = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].GetComponentInParent<DoorClick>();
            if (door == null || door.IsOpen() || door.IsBroken())
                continue;

            float dist = Vector3.Distance(referencePosition, GetDoorWorldPosition(door.transform));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    public static DoorClick FindClosedDoorOnRoute(Vector3 from, Vector3 to, float maxSideDistance = 3f)
    {
        Vector3 flatFrom = from;
        Vector3 flatTo = to;
        flatFrom.y = 0f;
        flatTo.y = 0f;

        Vector3 toDest = flatTo - flatFrom;
        float routeLength = toDest.magnitude;
        if (routeLength <= 0.5f)
            return null;

        Vector3 routeDir = toDest / routeLength;
        DoorClick[] doors = Object.FindObjectsByType<DoorClick>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        DoorClick bestDoor = null;
        float bestAlong = float.MaxValue;

        for (int i = 0; i < doors.Length; i++)
        {
            DoorClick door = doors[i];
            if (door == null || door.IsOpen() || door.IsBroken())
                continue;

            Vector3 doorPos = GetDoorWorldPosition(door.transform);
            doorPos.y = 0f;

            Vector3 toDoor = doorPos - flatFrom;
            float along = Vector3.Dot(toDoor, routeDir);
            if (along < 0.2f || along > routeLength + 0.5f)
                continue;

            Vector3 closestOnRoute = flatFrom + routeDir * along;
            if (Vector3.Distance(doorPos, closestOnRoute) > maxSideDistance)
                continue;

            if (Vector3.Distance(doorPos, flatTo) >= Vector3.Distance(flatFrom, flatTo))
                continue;

            if (along < bestAlong)
            {
                bestAlong = along;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    public static bool IsDoorUsefulForTarget(Vector3 agentPos, Vector3 doorPos, Vector3 target)
    {
        Vector3 toTarget = target - agentPos;
        Vector3 toDoor = doorPos - agentPos;
        toTarget.y = 0f;
        toDoor.y = 0f;

        if (toTarget.sqrMagnitude <= 0.01f || toDoor.sqrMagnitude <= 0.01f)
            return false;

        float dot = Vector3.Dot(toTarget.normalized, toDoor.normalized);
        if (dot < 0.15f)
            return false;

        return Vector3.Distance(doorPos, target) < Vector3.Distance(agentPos, target);
    }

    /// <summary>
    /// DoorClick 플레이어 E열기와 동일: 방향 설정 → open=true → 소리 → NavMesh 갱신
    /// </summary>
    public static bool TryOpenDoor(DoorClick door, Vector3 openerPosition)
    {
        if (door == null || door.IsOpen() || door.IsBroken())
            return false;

        System.Type doorType = typeof(DoorClick);
        FieldInfo openField = doorType.GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
        if (openField == null)
            return false;

        FieldInfo openRotField = doorType.GetField("openRot", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo autoDirectionField = doorType.GetField("autoDirection", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo doorOpenAngleField = doorType.GetField("DoorOpenAngle", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo playSoundMethod = doorType.GetMethod("PlayDoorSound", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo syncNavMethod = doorType.GetMethod("SyncNavMeshObstacle", BindingFlags.Instance | BindingFlags.NonPublic);

        bool autoDirection = autoDirectionField != null && (bool)autoDirectionField.GetValue(door);
        if (autoDirection && openRotField != null && doorOpenAngleField != null)
        {
            float openAngle = (float)doorOpenAngleField.GetValue(door);
            Vector3 openerDir = openerPosition - door.transform.position;
            float dot = Vector3.Dot(door.transform.right, openerDir);
            float angle = dot > 0f ? openAngle : -openAngle;
            Quaternion enemySideOpenRot = Quaternion.Euler(
                door.transform.eulerAngles.x,
                door.transform.eulerAngles.y + angle,
                door.transform.eulerAngles.z
            );
            openRotField.SetValue(door, enemySideOpenRot);
        }

        openField.SetValue(door, true);
        playSoundMethod?.Invoke(door, null);
        syncNavMethod?.Invoke(door, new object[] { true });
        DoorNavMeshUtility.SetNavMeshBlocked(door.transform, false);

        return true;
    }
}