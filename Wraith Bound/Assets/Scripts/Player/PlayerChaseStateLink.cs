using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적(EnemyBase) 중 하나라도 플레이어를 '직접 보고' 쫓는 중이면 PlayerChaseEffectController의 추격 연출을 켭니다.
/// 발소리만 듣고 Chase로 들어온 경우는 제외 — 그 Chase 동안 한 번이라도 시야로 플레이어를 확인해야 연출이 켜지고,
/// 이후 시야를 잠깐 놓쳐도 해당 적이 Chase를 벗어날 때까지 유지됩니다.
/// EnemyBase는 수정하지 않고 RuntimeState를 읽기만 합니다.
/// 스포너가 런타임에 만드는 적도 잡히도록 주기적으로 적 목록을 갱신합니다.
/// </summary>
[RequireComponent(typeof(PlayerChaseEffectController))]
public class PlayerChaseStateLink : MonoBehaviour
{
    [Tooltip("적 목록 갱신 주기(초)")]
    [SerializeField] private float enemyRefreshInterval = 0.5f;
    [Tooltip("모든 적이 Chase를 벗어난 뒤 연출을 끄기까지 유지 시간(초). 상태가 잠깐 바뀌어도 연출이 끊기지 않도록.")]
    [SerializeField] private float chaseReleaseDelay = 1.5f;

    private readonly List<EnemyBase> enemies = new List<EnemyBase>();
    // 현재 Chase 구간에서 플레이어를 시야로 확인한 적
    private readonly HashSet<EnemyBase> sightedChasers = new HashSet<EnemyBase>();
    private PlayerChaseEffectController chaseEffects;
    private float nextRefreshTime;
    private float lastChaseTime = float.NegativeInfinity;
    private bool linkedChase;

    void Awake()
    {
        chaseEffects = GetComponent<PlayerChaseEffectController>();
    }

    void Update()
    {
        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + Mathf.Max(0.05f, enemyRefreshInterval);
            RefreshEnemies();
        }

        if (IsAnyEnemyChasing())
        {
            lastChaseTime = Time.time;
        }

        bool chased = Time.time - lastChaseTime <= chaseReleaseDelay;

        // 상태가 바뀔 때만 전달 → 평소엔 F6/F7 디버그 토글을 덮어쓰지 않음
        if (chased != linkedChase)
        {
            linkedChase = chased;
            chaseEffects.SetBeingChased(chased);
        }
    }

    private void RefreshEnemies()
    {
        enemies.Clear();
        enemies.AddRange(FindObjectsByType<EnemyBase>(FindObjectsSortMode.None));
        sightedChasers.RemoveWhere(e => e == null);
    }

    private bool IsAnyEnemyChasing()
    {
        bool anyChasing = false;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBase enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            if (!enemy.isActiveAndEnabled || enemy.RuntimeState.CurrentState != EnemyBase.State.Chase)
            {
                // Chase를 벗어나면 시야 확인 기록 초기화 → 다음 Chase는 다시 시야 확인 필요
                sightedChasers.Remove(enemy);
                continue;
            }

            if (enemy.RuntimeState.LastSawPlayer)
            {
                sightedChasers.Add(enemy);
            }

            if (sightedChasers.Contains(enemy))
            {
                anyChasing = true;
            }
        }

        return anyChasing;
    }
}
