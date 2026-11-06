using UnityEngine;

/// <summary>
/// 열리지 않는 문 (프롤로그에서 쾅 닫힌 정문 등). E로 조사하면 화면 안내 문구만 띄움.
/// 문짝 콜라이더가 있는 오브젝트(또는 그 부모)에 붙입니다. 덜컹 소리 슬롯은 비워 둠(사운드는 팀이 나중에 결정).
/// </summary>
public class SealedDoorInteract : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactPrompt = "[E] 문 열기";
    [SerializeField] private string[] messages = { "…열리지 않는다.", "꿈쩍도 하지 않는다." };
    [SerializeField] private float messageDuration = 1.8f;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rattleClip;

    private int tries;

    public void Interact(GameObject interactor)
    {
        if (messages != null && messages.Length > 0)
        {
            ScreenMessageUI.Show(messages[Mathf.Min(tries, messages.Length - 1)], messageDuration);
        }

        tries++;
        if (audioSource != null && rattleClip != null)
        {
            audioSource.PlayOneShot(rattleClip);
        }
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }
}
