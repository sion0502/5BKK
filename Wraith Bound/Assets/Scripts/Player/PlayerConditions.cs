using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerConditions : MonoBehaviour
{
    [SerializeField]
    public float maxHealth; // 최대 체력

    [SerializeField]
    public float maxStamina; // 최대 스태미나

    [SerializeField]
    private Coroutine regenCoroutine; // 스태미나 재생 코루틴

    [SerializeField]
    private float staminaRegenRate = 20f; // 초당 스태미나 회복량

    [SerializeField]
    private float regenDelay = 1.5f; // 스태미나 재생 시작까지 대기(초)

    [Header("Stamina Sprint / Bar Colors")]
    [SerializeField] private float staminaYellowThreshold = 0.4f;
    [SerializeField] private float staminaRedThreshold = 0.15f;
    [SerializeField] private float sprintRecoverThreshold = 0.4f;

    private bool staminaExhausted;

    public float currentHealth; // 현재 체력
    public float currentStamina; // 현재 스태미나
    public Boolean dead = false; // 사망상태(기본값은 false)

    public float maxHealthPublic => maxHealth; // 읽기 전용 프로퍼티
    public float maxStaminaPublic => maxStamina;
    public float GetCurrentHealth() => currentHealth;

    PlayerController playerController;

    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;

        playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        // [임시] 체력 UI·색상 테스트용
        if (Input.GetKeyDown(KeyCode.F5))
        {
            onDamage(10);
            Debug.Log($"[Debug] F5 테스트 데미지 -10, 현재 체력: {currentHealth}");
        }
    }

    public void onDamage(int damage) // 데미지를 받는 메서드
    {
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        // 현재 체력이 0 이하라면
        if (currentHealth <= 0)
        {
            // Die 메서드 호출
            Die();
        }
    }

    public void RecoverHealth(int amount)
    {
        // 아이템을 통해 체력을 회복할 때 회복한 현재 값이 최대값을 넘지 않도록, 최대값보다 클 경우 최대값으로 조정
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void RecoverStamina(int amount)
    {
        // 아이템을 통해 스태미나를 회복할 때 회복한 현재 값이 최대값을 넘지 않도록, 최대값보다 클 경우 최대값으로 조정
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
    }


    /// <param name="drainRatePerSecond">PlayerController에서 staminaDrainRate를 넘깁니다.</param>
    public void ConsumeStamina(float drainRatePerSecond)
    {
        if (regenCoroutine != null)
        {
            StopCoroutine(regenCoroutine);
            regenCoroutine = null;
        }

        currentStamina -= drainRatePerSecond * Time.deltaTime;
        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            staminaExhausted = true;
        }
        else
        {
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
    }

    public void StartStaminaRegen()
    {
        if (regenCoroutine == null && currentStamina < maxStamina)
        {
            regenCoroutine = StartCoroutine(RegenRoutine());
        }
    }

    private IEnumerator RegenRoutine()
    {
        yield return new WaitForSeconds(regenDelay);

        while (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
            TryClearStaminaExhaustion();

            yield return null;
        }

        TryClearStaminaExhaustion();
        regenCoroutine = null;
    }

    public float GetCurrentStamina()
    {
        // 현재 스태미나 수치를 리턴
        return currentStamina;
    }

    public float GetStaminaRatio()
    {
        if (maxStamina <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(currentStamina / maxStamina);
    }

    public bool IsStaminaExhausted => staminaExhausted;

    public float SprintRecoverThreshold => sprintRecoverThreshold;

    public bool IsRegeneratingStamina => regenCoroutine != null;

    /// <summary>
    /// 전부 소진 후 회복 중에는 sprintRecoverThreshold(노랑) 미만이면 달리기 불가.
    /// 그 외에는 stamina &gt; 0이면 달리기 가능(빨강 구간 포함).
    /// </summary>
    public bool CanSprint()
    {
        if (currentStamina <= 0f)
        {
            return false;
        }

        if (staminaExhausted && GetStaminaRatio() < sprintRecoverThreshold)
        {
            return false;
        }

        return true;
    }

    public Color GetStaminaBarColor()
    {
        return EvaluateStaminaBarColor(GetStaminaRatio(), staminaYellowThreshold, staminaRedThreshold);
    }

    public static Color EvaluateStaminaBarColor(float ratio, float yellowThreshold = 0.4f, float redThreshold = 0.15f)
    {
        Color green = new Color(0.2f, 0.95f, 0.35f, 0.85f);
        Color yellow = new Color(1f, 0.92f, 0.2f, 0.85f);
        Color red = new Color(1f, 0.25f, 0.2f, 0.85f);

        ratio = Mathf.Clamp01(ratio);

        if (ratio >= yellowThreshold)
        {
            float t = (ratio - yellowThreshold) / Mathf.Max(0.0001f, 1f - yellowThreshold);
            return Color.Lerp(yellow, green, t);
        }

        if (ratio >= redThreshold)
        {
            float t = (ratio - redThreshold) / Mathf.Max(0.0001f, yellowThreshold - redThreshold);
            return Color.Lerp(red, yellow, t);
        }

        return red;
    }

    private void TryClearStaminaExhaustion()
    {
        if (staminaExhausted && GetStaminaRatio() >= sprintRecoverThreshold)
        {
            staminaExhausted = false;
        }
    }

    public void Die()
    {
        // 사망상태를 true로 설정
        dead = true;
        Debug.Log("플레이어 사망");
    }
}
