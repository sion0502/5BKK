using UnityEngine;
using UnityEngine.AI;

public class DoorController : MonoBehaviour
{
    public GameObject brokenDoor;

    NavMeshObstacle obstacle;
    bool isBroken = false;

    void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
    }

    public bool IsBroken()
    {
        return isBroken;
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        Break();
    }

    void Break()
    {
        isBroken = true;

        if (brokenDoor != null)
        {
            // ⭐ 바닥 위로 위치만 정확히 맞춤 (핵심)
            brokenDoor.transform.position = transform.position + Vector3.up * 0.6f;
            brokenDoor.transform.rotation = transform.rotation;

            brokenDoor.SetActive(true);
        }

        gameObject.SetActive(false);

        if (obstacle != null)
            obstacle.enabled = false;
    }
}