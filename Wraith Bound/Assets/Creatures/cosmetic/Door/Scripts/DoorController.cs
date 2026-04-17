using UnityEngine;

public class DoorController : MonoBehaviour
{
    [SerializeField] private int hp = 3;
    [SerializeField] private GameObject brokenDoor;

    private Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
    }

    void OnCollisionEnter(Collision other)
    {
        // ⭐ 플레이어는 무조건 통과
        if (other.gameObject.CompareTag("Player"))
        {
            Physics.IgnoreCollision(col, other.collider);
        }
    }

    public bool IsBroken()
    {
        return hp <= 0;
    }

    public void TakeDamage(int damage)
    {
        if (hp <= 0) return;

        hp -= damage;

        if (hp <= 0)
        {
            BreakDoor();
        }
    }

    void BreakDoor()
    {
        gameObject.SetActive(false);

        if (brokenDoor != null)
            brokenDoor.SetActive(true);
    }
}