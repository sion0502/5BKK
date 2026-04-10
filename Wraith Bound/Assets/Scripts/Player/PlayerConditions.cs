using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerConditions : MonoBehaviour
{
    [SerializeField]
    private float maxHealth;

    [SerializeField]
    private float maxStamina;

    [SerializeField]
    private float staminaRegenRate;
    private float regenDelay = 1.5f;


    private float currentStamina;
    private Coroutine regenCoroutine;
    private float currentHealth;

    public float maxHealthPublic => maxHealth; // 읽기 전용 프로퍼티
    public float maxStaminaPublic => maxStamina;
    public float GetCurrentHealth() => currentHealth;

    PlayerController playerController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;

        playerController = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        if(currentHealth == 0.0f)
        {
            Die();
        }
    }

    public void RecoverHealth(int amount)
    {
        // 아이템을 통해 체력을 회복할 때 회복한 현재 값이 최대값을 넘지 않도록, 최대값보다 클 경우 최대값을 현재 Value로 변경
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void RecoverStamina(int amount)
    {
        // 아이템을 통해 스태미나를 회복할 때 회복한 현재 값이 최대값을 넘지 않도록, 최대값보다 클 경우 최대값을 현재 Value로 변경
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
    }


    public void ConsumeStamina(float deltaTime)
    {
        if (regenCoroutine != null)
        {
            StopCoroutine(regenCoroutine);
            regenCoroutine = null;
        }

        currentStamina -= playerController.staminaDrainRate * deltaTime;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
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

            yield return null;
        }

        regenCoroutine = null;
    }

    public float GetCurrentStamina()
    {
        // 현재 스태미나 수치를 리턴
        return currentStamina;
    }

    public void Die()
    {
        Debug.Log("플레이어 사망");
    }
}
