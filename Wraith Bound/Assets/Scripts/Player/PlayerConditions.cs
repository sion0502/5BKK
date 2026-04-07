using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class Condition
{
    [HideInInspector]
    public float curValue;
    public float maxValue;
    public float startValue;
    public float regenRate;

    public void Add(float amount)
    {
        // Condition의 값이 추가가 될 때 추가된 현재 값이 최대값을 넘지 않도록, 최대값보다 클 경우 최대값을 현재 Value로 변경
        curValue = Mathf.Min(curValue + amount, maxValue);
    }

    public void Subtract(float amount)
    {
        // Condition의 값이 차감될 때 추가된 현재 값이 최소값을 넘지 않도록, 최소값보다 작을 경우 최소값을 현재 Value로 변경
        curValue = Mathf.Max(curValue - amount, 0.0f);
    }
}

public class PlayerConditions : MonoBehaviour
{
    public Condition health;
    public Condition stamina;

    public UnityEvent onTakeDamage; //데미지

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        health.curValue = health.startValue;
        stamina.curValue = stamina.startValue;
    }

    // Update is called once per frame
    void Update()
    {
        // 1초마다 스태미나 회복
        stamina.Add(stamina.regenRate * Time.deltaTime);

        if(health.curValue == 0.0f)
        {
            
        }
    }

    public void Heal(float amount)
    {
        health.Add(amount);
    }

    public bool UseStamina(float amount)
    {
        // 스태미나가 없을 경우 스태미나를 사용하지 못하도록
        if(stamina.curValue - amount < 0) return false;

        stamina.Subtract(amount);
        return true;
    }

    public void Die()
    {
        Debug.Log("플레이어 사망");
    }
}
