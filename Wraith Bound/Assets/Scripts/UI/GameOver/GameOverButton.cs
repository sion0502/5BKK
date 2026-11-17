using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임오버 선택지 한 개. 마우스를 올리면 선택 상태가 되어 키보드/마우스 강조가 하나로 통일된다.
/// 모양·강조 연출은 메뉴 공통 디자인(MenuUIUtility.CreateMenuButton + MenuButtonStyle.followSelection)이 맡음.
/// </summary>
[RequireComponent(typeof(Button))]
public class GameOverButton : MonoBehaviour, IPointerEnterHandler
{
    private Button button;

    public Button Button => button != null ? button : (button = GetComponent<Button>());

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
