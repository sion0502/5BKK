using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BatteryManager : MonoBehaviour
{
    [Header("장비 데이터 (SO)")]
    public Equipment itemData;

    [Header("UI 연결")]
    public Image batteryFill;
    public TextMeshProUGUI batteryText;
    public GameObject sonarDisplay;   // 레이더 화면
    public GameObject deadBatteryUI; // 방전 시 나타날 이미지 (새로 추가)

    private float currentEnergy;
    private bool isDead = false;

    void Start()
    {
        if (itemData != null)
        {
            currentEnergy = itemData.maxEnergy;
            if (deadBatteryUI != null) deadBatteryUI.SetActive(false); // 처음엔 방전 UI 끄기
        }
    }

    void Update()
    {
        if (itemData == null || isDead) return;

        if (currentEnergy > 0)
        {
            currentEnergy -= itemData.consumeRate * Time.deltaTime;

            // 0 이하로 내려가는 것 방지 (버그 수정 핵심)
            if (currentEnergy < 0) currentEnergy = 0;

            UpdateUI();
        }

        // 에너지가 0이 되는 순간 한 번만 실행
        if (currentEnergy <= 0 && !isDead)
        {
            Die();
        }
    }

    void UpdateUI()
    {
        float ratio = currentEnergy / itemData.maxEnergy;
        batteryFill.fillAmount = ratio;

        // 정수 변환 시 소수점 오차 방지를 위해 Mathf.Clamp 사용
        int percent = Mathf.CeilToInt(ratio * 100f);
        batteryText.text = percent.ToString();
    }

    void Die()
    {
        isDead = true;
        currentEnergy = 0;
        batteryText.text = "0";

        if (sonarDisplay != null) sonarDisplay.SetActive(false); // 레이더 끄기
        if (deadBatteryUI != null) deadBatteryUI.SetActive(true); // 방전 이미지 켜기

        Debug.Log("장비가 완전히 방전되었습니다.");
    }
}