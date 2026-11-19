using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 중 HUD 요소(체력 숫자·스태미나 바)를 반짝이는 테두리 + 이름표로 가리킴 (2026-11-19 — 체력·스태미나 UI 설명).
/// 대상은 PlayerStatusHudUI가 만든 'HealthPanel'·'StaminaPanel'을 이름으로 찾음(HUD 코드는 안 건드림).
/// 스태미나 바는 달릴 때만 보이므로, 꺼져 있으면 그 자리에 빈 바 모양을 대신 그려 위치를 알려 줌. unscaled 시간.
/// </summary>
public class TutorialHudPointer : MonoBehaviour
{
    public const string Health = "HealthPanel";
    public const string Stamina = "StaminaPanel";

    private static readonly Color FrameColor = new Color(1f, 0.82f, 0.35f, 1f);

    private class Marker
    {
        public string targetName;
        public RectTransform target;
        public RectTransform root;
        public Image ghostBar;
        public CanvasGroup group;
    }

    private Canvas canvas;
    private readonly List<Marker> markers = new List<Marker>();

    public void Show(params (string target, string label)[] items)
    {
        Hide();
        EnsureCanvas();
        foreach ((string target, string label) in items)
        {
            markers.Add(CreateMarker(target, label));
        }
    }

    public void Hide()
    {
        foreach (Marker m in markers)
        {
            if (m.root != null)
            {
                Destroy(m.root.gameObject);
            }
        }

        markers.Clear();
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
        {
            return;
        }

        // 최상위 캔버스로 — 튜토리얼 캔버스(CanvasScaler) 안에 넣으면 그 크기·배율을 물려받아 화면 픽셀 좌표가 어긋났음
        var go = new GameObject("TutorialHudPointer", typeof(RectTransform));
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 640; // HUD·튜토리얼 안내 위, 팁 팝업(일시정지) 아래
    }

    private Marker CreateMarker(string targetName, string label)
    {
        var m = new Marker { targetName = targetName };
        var go = new GameObject("Marker_" + targetName, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        m.root = (RectTransform)go.transform;
        m.root.anchorMin = m.root.anchorMax = Vector2.zero;
        m.root.pivot = new Vector2(0.5f, 0.5f);
        m.group = go.AddComponent<CanvasGroup>();
        m.group.blocksRaycasts = false;

        // 스태미나 바가 꺼져 있을 때 보여 줄 빈 바
        var ghost = new GameObject("GhostBar", typeof(RectTransform));
        ghost.transform.SetParent(go.transform, false);
        var gr = (RectTransform)ghost.transform;
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(8f, 8f);
        gr.offsetMax = new Vector2(-8f, -8f);
        m.ghostBar = ghost.AddComponent<Image>();
        m.ghostBar.color = new Color(1f, 1f, 1f, 0.18f);
        m.ghostBar.raycastTarget = false;

        // 테두리 4변
        AddEdge(go.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));
        AddEdge(go.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 0f));
        AddEdge(go.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(3f, 0f));
        AddEdge(go.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), new Vector2(0f, 0f));

        // 이름표 (테두리 위)
        var lgo = new GameObject("Label", typeof(RectTransform));
        lgo.transform.SetParent(go.transform, false);
        var lr = (RectTransform)lgo.transform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.anchoredPosition = new Vector2(0f, 8f);
        lr.sizeDelta = new Vector2(240f, 40f);
        TextMeshProUGUI text = lgo.AddComponent<TextMeshProUGUI>();
        text.text = "▼ " + label;
        text.fontSize = 26f;
        text.fontStyle = FontStyles.Bold;
        text.color = FrameColor;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        TMP_FontAsset font = UIFontConfig.UI;
        if (font != null)
        {
            text.font = font;
        }

        return m;
    }

    private void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    private static void AddEdge(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject("Edge", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = aMin;
        r.anchorMax = aMax;
        r.offsetMin = oMin;
        r.offsetMax = oMax;
        Image img = go.AddComponent<Image>();
        img.color = FrameColor;
        img.raycastTarget = false;
    }

    private static void ComputeFromAnchors(RectTransform r, out Vector2 min, out Vector2 max)
    {
        var parent = r.parent as RectTransform;
        var pc = new Vector3[4];
        if (parent == null)
        {
            min = max = Vector2.zero;
            return;
        }

        parent.GetWorldCorners(pc);
        Vector2 pMin = pc[0];
        Vector2 pMax = pc[2];
        Vector2 pSize = pMax - pMin;
        float scale = parent.lossyScale.x;
        Vector2 aMin = pMin + Vector2.Scale(pSize, r.anchorMin);
        Vector2 aMax = pMin + Vector2.Scale(pSize, r.anchorMax);
        Vector2 size = (aMax - aMin) + r.sizeDelta * scale;
        Vector2 anchorRef = Vector2.Lerp(aMin, aMax, 0.5f);
        if (r.anchorMin == r.anchorMax)
        {
            anchorRef = aMin;
        }

        Vector2 pivotPos = anchorRef + r.anchoredPosition * scale;
        min = pivotPos - Vector2.Scale(size, r.pivot);
        max = min + size;

        // 바 자체(첫 자식) 크기가 패널보다 크면 그걸 씀 — 패널 크기가 0인 경우
        if (size.sqrMagnitude < 4f && r.childCount > 0 && r.GetChild(0) is RectTransform child)
        {
            Vector2 cs = child.sizeDelta * scale;
            Vector2 c = pivotPos + child.anchoredPosition * scale;
            min = c - cs * 0.5f;
            max = c + cs * 0.5f;
        }
    }

    private static RectTransform FindTarget(string name)
    {
        foreach (RectTransform r in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name == name && r.gameObject.scene.IsValid())
            {
                return r;
            }
        }

        return null;
    }

    private void LateUpdate()
    {
        float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.1f));
        var corners = new Vector3[4];
        foreach (Marker m in markers)
        {
            if (m.root == null)
            {
                continue;
            }

            if (m.target == null)
            {
                m.target = FindTarget(m.targetName);
            }

            if (m.target == null)
            {
                m.group.alpha = 0f;
                continue;
            }

            // 대상 HUD 캔버스는 화면 오버레이라 월드 좌표 = 화면 픽셀. 대상 크기가 0이면(스태미나 채움이 빈 경우 등) 최소 크기
            Vector2 min;
            Vector2 max;
            if (m.target.gameObject.activeInHierarchy)
            {
                m.target.GetWorldCorners(corners);
                min = corners[0];
                max = corners[2];
            }
            else
            {
                // 꺼진 패널(스태미나 바)은 레이아웃이 안 돼 월드 좌표가 엉뚱함 → 부모 사각형 + 앵커로 직접 계산
                ComputeFromAnchors(m.target, out min, out max);
            }

            Vector2 size = max - min;
            Vector2 center = (min + max) * 0.5f;
            size.x = Mathf.Max(size.x, 120f) + 24f;
            size.y = Mathf.Max(size.y, 26f) + 24f;
            m.root.anchoredPosition = center;
            m.root.sizeDelta = size;
            m.ghostBar.enabled = !m.target.gameObject.activeInHierarchy && m.targetName == Stamina;
            m.group.alpha = pulse;
        }
    }
}
