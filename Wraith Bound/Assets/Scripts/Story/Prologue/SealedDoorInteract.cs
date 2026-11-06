using UnityEngine;

/// <summary>
/// 열리지 않는 문 (프롤로그에서 쾅 닫힌 정문 등). E로 열어 보려 하면
/// 처음 한 번은 독백 자막(firstTryLines), 그 뒤로는 화면 안내 문구(messages)만 띄움.
/// 문짝 콜라이더가 있는 오브젝트(또는 그 부모)에 붙입니다. 덜컹 소리 슬롯은 비워 둠(사운드는 팀이 나중에 결정).
/// </summary>
public class SealedDoorInteract : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactPrompt = "[E] 문 열기";

    [Tooltip("처음 열어 볼 때 독백 (비우면 바로 messages)")]
    [SerializeField] private SubtitleLine[] firstTryLines = { };

    [SerializeField] private string[] messages = { "…열리지 않는다.", "꿈쩍도 하지 않는다." };
    [SerializeField] private float messageDuration = 1.8f;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rattleClip;

    private int tries;
    private bool monologuePlaying;

    public void Interact(GameObject interactor)
    {
        if (audioSource != null && rattleClip != null)
        {
            audioSource.PlayOneShot(rattleClip);
        }

        if (tries == 0 && firstTryLines != null && firstTryLines.Length > 0)
        {
            tries++;
            monologuePlaying = true;
            SubtitleUI.Play(firstTryLines, () => monologuePlaying = false);
            return;
        }

        if (monologuePlaying)
        {
            return; // 독백 중 연타 — 덜컹 소리만
        }

        if (messages != null && messages.Length > 0)
        {
            int index = firstTryLines != null && firstTryLines.Length > 0 ? tries - 1 : tries;
            ScreenMessageUI.Show(messages[Mathf.Clamp(index, 0, messages.Length - 1)], messageDuration);
        }

        tries++;
    }

    public string GetInteractPrompt()
    {
        return interactPrompt;
    }
}
