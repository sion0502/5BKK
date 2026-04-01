using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public int size = 5;
    public float tileSize = 10f;
    public TileSet tileSet;

    Dir[,] grid;

    Vector2Int start = new Vector2Int(2, 0);
    Vector2Int end = new Vector2Int(2, 4);

    void Start()
    {
        grid = new Dir[size, size];

        GenerateGrid();
        Build();
    }

    // ✅ 규칙 기반 생성 (핵심)
    void GenerateGrid()
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                grid[x, y] = GetRequiredDir(x, y);
            }
        }

        // ✅ 입구 / 출구 앞은 무조건 4방향
        grid[2, 1] = Dir.Up | Dir.Down | Dir.Left | Dir.Right;
        grid[2, 3] = Dir.Up | Dir.Down | Dir.Left | Dir.Right;

        // ✅ 입구 / 출구
        grid[start.x, start.y] = Dir.Up;
        grid[end.x, end.y] = Dir.Down;
    }

    // ✅ 위치 기반 방향 결정
    Dir GetRequiredDir(int x, int y)
    {
        bool up = y < size - 1;
        bool down = y > 0;
        bool left = x > 0;
        bool right = x < size - 1;

        Dir result = Dir.None;

        if (up) result |= Dir.Up;
        if (down) result |= Dir.Down;
        if (left) result |= Dir.Left;
        if (right) result |= Dir.Right;

        return result;
    }

    // ✅ 타일 선택
    GameObject GetMatchingTile(Dir need)
    {
        List<GameObject> candidates = new List<GameObject>();

        foreach (var go in tileSet.tiles)
        {
            Tile t = go.GetComponent<Tile>();

            if (t != null && ((t.openings & need) == need))
                candidates.Add(go);
        }

        if (candidates.Count == 0)
        {
            Debug.LogError("매칭되는 타일 없음: " + need);
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ✅ 생성
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
}
