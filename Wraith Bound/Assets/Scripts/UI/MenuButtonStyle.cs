using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메뉴 버튼 마우스 올림 연출 (MenuUIUtility.CreateMenuButton이 붙임).
/// 평소: 흐린 글자 + 양옆 얇은 핀 장식 / 올리면: 뒤에 붓질 띠·위아래 붉은 실선이 번지고 글자가 붉게, 살짝 커짐.
/// 일시정지(timeScale 0) 중에도 움직이도록 unscaled 시간 사용.
/// </summary>
[DisallowMultipleComponent]
public class MenuButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float FadeSpeed = 9f;

    public Image band;
    public Image bandTop;
    public Image bandBottom;
    public Image leftPin;
    public Image rightPin;
    public TextMeshProUGUI label;

    [Tooltip("1 = 보통, 낮추면 흐리게 (클릭은 그대로 됨 — 예: 기록 없는 '이어서 하기')")]
    [Range(0f, 1f)] public float dim = 1f;

    [Tooltip("키보드로 고르는 메뉴(게임오버 등): EventSystem에서 선택된 버튼도 강조")]
    public bool followSelection;

    private Button button;
    private bool hovered;
    private float highlight;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        hovered = false;
        highlight = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        bool selected = followSelection && EventSystem.current != null &&
                        EventSystem.current.currentSelectedGameObject == gameObject;
        bool active = (hovered || selected) && (button == null || button.IsInteractable());
        highlight = Mathf.MoveTowards(highlight, active ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
        Apply();
    }

    private void Apply()
    {
        float h = highlight * highlight * (3f - 2f * highlight);

        if (band != null)
        {
            Color c = band.color;
            c.a = h * 0.92f;
            band.color = c;
        }

        SetAlpha(bandTop, MenuSkin.Accent, h * 0.85f);
        SetAlpha(bandBottom, MenuSkin.Accent, h * 0.85f);

        float pinAlpha = Mathf.Lerp(0.75f, 1.4f, h) * dim; // Ornament 알파(0.6)에 곱해짐 → 평소 0.45, 올리면 0.84
        SetAlpha(leftPin, MenuSkin.Ornament, pinAlpha);
        SetAlpha(rightPin, MenuSkin.Ornament, pinAlpha);

        if (label != null)
        {
            Color c = Color.Lerp(MenuSkin.Text, MenuSkin.TextHover, h);
            c.a *= dim;
            label.color = c;
            label.rectTransform.localScale = Vector3.one * (1f + 0.04f * h);
        }
    }

    private static void SetAlpha(Image image, Color baseColor, float alpha)
    {
        if (image == null)
        {
            return;
        }

        baseColor.a *= alpha;
        image.color = baseColor;
    }
}
