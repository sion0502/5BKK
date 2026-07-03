using System.Reflection;
using UnityEngine;

public static class EnemyDoorUtility
{
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
        if (doorLayer.value == 0)
            return null;

        Vector3 start = from + Vector3.up * doorCheckHeight;
        Vector3 end = to + Vector3.up * 0.2f;
        Vector3 dir = end - start;
        float dist = dir.magnitude;

        if (dist <= 0.1f)
            return null;

        dir.Normalize();

        RaycastHit[] hits = Physics.SphereCastAll(start, pathDoorCheckRadius, dir, dist, doorLayer, QueryTriggerInteraction.Collide);
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

    public static DoorClick FindBestClosedDoorTowardTarget(Vector3 agentPos, Vector3 target, float searchRadius, LayerMask doorLayer)
    {
        if (doorLayer.value == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(agentPos, searchRadius, doorLayer, QueryTriggerInteraction.Collide);
        DoorClick bestDoor = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].GetComponentInParent<DoorClick>();
            if (door == null) continue;
            if (door.IsOpen() || door.IsBroken()) continue;

            Vector3 doorPos = GetDoorWorldPosition(door.transform);
            if (!IsDoorUsefulForTarget(agentPos, doorPos, target))
                continue;

            float score = Vector3.Distance(agentPos, doorPos) + Vector3.Distance(doorPos, target);
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    public static DoorClick FindClosedDoorNearPosition(Vector3 position, LayerMask doorLayer, float radius)
    {
        if (doorLayer.value == 0)
            return null;

        Collider[] hits = Physics.OverlapSphere(position, radius, doorLayer, QueryTriggerInteraction.Collide);
        DoorClick bestDoor = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            DoorClick door = hits[i].GetComponentInParent<DoorClick>();
            if (door == null || door.IsOpen() || door.IsBroken())
                continue;

            float dist = Vector3.Distance(position, GetDoorWorldPosition(door.transform));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    public static bool TryOpenDoor(DoorClick door, Vector3 openerPosition)
    {
        if (door == null || door.IsOpen() || door.IsBroken())
            return false;

        System.Type doorType = typeof(DoorClick);
        FieldInfo openField = doorType.GetField("open", BindingFlags.Instance | BindingFlags.NonPublic);
        if (openField == null) return false;

        MethodInfo playSoundMethod = doorType.GetMethod("PlayDoorSound", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo syncNavMethod = doorType.GetMethod("SyncNavMeshObstacle", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo openRotField = doorType.GetField("openRot", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo doorOpenAngleField = doorType.GetField("DoorOpenAngle", BindingFlags.Instance | BindingFlags.Public);

        if (openRotField != null && doorOpenAngleField != null)
        {
            float openAngle = (float)doorOpenAngleField.GetValue(door);
            Vector3 enemyDir = openerPosition - door.transform.position;
            float dot = Vector3.Dot(door.transform.right, enemyDir);
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
        return true;
    }
}
