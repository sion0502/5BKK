using UnityEngine;

/// <summary>
/// 긴 복도 끝 비상구 문. PrologueDirector가 Arm()한 뒤(숨어서 위기를 넘긴 뒤)에 E로 열어 보면 마지막 컷씬 시작 — 로딩 없이 같은 씬.
/// Arm 전(또는 추격 중)이면 그냥 잠긴 문 안내만.
/// </summary>
public class PrologueFinaleDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private PrologueFinaleCutscene finale;
    [SerializeField] private string interactPrompt = "[E] 문 열기";
    [SerializeField] private string lockedMessage = "…잠겨 있어.";

    private bool armed;

    public void Arm(bool value)
    {
        armed = value;
    }

    public void Interact(GameObject interactor)
    {
        if (finale != null && finale.IsRunning)
        {
            return;
        }

        if (!armed || finale == null)
        {
            ScreenMessageUI.Show(lockedMessage, 1.6f);
            return;
        }

        armed = false;
        finale.Begin();
    }

    public string GetInteractPrompt()
    {
        return finale != null && finale.IsRunning ? string.Empty : interactPrompt;
    }
}
