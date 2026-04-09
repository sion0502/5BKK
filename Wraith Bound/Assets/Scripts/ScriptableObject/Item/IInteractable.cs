using System;
using UnityEngine;

public interface IInteractable
{
    // interactor: 상호작용을 시도한 주체 (이 경우 Player)
    void Interact(GameObject interactor);

    String GetInteractPrompt();
}
