using UnityEngine;
using UnityEngine.AI;

public class DoorController : MonoBehaviour
{
    public GameObject brokenDoor;

    NavMeshObstacle obstacle;
    Collider doorCollider;

    bool isBroken;

    void Awake()
    {
        obstacle =
            GetComponent<NavMeshObstacle>();

        doorCollider =
            GetComponent<Collider>();
    }

    void Start()
    {
        IgnorePlayerCollision();
    }

    void IgnorePlayerCollision()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag(
                "Player");

        if (player == null)
            return;

        Collider[] playerCols =
            player.GetComponentsInChildren<Collider>();

        foreach (Collider col in playerCols)
        {
            Physics.IgnoreCollision(
                col,
                doorCollider);
        }
    }

    public bool IsBroken()
    {
        return isBroken;
    }

    public void OpenPath()
    {
        if (isBroken)
            return;

        if (obstacle != null)
        {
            obstacle.enabled = false;
        }
    }

    public void ClosePath()
    {
        if (isBroken)
            return;

        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
    }

    public void TakeDamage(
        int damage)
    {
        if (isBroken)
            return;

        Break();
    }

    void Break()
    {
        isBroken = true;

        if (brokenDoor != null)
        {
            brokenDoor.transform.position =
                transform.position;

            brokenDoor.transform.rotation =
                transform.rotation;

            brokenDoor.SetActive(true);

            Rigidbody rb =
                brokenDoor.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity =
                    Vector3.zero;

                rb.angularVelocity =
                    Vector3.zero;
            }
        }

        if (obstacle != null)
        {
            obstacle.enabled = false;
        }

        gameObject.SetActive(false);
    }
}