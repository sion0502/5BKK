using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class LobbyEnemySpawner : MonoBehaviour
{
    [Header("Environment")]
    [SerializeField] private NavMeshSurface surface;

    [Header("Monster Prefabs")]
    [SerializeField] private GameObject[] monsterPrefabs;

    [Header("Ghost Prefabs")]
    [SerializeField] private GameObject[] ghostPrefabs;

    [Header("Lobby Search")]
    [SerializeField] private string lobbyNameKeyword = "(Lobby)";

    [Header("NavMesh Build")]
    [SerializeField] private float waitBeforeBuild = 0.5f;
    [SerializeField] private float waitAfterBuild = 0.2f;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRadius = 6f;
    [SerializeField] private float navMeshSampleRadius = 12f;
    [SerializeField] private float heightOffset = 2f;

    private bool initialized = false;

    private void Awake()
    {
        if (surface == null)
            surface = GetComponent<NavMeshSurface>();
    }

    private IEnumerator Start()
    {
        if (initialized)
            yield break;

        initialized = true;

        Debug.Log("LobbyEnemySpawner Start");

        if (surface == null)
        {
            Debug.LogError("NavMeshSurface�� �����ϴ�.");
            yield break;
        }

        yield return new WaitForSeconds(waitBeforeBuild);

        surface.BuildNavMesh();

        Debug.Log("NavMesh Build Complete");

        yield return new WaitForSeconds(waitAfterBuild);

        List<Transform> lobbies = FindAllLobbyRoots();

        Debug.Log($"ã�� �κ� �� : {lobbies.Count}");

        foreach (Transform lobby in lobbies)
        {
            Debug.Log($"�κ� �߰� : {lobby.name}");
        }

        if (lobbies.Count == 0)
        {
            Debug.LogError("Lobby�� ã�� ���߽��ϴ�.");
            yield break;
        }

        Debug.Log($"Monster Prefab ���� : {monsterPrefabs.Length}");
        Debug.Log($"Ghost Prefab ���� : {ghostPrefabs.Length}");

        SpawnUniqueLobby(monsterPrefabs, lobbies, "Monster");
        SpawnUniqueLobby(ghostPrefabs, lobbies, "Ghost");

        Debug.Log("Spawn Complete");
    }

    private List<Transform> FindAllLobbyRoots()
    {
        List<Transform> list = new List<Transform>();

        foreach (Transform t in transform)
        {
            if (t.name.Contains(lobbyNameKeyword))
            {
                list.Add(t);
            }
        }

        return list;
    }

    private Vector3 GetLobbyCenter(Transform lobby)
    {
        Collider c = lobby.GetComponentInChildren<Collider>();

        if (c != null)
            return c.bounds.center;

        Renderer r = lobby.GetComponentInChildren<Renderer>();

        if (r != null)
            return r.bounds.center;

        return lobby.position;
    }

    private void SpawnUniqueLobby(GameObject[] prefabs, List<Transform> lobbies, string label)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"{label} �������� �������");
            return;
        }

        List<Transform> availableLobbies = new List<Transform>(lobbies);

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null)
                continue;

            if (availableLobbies.Count == 0)
            {
                Debug.LogWarning($"{label} : ���� ������ �κ� ����");
                break;
            }

            int lobbyIndex = Random.Range(0, availableLobbies.Count);

            Transform selectedLobby = availableLobbies[lobbyIndex];

            availableLobbies.RemoveAt(lobbyIndex);

            Vector3 center = GetLobbyCenter(selectedLobby);

            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;

            Vector3 origin = new Vector3(
                center.x + randomPos.x,
                center.y + heightOffset,
                center.z + randomPos.y
            );

            Debug.Log(
                $"[{label}] {prefab.name} ����\n" +
                $"�κ� : {selectedLobby.name}\n" +
                $"Center : {center}\n" +
                $"Origin : {origin}"
            );

            if (NavMesh.SamplePosition(
                origin,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
            {
                Debug.Log(
                    $"�ڡڡڡ� SPAWN ���� �ڡڡڡ�\n" +
                    $"Prefab : {prefab.name}\n" +
                    $"Lobby : {selectedLobby.name}\n" +
                    $"Spawn Position : {hit.position}\n" +
                    $"X = {hit.position.x:F2}\n" +
                    $"Y = {hit.position.y:F2}\n" +
                    $"Z = {hit.position.z:F2}"
                );

                Instantiate(
                    prefab,
                    hit.position,
                    Quaternion.identity
                );
            }
            else
            {
                Debug.LogError(
                    $"XXXX SPAWN ���� XXXX\n" +
                    $"Prefab : {prefab.name}\n" +
                    $"Lobby : {selectedLobby.name}\n" +
                    $"Origin : {origin}\n" +
                    $"Center : {center}"
                );
            }
        }
    }
}