using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메뉴 버튼에 마우스를 올리면 EscHover 소리 (일시정지(ESC) 메뉴 버튼에 붙임).
/// 일시정지 중(timeScale 0)에도 들리도록 GameSfx가 unscaled 시간으로 재생합니다.
/// </summary>
public class MenuHoverSound : MonoBehaviour, IPointerEnterHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || button.IsInteractable())
        {
            GameSfx.PlayEscHover();
        }
    }
}
