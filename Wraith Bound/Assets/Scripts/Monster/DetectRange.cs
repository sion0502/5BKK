using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class DetectRange : MonoBehaviour
{
    public EnemyController enemy;

    void Start()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = enemy.data.detectRange;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (enemy.currentState == State.ATTACK) return;

        if (enemy.IsInCorridor(other.transform))
        {
            enemy.OnLosePlayer();
            return;
        }

        enemy.OnDetectPlayer(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            enemy.OnLosePlayer();
        }
    }
}