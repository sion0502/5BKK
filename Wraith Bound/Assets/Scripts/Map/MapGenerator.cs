using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

    public int size = 5;
    public float tileSize = 10f;
    public TileSet tileSet;

    Dir[,] grid;

    Vector2Int start = new Vector2Int(2, 0);
    Vector2Int end = new Vector2Int(2, 4);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        grid = new Dir[size, size];
        
        CreateMainPath();
        AddExtraConnections(0.3f);
        Build();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void CreateMainPath()
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        DFS(start, visited);
    }

    bool DFS(Vector2Int cur, HashSet<Vector2Int> visited)
    {
        if (cur == end)
            return true;

        visited.Add(cur);

        List<Vector2Int> dirs = new List<Vector2Int>
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        Shuffle(dirs);

        foreach (var d in dirs)
        {
            Vector2Int next = cur + d;

            if (!InRange(next) || visited.Contains(next))
                continue;

            if (DFS(next, visited))
            {
                Connect(cur, next);
                return true;
            }
        }

        return false;
    }

    void Connect(Vector2Int a, Vector2Int b)
    {
        Vector2Int diff = b - a;

        if (diff == Vector2Int.up)
        {
            grid[a.x, a.y] |= Dir.Up;
            grid[b.x, b.y] |= Dir.Down;
        }
        else if (diff == Vector2Int.down)
        {
            grid[a.x, a.y] |= Dir.Down;
            grid[b.x, b.y] |= Dir.Up;
        }
        else if (diff == Vector2Int.left)
        {
            grid[a.x, a.y] |= Dir.Left;
            grid[b.x, b.y] |= Dir.Right;
        }
        else if (diff == Vector2Int.right)
        {
            grid[a.x, a.y] |= Dir.Right;
            grid[b.x, b.y] |= Dir.Left;
        }
    }


    void AddExtraConnections(float chance)
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int cur = new Vector2Int(x, y);

                foreach (var dir in new[] { Vector2Int.right, Vector2Int.up })
                {
                    Vector2Int next = cur + dir;

                    if (!InRange(next)) continue;

                    if (Random.value < chance)
                        Connect(cur, next);
                }
            }
        }
    }


GameObject GetMatchingTile(Dir need)
{
    List<GameObject> candidates = new List<GameObject>();

    foreach (var go in tileSet.tiles)
    {
        Tile t = go.GetComponent<Tile>();

        if (t != null && t.openings == need)
            candidates.Add(go);
    }

    if (candidates.Count == 0)
    {
        Debug.LogError("매칭되는 타일 없음: " + need);
        return null;
    }

    return candidates[Random.Range(0, candidates.Count)];
}



void Build()
{
    for (int x = 0; x < size; x++)
    {
        for (int y = 0; y < size; y++)
        {
            Dir need = grid[x, y];

            GameObject prefab = GetMatchingTile(need);

            if (prefab == null) continue;

            Vector3 pos = new Vector3(x * tileSize, 0, y * tileSize);

            Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }
}




    bool InRange(Vector2Int p)
    {
        return p.x >= 0 && p.x < size && p.y >= 0 && p.y < size;
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            var tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}
