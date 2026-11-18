using UnityEngine;

/// <summary>허브 게시판의 쪽지 한 장 — E로 다시 읽기.</summary>
public class HubNoteArchiveSlot : MonoBehaviour, IInteractable
{
    public const int FirstNoteId = -1; // 깨어난 자리의 기록자 쪽지(HubNoteText)

    private int noteId;

    public void Init(int id)
    {
        noteId = id;
    }

    public string GetInteractPrompt()
    {
        return noteId == FirstNoteId ? "[E] 기록자의 쪽지 읽기" : "[E] 기록 " + (noteId + 1) + " 읽기";
    }

    public void Interact(GameObject interactor)
    {
        if (StoryDocumentReader.IsOpen)
        {
            return;
        }

        if (noteId == FirstNoteId)
        {
            StoryDocumentReader.Open(HubNoteText.Title, HubNoteText.Body);
            return;
        }

        StoryNoteDef def = StoryNoteCatalog.All[noteId];
        StoryDocumentReader.Open(def.Title, def.Body);
    }
}
