using UnityEngine;

/// <summary>
/// 맵에 떨어진 스토리 쪽지 한 장 — E로 읽기(StoryDocumentReader) → 수집 기록(StoryNoteProgress) → 닫으면 사라짐.
/// 읽은 쪽지는 허브 게시판에서 다시 볼 수 있음. 모양·빛은 StoryNoteSpawner가 만들 때 붙임.
/// </summary>
public class StoryNotePickup : MonoBehaviour, IInteractable
{
    private int noteId = -1;
    private bool reading;

    public void Init(int id)
    {
        noteId = id;
    }

    public string GetInteractPrompt()
    {
        return reading || noteId < 0 ? null : "[E] 쪽지 읽기";
    }

    public void Interact(GameObject interactor)
    {
        if (reading || noteId < 0 || StoryDocumentReader.IsOpen)
        {
            return;
        }

        StoryNoteDef def = StoryNoteCatalog.All[noteId];
        reading = true;
        StoryNoteProgress.Collect(noteId);
        StoryDocumentReader.Open(def.Title, def.Body, OnClosed);
    }

    private void OnClosed()
    {
        ScreenMessageUI.Show("기록 " + StoryNoteProgress.Count + "/" + StoryNoteCatalog.Count + " — 귀문의 방에서 다시 읽을 수 있다", 2.6f);
        Destroy(gameObject);
    }
}
