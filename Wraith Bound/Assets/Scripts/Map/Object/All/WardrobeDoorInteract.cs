using UnityEngine;

public class WardrobeDoorInteract : MonoBehaviour, IInteractable
{
    public float openAngle = 90f;
    public float interactDistance = 5f;
    private bool isOpen;

    public void Interact(GameObject interactor)
    {
        // DoorClick, if present, owns this door's animation and sound.
        DoorClick door = GetComponent<DoorClick>();
        if (door != null)
        {
            if (door.isActiveAndEnabled) door.Interact(interactor);
            return;
        }
        isOpen = !isOpen;
        transform.Rotate(0, isOpen ? openAngle : -openAngle, 0);
    }

    public string GetInteractPrompt()
    {
        return isOpen ? "Close" : "Open";
    }
}
