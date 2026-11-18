using TMPro;
using UnityEngine;

/// <summary>데모 종료 팝업의 '클릭하여 닫기' 문구가 천천히 깜빡임.</summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class DemoEndPopupBlink : MonoBehaviour
{
    private TextMeshProUGUI label;
    private float baseAlpha;

    private void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        baseAlpha = label.color.a;
    }

    private void Update()
    {
        Color c = label.color;
        c.a = baseAlpha * (0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f)));
        label.color = c;
    }
}
