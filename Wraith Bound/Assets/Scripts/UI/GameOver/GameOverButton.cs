using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임오버 선택지 한 개. 마우스를 올리면 선택 상태가 되어 키보드/마우스 강조가 하나로 통일된다.
/// 선택 시 캠코더 메뉴처럼 왼쪽 네모 표시 + 밑줄 + 밝은 글자.
/// </summary>
[RequireComponent(typeof(Button))]
public class GameOverButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    private static readonly Color IdleColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    private static readonly Color SelectedColor = Color.white;

    private TextMeshProUGUI label;
    private Image marker;
    private Image underline;
    private Button button;

    public Button Button => button;

    public void Init(TextMeshProUGUI labelText, Image markerImage, Image underlineImage)
    {
        label = labelText;
        marker = markerImage;
        underline = underlineImage;
        button = GetComponent<Button>();
        SetHighlighted(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetHighlighted(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlighted(false);
    }

    private void SetHighlighted(bool on)
    {
        label.color = on ? SelectedColor : IdleColor;
        marker.enabled = on;
        underline.enabled = on;
    }
}
