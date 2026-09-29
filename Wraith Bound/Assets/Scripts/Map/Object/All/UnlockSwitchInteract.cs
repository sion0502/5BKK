using UnityEngine;

/// <summary>
/// E키 상호작용으로 연결된 문의 잠금을 해제하는 일회용 스위치입니다.
/// UnlockSwitch 프리팹의 CodeLock / CodeLock_open / CodeLock_error 외형을 자동으로 찾습니다.
/// </summary>
public class UnlockSwitchInteract : MonoBehaviour, IInteractable
{
    [Header("Door Targets")]
    [Tooltip("이 스위치로 잠금 해제할 문들을 연결하세요.")]
    [SerializeField] private LockedDoor[] targetDoors;

    [Header("Switch Visuals")]
    [SerializeField] private GameObject inactiveVisual;
    [SerializeField] private GameObject openVisual;
    [SerializeField] private GameObject errorVisual;
    [SerializeField] private string inactiveVisualName = "CodeLock";
    [SerializeField] private string openVisualName = "CodeLock_open";
    [SerializeField] private string errorVisualName = "CodeLock_error";

    [Header("Sound")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip errorSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    [SerializeField] private Transform soundOrigin;

    [Header("Prompt")]
    [SerializeField] private string interactPrompt = "[E] Unlock";
    [SerializeField] private string usedPrompt = "";
    [SerializeField] private string errorPrompt = "[Error] Door not connected";

    private bool hasActivated;
    private bool hasError;

    private void Awake()
    {
        ResolveVisuals();
        ShowInactiveState();
    }

    public void Interact(GameObject interactor)
    {
        if (hasActivated)
        {
            return;
        }

        if (!HasValidDoorTarget())
        {
            hasError = true;
            ShowErrorState();
            PlaySound(errorSound != null ? errorSound : unlockSound);
            Debug.LogWarning($"[{name}] 잠금 해제할 LockedDoor가 연결되지 않았습니다.");
            return;
        }

        hasActivated = true;
        hasError = false;

        PlaySound(unlockSound);
        ShowOpenState();

        foreach (LockedDoor targetDoor in targetDoors)
        {
            if (targetDoor != null)
            {
                targetDoor.UnlockDoor();
            }
        }

        Debug.Log($"[{name}] 연결된 문의 잠금을 해제했습니다.");
    }

    public string GetInteractPrompt()
    {
        if (hasActivated)
        {
            return usedPrompt;
        }

        return hasError ? errorPrompt : interactPrompt;
    }

    private bool HasValidDoorTarget()
    {
        if (targetDoors == null || targetDoors.Length == 0)
        {
            return false;
        }

        foreach (LockedDoor targetDoor in targetDoors)
        {
            if (targetDoor != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveVisuals()
    {
        if (inactiveVisual == null)
        {
            inactiveVisual = FindChildByName(inactiveVisualName);
        }
        if (openVisual == null)
        {
            openVisual = FindChildByName(openVisualName);
        }
        if (errorVisual == null)
        {
            errorVisual = FindChildByName(errorVisualName);
        }
    }

    private GameObject FindChildByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void ShowInactiveState()
    {
        SetVisualState(true, false, false);
    }

    private void ShowOpenState()
    {
        SetVisualState(false, true, false);
    }

    private void ShowErrorState()
    {
        SetVisualState(false, false, true);
    }

    private void SetVisualState(bool showInactive, bool showOpen, bool showError)
    {
        if (inactiveVisual != null) inactiveVisual.SetActive(showInactive);
        if (openVisual != null) openVisual.SetActive(showOpen);
        if (errorVisual != null) errorVisual.SetActive(showError);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        Vector3 position = soundOrigin != null ? soundOrigin.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, position, soundVolume);
    }
}
