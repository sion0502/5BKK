using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력(숫자·색상) + 조건부 스테미나 바 HUD. Canvas에 붙입니다.
/// </summary>
public class PlayerStatusHudUI : MonoBehaviour
{
    private const string PolFontAssetPath =
        "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    private static readonly Color HealthColorNormal = Color.white;
    private static readonly Color HealthColorWarning = Color.yellow;
    private static readonly Color HealthColorCritical = Color.red;

    [Header("References")]
    [SerializeField] private PlayerConditions conditions;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerHidingController hidingController;

    [Header("Health (bottom-left)")]
    [SerializeField] private Vector2 healthAnchoredPosition = new Vector2(24f, 24f);
    [SerializeField] private int healthFontSize = 28;

    [Header("Stamina (bottom-center)")]
    [SerializeField] private Vector2 staminaAnchoredPosition = new Vector2(0f, 32f);
    [SerializeField] private Vector2 staminaBarSize = new Vector2(360f, 14f);
    [SerializeField] private float staminaFadeOutDuration = 0.5f;

    [Header("Breath (shown while hiding)")]
    // 숨는 중에만 표시되는 숨 참기 게이지 위치와 크기입니다.
    [SerializeField] private Vector2 breathAnchoredPosition = new Vector2(0f, 58f);
    [SerializeField] private Vector2 breathBarSize = new Vector2(320f, 12f);

    private TextMeshProUGUI healthValueText;
    private GameObject staminaRoot;
    private CanvasGroup staminaCanvasGroup;
    private GameObject staminaTrackGo;
    private RectTransform staminaFillRect;
    private GameObject staminaFillGo;
    private Image staminaFillImage;
    private Coroutine staminaFadeCoroutine;
    private GameObject breathRoot;
    private GameObject breathTrackGo;
    private RectTransform breathFillRect;
    private GameObject breathFillGo;
    private Image breathFillImage;

    void Awake()
    {
        if (conditions == null)
        {
            conditions = FindFirstObjectByType<PlayerConditions>();
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (hidingController == null)
        {
            hidingController = FindFirstObjectByType<PlayerHidingController>();
        }

        EnsureHudRoot();
        BuildHealthUI();
        BuildStaminaUI();
        BuildBreathUI();
    }

    void Update()
    {
        if (conditions == null)
        {
            return;
        }

        UpdateHealthUI();
        UpdateStaminaUI();
        UpdateBreathUI();
    }

    private void EnsureHudRoot()
    {
        if (GetComponent<Canvas>() != null)
        {
            GameplayHudCanvasSetup.EnsureOverlayCanvas(gameObject);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[PlayerStatusHudUI] Canvas를 찾을 수 없습니다.");
            return;
        }

        transform.SetParent(canvas.transform, false);
    }

    private void BuildHealthUI()
    {
        GameObject panel = CreatePanel(
            "HealthPanel",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            healthAnchoredPosition);

        GameObject textGo = new GameObject("HealthValue");
        textGo.transform.SetParent(panel.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        healthValueText = textGo.AddComponent<TextMeshProUGUI>();
        healthValueText.font = ResolveFont();
        healthValueText.fontSize = healthFontSize;
        healthValueText.alignment = TextAlignmentOptions.BottomLeft;
        healthValueText.raycastTarget = false;
        healthValueText.text = "100";
    }

    private void BuildStaminaUI()
    {
        staminaRoot = CreatePanel(
            "StaminaPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            staminaAnchoredPosition);

        staminaCanvasGroup = staminaRoot.AddComponent<CanvasGroup>();
        staminaCanvasGroup.alpha = 1f;
        staminaCanvasGroup.blocksRaycasts = false;
        staminaCanvasGroup.interactable = false;

        staminaTrackGo = new GameObject("StaminaTrack");
        staminaTrackGo.transform.SetParent(staminaRoot.transform, false);

        RectTransform trackRect = staminaTrackGo.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = staminaBarSize;

        Image trackImage = staminaTrackGo.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.2f);
        trackImage.raycastTarget = false;

        staminaFillGo = new GameObject("StaminaFill");
        staminaFillGo.transform.SetParent(staminaTrackGo.transform, false);

        staminaFillRect = staminaFillGo.AddComponent<RectTransform>();
        staminaFillRect.anchorMin = new Vector2(0.5f, 0.5f);
        staminaFillRect.anchorMax = new Vector2(0.5f, 0.5f);
        staminaFillRect.pivot = new Vector2(0.5f, 0.5f);
        staminaFillRect.anchoredPosition = Vector2.zero;
        staminaFillRect.sizeDelta = staminaBarSize;

        staminaFillImage = staminaFillGo.AddComponent<Image>();
        staminaFillImage.color = PlayerConditions.EvaluateStaminaBarColor(1f);
        staminaFillImage.raycastTarget = false;

        staminaRoot.SetActive(false);
    }

    private void BuildBreathUI()
    {
        // 기존 HUD 프리팹을 크게 수정하지 않기 위해 런타임에 숨 게이지 UI를 구성합니다.
        breathRoot = CreatePanel(
            "BreathPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            breathAnchoredPosition);

        breathTrackGo = new GameObject("BreathTrack");
        breathTrackGo.transform.SetParent(breathRoot.transform, false);

        RectTransform trackRect = breathTrackGo.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = breathBarSize;

        Image trackImage = breathTrackGo.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.25f);
        trackImage.raycastTarget = false;

        breathFillGo = new GameObject("BreathFill");
        breathFillGo.transform.SetParent(breathTrackGo.transform, false);

        breathFillRect = breathFillGo.AddComponent<RectTransform>();
        breathFillRect.anchorMin = new Vector2(0.5f, 0.5f);
        breathFillRect.anchorMax = new Vector2(0.5f, 0.5f);
        breathFillRect.pivot = new Vector2(0.5f, 0.5f);
        breathFillRect.anchoredPosition = Vector2.zero;
        breathFillRect.sizeDelta = breathBarSize;

        breathFillImage = breathFillGo.AddComponent<Image>();
        breathFillImage.color = GetBreathColor(1f);
        breathFillImage.raycastTarget = false;

        breathRoot.SetActive(false);
    }

    private void UpdateHealthUI()
    {
        if (healthValueText == null)
        {
            return;
        }

        int hp = Mathf.CeilToInt(conditions.GetCurrentHealth());
        healthValueText.text = hp.ToString();
        healthValueText.color = GetHealthColor(hp);
    }

    private Color GetHealthColor(int currentHp)
    {
        if (currentHp <= 20)
        {
            return HealthColorCritical;
        }

        if (currentHp <= 50)
        {
            return HealthColorWarning;
        }

        return HealthColorNormal;
    }

    private void UpdateStaminaUI()
    {
        if (staminaRoot == null || staminaFillRect == null || staminaCanvasGroup == null)
        {
            return;
        }

        bool isDraining = playerController != null && playerController.isRun;
        bool isRegenerating = conditions.IsRegeneratingStamina;
        bool isDepleted = conditions.GetCurrentStamina() <= 0.01f;
        float ratio = conditions.GetStaminaRatio();
        bool isExhaustedLockout = conditions.IsStaminaExhausted
            && ratio < conditions.SprintRecoverThreshold;
        bool wantsVisible = isDraining || isRegenerating || isDepleted || isExhaustedLockout;

        if (wantsVisible)
        {
            StopStaminaFade();

            if (!staminaRoot.activeSelf)
            {
                staminaRoot.SetActive(true);
            }

            staminaCanvasGroup.alpha = 1f;
            ApplyStaminaFillWidth(ratio);
            return;
        }

        if (staminaRoot.activeSelf && staminaCanvasGroup.alpha > 0.01f)
        {
            if (staminaFadeCoroutine == null)
            {
                staminaFadeCoroutine = StartCoroutine(FadeOutStaminaBar());
            }

            return;
        }

        staminaRoot.SetActive(false);
        staminaCanvasGroup.alpha = 1f;
    }

    private void ApplyStaminaFillWidth(float ratio)
    {
        staminaTrackGo.SetActive(true);

        if (ratio <= 0.001f)
        {
            staminaFillGo.SetActive(false);
            staminaFillRect.sizeDelta = Vector2.zero;
            return;
        }

        staminaFillGo.SetActive(true);
        staminaFillRect.sizeDelta = new Vector2(staminaBarSize.x * ratio, staminaBarSize.y);

        if (staminaFillImage != null)
        {
            staminaFillImage.color = conditions != null
                ? conditions.GetStaminaBarColor()
                : PlayerConditions.EvaluateStaminaBarColor(ratio);
        }
    }

    private void UpdateBreathUI()
    {
        if (breathRoot == null || breathFillRect == null)
        {
            return;
        }

        if (hidingController == null)
        {
            hidingController = FindFirstObjectByType<PlayerHidingController>();
        }

        bool wantsVisible =
            hidingController != null &&
            hidingController.isHiding;

        // 숨는 장소 안에 있을 때만 숨 게이지를 보여줍니다.
        breathRoot.SetActive(wantsVisible);

        if (!wantsVisible)
        {
            return;
        }

        ApplyBreathFillWidth(hidingController.BreathRatio);
    }

    private void ApplyBreathFillWidth(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        breathTrackGo.SetActive(true);

        if (ratio <= 0.001f)
        {
            breathFillGo.SetActive(false);
            breathFillRect.sizeDelta = Vector2.zero;
            return;
        }

        breathFillGo.SetActive(true);
        breathFillRect.sizeDelta = new Vector2(breathBarSize.x * ratio, breathBarSize.y);

        if (breathFillImage != null)
        {
            breathFillImage.color = GetBreathColor(ratio);
        }
    }

    private Color GetBreathColor(float ratio)
    {
        // 게이지가 낮을수록 빨간색에 가까워져 위험 상태를 직관적으로 보여줍니다.
        Color calm = new Color(0.45f, 0.9f, 1f, 0.85f);
        Color warning = new Color(1f, 0.92f, 0.2f, 0.85f);
        Color danger = new Color(1f, 0.25f, 0.2f, 0.85f);

        ratio = Mathf.Clamp01(ratio);

        if (ratio >= 0.35f)
        {
            float t = (ratio - 0.35f) / 0.65f;
            return Color.Lerp(warning, calm, t);
        }

        float lowT = ratio / 0.35f;
        return Color.Lerp(danger, warning, lowT);
    }

    private IEnumerator FadeOutStaminaBar()
    {
        float startAlpha = staminaCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < staminaFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = staminaFadeOutDuration > 0f ? elapsed / staminaFadeOutDuration : 1f;
            staminaCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        staminaCanvasGroup.alpha = 0f;
        staminaRoot.SetActive(false);
        staminaCanvasGroup.alpha = 1f;
        staminaFadeCoroutine = null;
    }

    private void StopStaminaFade()
    {
        if (staminaFadeCoroutine != null)
        {
            StopCoroutine(staminaFadeCoroutine);
            staminaFadeCoroutine = null;
        }
    }

    private GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(160f, 48f);

        return panel;
    }

    private TMP_FontAsset ResolveFont()
    {
        TMP_FontAsset font = UIFontConfig.PolHumanRights;

#if UNITY_EDITOR
        if (font == null)
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PolFontAssetPath);
        }
#endif

        return font != null ? font : TMP_Settings.defaultFontAsset;
    }
}
