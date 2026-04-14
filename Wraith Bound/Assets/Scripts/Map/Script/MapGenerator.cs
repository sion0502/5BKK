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
    // 1. 일단 전체를 규칙대로 채운다
    for (int x = 0; x < size; x++)
    {
        for (int y = 0; y < size; y++)
        {
            grid[x, y] = GetRequiredDir(x, y);
        }
    }

    // 2. 마지막에 입구/출구 앞만 "강제로" 덮어쓴다 (이게 최종값이어야 함)
    grid[2, 0] = Dir.Up | Dir.Down | Dir.Left | Dir.Right; 
    grid[2, 4] = Dir.Up | Dir.Down | Dir.Left | Dir.Right;
    
    // 3. 제대로 들어갔나 확인
    Debug.Log($"최종 확인 grid[2,1] : {grid[2,1]}");
}

    // ✅ 위치 기반 방향 결정
Dir GetRequiredDir(int x, int y)
{
    // 코너 (2방향)
    if (x == 0 && y == 0) return Dir.Up | Dir.Right;
    if (x == 0 && y == size - 1) return Dir.Down | Dir.Right;
    if (x == size - 1 && y == 0) return Dir.Up | Dir.Left;
    if (x == size - 1 && y == size - 1) return Dir.Down | Dir.Left;

    // 테두리 (3방향)
    if (x == 0) return Dir.Up | Dir.Down | Dir.Right;
    if (x == size - 1) return Dir.Up | Dir.Down | Dir.Left;
    if (y == 0) return Dir.Up | Dir.Left | Dir.Right;
    if (y == size - 1) return Dir.Down | Dir.Left | Dir.Right;

    // 내부 (4방향)
    return Dir.Up | Dir.Down | Dir.Left | Dir.Right;
}




    // ✅ 타일 선택
GameObject GetMatchingTile(Dir need, out Quaternion rotOut)
{
    List<(GameObject, Quaternion)> candidates = new();

    int target = (int)need;
    if (target == -1) target = 15;

    foreach (var go in tileSet.tiles)
    {
        Tile t = go.GetComponent<Tile>();
        if (t == null) continue;

        for (int r = 0; r < 4; r++)
        {
            Dir rotated = RotateDir(t.openings, r);
            int val = (int)rotated;
            if (val == -1) val = 15;

            if (val == target)
            {
                Quaternion rot = Quaternion.Euler(0, r * 90f, 0);
                candidates.Add((go, rot));
            }
        }
    }

    if (candidates.Count == 0)
    {
        Debug.LogError($"매칭 실패: {need}");
        rotOut = Quaternion.identity;
        return null;
    }

    var pick = candidates[Random.Range(0, candidates.Count)];
    rotOut = pick.Item2;
    return pick.Item1;
}


// 비트 개수를 세어주는 보조 함수 (동일 위치에 추가)
int GetBitCount(int value)
{
    // -1(Everything) 처리: 우리에겐 4방향과 같음
    if (value == -1) return 4; 
    
    int count = 0;
    for (int i = 0; i < 4; i++) // Up, Down, Left, Right 4개만 체크
    {
        if ((value & (1 << i)) != 0) count++;
    }
    return count;
}

    // ✅ 생성
void Build()
{
    for (int x = 0; x < size; x++)
    {
        for (int y = 0; y < size; y++)
        {
            Dir need = grid[x, y];

            Quaternion rot;
            GameObject prefab = GetMatchingTile(need, out rot);
            if (prefab == null) continue;

            Vector3 pos = new Vector3(x * tileSize, 0, y * tileSize);
            Instantiate(prefab, pos, rot, transform);
        }
    }
}


Dir RotateDir(Dir d, int rot)
{
    int val = (int)d;
    if (val == -1) val = 15;

    for (int i = 0; i < rot; i++)
    {
        int newVal = 0;

        if ((val & (int)Dir.Up) != 0) newVal |= (int)Dir.Right;
        if ((val & (int)Dir.Right) != 0) newVal |= (int)Dir.Down;
        if ((val & (int)Dir.Down) != 0) newVal |= (int)Dir.Left;
        if ((val & (int)Dir.Left) != 0) newVal |= (int)Dir.Up;

        val = newVal;
    }

    return (Dir)val;
}



}
