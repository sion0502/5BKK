using System.Collections;
using UnityEngine;

/// <summary>
/// 프롤로그 로비: 들어올 때 희미하게 켜져 있던 불(<see cref="FlickeringLightGroup"/>)이
/// 접수대 손전등을 줍는 순간 깜빡이다 꺼짐 (2026-11-17 사용자 기획 — 인트로에서 "왜 불이 켜져 있지?"로 이어짐).
/// 손전등 줍기는 월드 손전등 오브젝트가 사라지거나(파괴·비활성) 꺼지는 것으로 감지 — 아이템 코드는 건드리지 않음.
/// </summary>
public class PrologueLobbyPowerFail : MonoBehaviour
{
    [SerializeField] private FlickeringLightGroup lobbyLights;
    [Tooltip("접수대 위 월드 손전등 (World_Flashlight)")]
    [SerializeField] private GameObject flashlightPickup;
    [SerializeField] private float delayAfterPickup = 0.5f;

    [Header("Line (비우면 생략)")]
    [SerializeField] private string speaker = "나";
    [SerializeField] private string afterLine = "…뭐, 뭐야. 불이…";
    [SerializeField] private float lineDelay = 0.5f;

    private bool triggered;

    private void Update()
    {
        if (triggered || lobbyLights == null)
        {
            return;
        }

        if (flashlightPickup == null || !flashlightPickup.activeInHierarchy)
        {
            triggered = true;
            StartCoroutine(Run());
        }
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(delayAfterPickup);
        Debug.Log("[Prologue] 로비 불 꺼짐 (손전등 획득)");
        yield return lobbyLights.FailOut();

        if (!string.IsNullOrEmpty(afterLine))
        {
            yield return new WaitForSeconds(lineDelay);
            SubtitleUI.ShowForDuration(speaker, afterLine, 2.6f);
        }
    }
}
