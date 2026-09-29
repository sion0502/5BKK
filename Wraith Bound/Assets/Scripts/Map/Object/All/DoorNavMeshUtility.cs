using UnityEngine;
using UnityEngine.AI;

public static class DoorNavMeshUtility
{
    public static void EnsureCarveObstacle(Transform doorRoot)
    {
        if (doorRoot == null)
            return;

        NavMeshObstacle[] existing = doorRoot.GetComponentsInChildren<NavMeshObstacle>(true);
        if (existing.Length > 0)
            return;

        BoxCollider box = FindDoorBlockerCollider(doorRoot);
        if (box == null)
            return;

        NavMeshObstacle obstacle = doorRoot.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.center = box.center;
        obstacle.size = box.size;
    }

    public static void SetNavMeshBlocked(Transform doorRoot, bool blocked)
    {
        if (doorRoot == null)
            return;

        NavMeshObstacle[] obstacles = doorRoot.GetComponentsInChildren<NavMeshObstacle>(true);
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i] != null)
                obstacles[i].enabled = blocked;
        }
    }

    private static BoxCollider FindDoorBlockerCollider(Transform doorRoot)
    {
        BoxCollider box = doorRoot.GetComponent<BoxCollider>();
        if (box != null && !box.isTrigger)
            return box;

        BoxCollider[] boxes = doorRoot.GetComponentsInChildren<BoxCollider>(true);
        BoxCollider best = null;
        float bestVolume = 0f;

        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].isTrigger)
                continue;

            Vector3 size = boxes[i].size;
            float volume = size.x * size.y * size.z;
            if (volume > bestVolume)
            {
                bestVolume = volume;
                best = boxes[i];
            }
        }

        return best;
    }
}
