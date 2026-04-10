/*using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerConditionUI : MonoBehaviour
{
    [Header("Data Reference")]
    public PlayerConditions conditions;

    [Header("Type A: Numeric (수치형)")]
    public GameObject numericPanel;
    public Image hpBar;
    public Image staminaBar;

    [Header("Type B: Immersive (몰입형)")]
    public GameObject immersivePanel;
    public Image bloodOverlay;
    public Volume postProcessVolume;
    private ColorAdjustments colorAdjustments;

    void Start()
    {
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out colorAdjustments);
        }
    }

    void Update()
    {
        if (conditions == null) return;

        // --- 수정 포인트: 팀원분의 함수와 변수명에 정확히 일치시킴 ---

        // 1. 체력: GetCurrentHealth() 함수 사용 / maxHealth는 public 변수 사용
        float curHP = conditions.GetCurrentHealth();
        float maxHP = conditions.maxHealth;
        float hpRatio = curHP / maxHP;

        // 2. 스태미나: GetCurrentStamina() 함수 사용 / maxStamina는 public 변수 사용
        float curST = conditions.GetCurrentStamina();
        float maxST = conditions.maxStamina;
        float stRatio = curST / maxST;

        // 3. 수치형 UI 업데이트
        if (numericPanel != null && numericPanel.activeSelf)
        {
            hpBar.fillAmount = hpRatio;
            staminaBar.fillAmount = stRatio;
        }

        // 4. 몰입형 UI 연출
        if (immersivePanel != null && immersivePanel.activeSelf)
        {
            // 피 묻음 효과
            float bloodAlpha = Mathf.Clamp01((0.7f - hpRatio) * 1.5f);
            Color c = bloodOverlay.color;
            c.a = bloodAlpha;
            bloodOverlay.color = c;

            // 회색 화면 효과
            if (colorAdjustments != null)
            {
                float targetSat = Mathf.Lerp(-100f, 0f, hpRatio / 0.4f);
                colorAdjustments.saturation.value = Mathf.Clamp(targetSat, -100f, 0f);
            }
        }
    }
}*/