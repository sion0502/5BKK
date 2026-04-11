using UnityEngine;

public class FallingDoor : MonoBehaviour
{
    public GameObject brokenDoorPrefab;

    private bool isBroken = false;

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        // 🔥 Lobby로 변경
        if (collision.gameObject.CompareTag("Lobby"))
        {
            isBroken = true;

            Instantiate(brokenDoorPrefab, transform.position, transform.rotation);

            Destroy(gameObject);
        }
    }
}