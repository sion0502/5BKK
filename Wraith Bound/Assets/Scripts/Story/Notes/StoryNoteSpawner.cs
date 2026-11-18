using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시연 챕터에 스토리 쪽지를 무작위로 배치 (2026-11-19 사용자 결정): 매 판 **아직 안 읽은 쪽지 전부**를 후보 자리(spots) 중 무작위로 골라 놓음.
/// 이미 읽은 쪽지는 안 나옴(허브 게시판에서 다시 읽음). 후보 자리는 에디터가 걸어서 닿는 바닥의 벽 가까운 곳에서 서로 떨어지게 뽑아 둠
/// (Editor/FixedDemoNotes.cs). 쪽지 = 바닥에 놓인 바랜 종이 + 아주 약한 빛(어둠 속에서 찾을 수 있게).
/// </summary>
public class StoryNoteSpawner : MonoBehaviour
{
    [SerializeField] private Vector3[] spots = { };
    [SerializeField] private Vector2 paperSize = new Vector2(0.42f, 0.56f); // 플레이어 1.3배 맵 — 허브 쪽지(0.32×0.44)의 1.3배
    [SerializeField] private float glowIntensity = 0.6f;
    [SerializeField] private float glowRange = 2.2f;

    private void Start()
    {
        var notes = new List<int>();
        for (int i = 0; i < StoryNoteCatalog.Count; i++)
        {
            if (!StoryNoteProgress.Has(i))
            {
                notes.Add(i);
            }
        }

        var order = new List<int>();
        for (int i = 0; i < spots.Length; i++)
        {
            order.Add(i);
        }

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        int placed = 0;
        for (int k = 0; k < notes.Count && k < order.Count; k++)
        {
            Spawn(notes[k], spots[order[k]]);
            placed++;
        }

        Debug.Log("[Story] 쪽지 " + placed + "장 배치 (안 읽은 " + notes.Count + "장, 후보 자리 " + spots.Length + "곳)");
    }

    private void Spawn(int id, Vector3 pos)
    {
        var go = new GameObject("StoryNote_" + (id + 1));
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(pos + Vector3.up * 0.012f, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));

        StoryNoteVisual.CreatePaper(go.transform, paperSize);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(paperSize.x * 2.6f, paperSize.y * 2.2f, 0.4f); // 바닥에 납작해 조준하기 어려워 넉넉히
        box.isTrigger = true;

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(go.transform, false);
        glow.transform.position = pos + Vector3.up * 0.5f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.86f, 0.6f);
        glow.intensity = glowIntensity;
        glow.range = glowRange;
        glow.shadows = LightShadows.None;

        go.AddComponent<StoryNotePickup>().Init(id);
    }
}
