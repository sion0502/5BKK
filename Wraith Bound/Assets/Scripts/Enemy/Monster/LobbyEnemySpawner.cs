using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class LobbyEnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private GameObject ghostPrefab;

    [Header("Spawn Count")]
    [SerializeField] private int monsterCount = 2;
    [SerializeField] private int ghostCount = 2;

    [Header("Lobby Search")]
    [SerializeField] private string lobbyNameKeyword = "(Lobby)";
    [SerializeField] private float waitTime = 2.0f;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 4f;
    [SerializeField] private float navMeshSampleRadius = 8f;
    [SerializeField] private float heightOffset = 2f;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(waitTime);

        Transform lobby = FindLobbyChild();
        if (lobby == null)
        {
            Debug.LogError($"이름에 {lobbyNameKeyword} 포함된 로비를 못 찾음");
            yield break;
        }

        Vector3 lobbyCenter = GetLobbyCenter(lobby);
        Debug.Log($"로비 찾음: {lobby.name} / 중심: {lobbyCenter}");

        SpawnEnemies(monsterPrefab, monsterCount, lobbyCenter);
        SpawnEnemies(ghostPrefab, ghostCount, lobbyCenter);
    }

    private Transform FindLobbyChild()
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains(lobbyNameKeyword))
                return child;
        }

        return null;
    }

    private Vector3 GetLobbyCenter(Transform lobby)
    {
        Renderer r = lobby.GetComponentInChildren<Renderer>();
        if (r != null)
            return r.bounds.center;

        Collider c = lobby.GetComponentInChildren<Collider>();
        if (c != null)
            return c.bounds.center;

        return lobby.position;
    }

    private void SpawnEnemies(GameObject prefab, int count, Vector3 center)
    {
        if (prefab == null)
        {
            Debug.LogWarning("프리팹이 비어있음");
            return;
        }

        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            bool spawned = false;

            for (int tryCount = 0; tryCount < 20; tryCount++)
            {
                Vector2 circle = Random.insideUnitCircle * spawnRadius;
                Vector3 candidate = new Vector3(
                    center.x + circle.x,
                    center.y + heightOffset,
                    center.z + circle.y
                );

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    GameObject obj = Instantiate(prefab, hit.position, Quaternion.identity);
                    Debug.Log($"생성 성공: {obj.name} / 위치: {hit.position}");
                    spawned = true;
                    break;
                }
            }

            if (!spawned)
            {
                Debug.LogError($"NavMesh 위 스폰 실패: {prefab.name}");
            }
        }
    }
}
