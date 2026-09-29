using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Debug "Death" 발생 시 전체 정지. 나중에 UI 담당이 TriggerDeath()를 게임오버 UI로 교체.
/// </summary>
public static class PlayerDeathDebug
{
    public static bool IsDead { get; private set; }

    public static void TriggerDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        Debug.Log("Death");

        Time.timeScale = 0f;
        FreezePlayer();
        FreezeAllEnemies();
    }

    static void FreezePlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        PlayerConditions conditions = player.GetComponent<PlayerConditions>();
        if (conditions != null)
            conditions.Die();

        DisableIfExists<PlayerController>(player);
        DisableIfExists<MouseLook>(player);
        DisableIfExists<PlayerAudioMixerController>(player);
        DisableIfExists<PlayerHidingController>(player);

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
    }

    static void FreezeAllEnemies()
    {
        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase enemy = enemies[i];
            if (enemy == null)
                continue;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            enemy.enabled = false;
        }
    }

    static void DisableIfExists<T>(GameObject root) where T : Behaviour
    {
        T behaviour = root.GetComponent<T>();
        if (behaviour != null)
            behaviour.enabled = false;

        T[] children = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
                children[i].enabled = false;
        }
    }
}
