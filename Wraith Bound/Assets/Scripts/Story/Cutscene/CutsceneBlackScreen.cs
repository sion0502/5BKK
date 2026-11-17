using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 화면 검은 막. 컷씬의 검은 화면 구간·암전·건너뛰기 페이드에 씁니다.
/// 씬에 배치하거나 CreateRuntime()으로 코드에서 만들 수 있습니다.
/// </summary>
public class CutsceneBlackScreen : CutsceneOverlayBase
{
    [SerializeField] private Color color = Color.black;

    /// <summary>씬에 없을 때 코드로 생성 (DontDestroyOnLoad 아님 — 씬 전환 시 사라짐).</summary>
    public static CutsceneBlackScreen CreateRuntime(int sortingOrder = 900, float alpha = 0f)
    {
        GameObject go = new GameObject("CutsceneBlackScreen", typeof(RectTransform), typeof(CanvasGroup));
        go.SetActive(false);
        CutsceneBlackScreen screen = go.AddComponent<CutsceneBlackScreen>();
        screen.SetSortingOrder(sortingOrder);
        go.SetActive(true);
        screen.Alpha = alpha;
        return screen;
    }

    /// <summary>unscaled 시간으로 알파를 target까지 바꿉니다.</summary>
    public IEnumerator FadeTo(float target, float duration)
    {
        float start = Alpha;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            Alpha = Mathf.Lerp(start, target, Mathf.Clamp01(timer / duration));
            yield return null;
        }

        Alpha = target;
    }

    protected override void BuildContent(RectTransform root)
    {
        Image image = CreateStretched("Black", root).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
