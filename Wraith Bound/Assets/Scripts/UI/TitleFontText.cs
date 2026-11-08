using TMPro;
using UnityEngine;

/// <summary>
/// 붙은 TMP 텍스트(3D·UI 모두)에 메인메뉴 제목 붓글씨 폰트(TitleFontLoader)를 실행 중에 입힘.
/// 제목 폰트는 TMP 에셋이 아니라 런타임에 만드는 동적 폰트라 씬에 미리 지정할 수 없음 — 허브 봉인 부적 글씨 등에 사용.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TitleFontText : MonoBehaviour
{
    private void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        TMP_FontAsset font = TitleFontLoader.Load();
        TMP_Text text = GetComponent<TMP_Text>();
        if (font == null || text == null)
        {
            return;
        }

        font.TryAddCharacters(text.text, out _);
        text.font = font;
    }
}
