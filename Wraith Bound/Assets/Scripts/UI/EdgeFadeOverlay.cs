using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가장자리부터 까맣게 조여 드는 전환 (2026-11-19 메인 메뉴 '이어서 하기' — 사용자 요청 '화면 옆에서부터 까맣게 페이드').
/// 가운데가 뚫린 비네트(절차적 텍스처)를 크게 → 작게 줄여 어둠이 안쪽으로 번지고, 끝에 완전한 검정으로 덮음. Open은 그 반대(가운데부터 열림).
/// unscaled 시간. 오버레이 캔버스 하나를 스스로 만듦.
/// </summary>
public class EdgeFadeOverlay : MonoBehaviour
{
    private RectTransform vignette;
    private Image black;
    private RawImage vignetteImage;

    public static EdgeFadeOverlay Create(int sortingOrder, Transform parent = null)
    {
        var go = new GameObject("EdgeFadeOverlay");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        EdgeFadeOverlay fade = go.AddComponent<EdgeFadeOverlay>();
        fade.Build();
        return fade;
    }

    private void Build()
    {
        // 비네트: 화면 크기에 맞춰 늘린 정사각 텍스처 (가운데 투명 → 가장자리 검정)
        var vgo = new GameObject("Vignette", typeof(RectTransform));
        vgo.transform.SetParent(transform, false);
        vignette = (RectTransform)vgo.transform;
        vignette.anchorMin = Vector2.zero;
        vignette.anchorMax = Vector2.one;
        vignette.offsetMin = vignette.offsetMax = Vector2.zero;
        vignetteImage = vgo.AddComponent<RawImage>();
        vignetteImage.texture = MakeVignette();
        vignetteImage.raycastTarget = false;

        // 비네트를 줄이면 그 바깥이 비므로, 비네트 사각형 바깥을 덮는 검정 판 4장(왼·오른·위·아래)을 자식으로 붙여 같이 줄어들게
        var edges = new (Vector2 min, Vector2 max)[]
        {
            (new Vector2(-9f, -9f), new Vector2(0f, 10f)), // 왼쪽
            (new Vector2(1f, -9f), new Vector2(10f, 10f)), // 오른쪽
            (new Vector2(0f, 1f), new Vector2(1f, 10f)),   // 위
            (new Vector2(0f, -9f), new Vector2(1f, 0f)),   // 아래
        };
        foreach ((Vector2 min, Vector2 max) in edges)
        {
            var sgo = new GameObject("Edge", typeof(RectTransform));
            sgo.transform.SetParent(vignette, false);
            var r = (RectTransform)sgo.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            Image img = sgo.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
        }

        var bgo = new GameObject("Black", typeof(RectTransform));
        bgo.transform.SetParent(transform, false);
        var br = (RectTransform)bgo.transform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        black = bgo.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        SetAmount(0f);
    }

    /// <summary>0 = 완전히 열림(안 보임), 1 = 완전히 검정.</summary>
    public void SetAmount(float a)
    {
        a = Mathf.Clamp01(a);
        // 비네트 크기: 열림 2.4배(가장자리만) → 닫힘 0.3배(가운데까지 어둠)
        float scale = Mathf.Lerp(2.4f, 0.3f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, a / 0.85f)));
        vignette.localScale = new Vector3(scale, scale, 1f);
        vignetteImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 4f));
        foreach (Image edge in vignette.GetComponentsInChildren<Image>())
        {
            edge.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a * 4f));
        }

        black.color = new Color(0f, 0f, 0f, Mathf.InverseLerp(0.75f, 1f, a));
    }

    public IEnumerator Close(float seconds)
    {
        yield return Animate(0f, 1f, seconds);
    }

    public IEnumerator Open(float seconds)
    {
        yield return Animate(1f, 0f, seconds);
    }

    private IEnumerator Animate(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAmount(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }

        SetAmount(to);
    }

    private static Texture2D MakeVignette()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_EdgeFadeVignette" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy); // 가운데 0, 변 가운데 1, 모서리 1.41
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, d)); // 더 안쪽부터 진하게 — 어두운 장면에서도 가장자리가 확실히 조여 보이게
                px[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
