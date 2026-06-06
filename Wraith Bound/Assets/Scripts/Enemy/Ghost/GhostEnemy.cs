using UnityEngine;

public class GhostEnemy : EnemyBase
{
    Collider[] myColliders;

    void Awake()
    {
        myColliders =
            GetComponentsInChildren<Collider>();
    }

    protected override void Start()
    {
        base.Start();

        GameObject[] doors =
            GameObject.FindGameObjectsWithTag(
                "Door");

        foreach (GameObject door in doors)
        {
            Collider[] doorCols =
                door.GetComponentsInChildren<
                    Collider>();

            foreach (Collider myCol in myColliders)
            {
                foreach (Collider doorCol in doorCols)
                {
                    Physics.IgnoreCollision(
                        myCol,
                        doorCol,
                        true);
                }
            }
        }
    }
}