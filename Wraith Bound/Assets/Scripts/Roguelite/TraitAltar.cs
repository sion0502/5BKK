using UnityEngine;

/// <summary>허브의 기 제단 — 조준해서 E로 기 고르기 화면(TraitAltarUI)을 엶.</summary>
public class TraitAltar : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactPrompt = "[E] 기 고르기";

    public void Interact(GameObject interactor)
    {
        TraitAltarUI.Open();
    }

    public string GetInteractPrompt()
    {
        return TraitAltarUI.IsOpen ? string.Empty : interactPrompt;
    }
}
