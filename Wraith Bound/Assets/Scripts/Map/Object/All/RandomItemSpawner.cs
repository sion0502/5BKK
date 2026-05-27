using UnityEngine;
using System.Collections.Generic;

public class RandomItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnItem
    {
        public GameObject prefab;

        [Range(0f, 100f)]
        public float chance = 10f;
    }

    [Header("Spawn Settings")]
    [Range(0, 4)]
    public int maxSpawnCount = 2;

    [Range(0f, 100f)]
    public float noSpawnChance = 70f;

    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    [Header("Items")]
    public SpawnItem[] items;

    private void Start()
    {
        TrySpawn();
    }

    private void TrySpawn()
    {
        // 아무것도 안 나올 확률
        if (Random.Range(0f, 100f) <= noSpawnChance)
            return;

        if (spawnPoints.Length == 0 || items.Length == 0)
            return;

        // 생성 개수
        int spawnCount = Random.Range(1, maxSpawnCount + 1);

        // 중복 없는 포인트 선택용
        List<Transform> availablePoints =
            new List<Transform>(spawnPoints);

        for (int i = 0; i < spawnCount; i++)
        {
            if (availablePoints.Count == 0)
                return;

            // 랜덤 포인트 선택
            int pointIndex =
                Random.Range(0, availablePoints.Count);

            Transform point =
                availablePoints[pointIndex];

            availablePoints.RemoveAt(pointIndex);

            // 아이템 선택
            GameObject prefab =
                GetRandomItem();

            if (prefab != null)
            {
                SpawnItemAtPoint(prefab, point);
            }
        }
    }

    private GameObject GetRandomItem()
    {
        float total = 0f;

        foreach (var item in items)
        {
            total += item.chance;
        }

        float rand = Random.Range(0f, total);

        float current = 0f;

        foreach (var item in items)
        {
            current += item.chance;

            if (rand <= current)
            {
                return item.prefab;
            }
        }

        return null;
    }

    private void SpawnItemAtPoint(GameObject prefab, Transform point)
    {
        // Y축 랜덤 회전
        float randomY = Random.Range(0f, 360f);

        Quaternion randomRot =
            Quaternion.Euler(0f, randomY, 0f);

        GameObject obj = Instantiate(
            prefab,
            point.position,
            randomRot,
            point
        );

        Collider col = obj.GetComponent<Collider>();

        if (col != null)
        {
            Vector3 pos = obj.transform.position;

            pos.y += col.bounds.extents.y;

            obj.transform.position = pos;
        }
    }
}