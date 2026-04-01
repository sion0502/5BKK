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
    // ✅ 코너 (2방향)
    if ((x == 0 && y == 0)) return Dir.Up | Dir.Right;
    if ((x == 0 && y == size - 1)) return Dir.Down | Dir.Right;
    if ((x == size - 1 && y == 0)) return Dir.Up | Dir.Left;
    if ((x == size - 1 && y == size - 1)) return Dir.Down | Dir.Left;

    // ✅ 테두리 (3방향)
    if (x == 0) return Dir.Up | Dir.Down | Dir.Right;
    if (x == size - 1) return Dir.Up | Dir.Down | Dir.Left;
    if (y == 0) return Dir.Up | Dir.Left | Dir.Right;
    if (y == size - 1) return Dir.Down | Dir.Left | Dir.Right;

    // ✅ 내부 (4방향)
    return Dir.Up | Dir.Down | Dir.Left | Dir.Right;
}


    // ✅ 타일 선택
GameObject GetMatchingTile(Dir need)
{
    List<GameObject> candidates = new List<GameObject>();

    // 찾는 값(need)을 숫자로 변환 (Everything -1이면 15로 강제 변환)
    int targetVal = (int)need;
    if (targetVal == -1) targetVal = 15;

    foreach (var go in tileSet.tiles)
    {
        Tile t = go.GetComponent<Tile>();
        if (t == null) continue;

        // 타일이 가진 값 (-1이면 15로 변환)
        int tileVal = (int)t.openings;
        if (tileVal == -1) tileVal = 15;

        // 🛡️ [핵심] 오직 숫자가 완벽하게 일치할 때만 후보에 넣음
        if (tileVal == targetVal)
        {
            candidates.Add(go);
        }
    }

    if (candidates.Count == 0)
    {
        Debug.LogError($"매칭 실패: {need} ({targetVal})");
        return null;
    }

    // 후보가 여러 개라면 랜덤, 하나라면 그 타일이 리턴됨
    return candidates[Random.Range(0, candidates.Count)];
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
            
            // ✅ (2,1)과 (2,3)에서 실제로 어떤 값이 들어오는지 확인
            if ((x == 2 && y == 1) || (x == 2 && y == 3))
            {
                Debug.Log($"[생성 직전 체크] 좌표 ({x}, {y})의 데이터: {need} ({(int)need})");
            }

            GameObject prefab = GetMatchingTile(need);
            if (prefab == null) continue;

            Vector3 pos = new Vector3(x * tileSize, 0, y * tileSize);
            GameObject instance = Instantiate(prefab, pos, Quaternion.identity, transform);
            
            // ✅ 생성된 오브젝트 이름에 좌표를 붙여서 씬에서 확인하기 쉽게 만듭니다.
            instance.name = $"Tile_{x}_{y}_{need}";

            if ((x == 2 && y == 1))
{
    // (2,1) 자리에 실제로 '어떤 이름'의 프리팹이 소환됐는지 출력
    Debug.Log($"[결과] (2,1) 자리에 생성된 프리팹 이름: {prefab.name}");
}
        }
    }
}
}
