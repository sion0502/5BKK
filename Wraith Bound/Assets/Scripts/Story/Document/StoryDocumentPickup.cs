using System;
using UnityEngine;

/// <summary>
/// 맵에 놓인 일지·쪽지. 조준하면 "[E] 일지 읽기" → StoryDocumentReader로 읽고, 닫으면 Read 이벤트 후 사라짐(챙긴 것으로).
/// 스토리 아이템 수집(허브 전시)은 나중에 documentId로 연결.
/// </summary>
public class StoryDocumentPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string documentId = "journal_00";
    [SerializeField] private string prompt = "[E] 일지 읽기";
    [SerializeField] private string title = "기록 #0";
    [SerializeField, TextArea(5, 14)] private string body;
    [SerializeField] private bool removeAfterReading = true;

    public event Action Read;

    public string DocumentId => documentId;

    public void Interact(GameObject interactor)
    {
        if (StoryDocumentReader.IsOpen)
        {
            return;
        }

        StoryDocumentReader.Open(title, body, OnClosed);
    }

    public string GetInteractPrompt()
    {
        return prompt;
    }

    private void OnClosed()
    {
        Read?.Invoke();
        if (removeAfterReading)
        {
            gameObject.SetActive(false);
        }
    }
}
