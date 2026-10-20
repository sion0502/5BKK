using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Debug "Death" 발생 시 전체 정지. 나중에 UI 담당이 TriggerDeath()를 게임오버 UI로 교체.
/// </summary>
public static class PlayerDeathDebug
{
    public static bool IsDead { get; private set; }
    public static bool IsDying { get; private set; }
    public static bool EnemiesReleased { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        if (IsDead || IsDying) Time.timeScale = 1f;
        IsDead = false;
        IsDying = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (IsDead || IsDying) Time.timeScale = 1f;
        IsDead = false;
        IsDying = false;
    }

    public static void BeginJumpscare(EnemyBase enemy)
    {
        if (IsDead || IsDying) return;
        Camera camera = enemy != null && enemy.Player != null
            ? enemy.Player.GetComponentInChildren<Camera>() : null;
        if (camera == null) camera = Camera.main;
        if (enemy == null || camera == null || !camera.isActiveAndEnabled)
        {
            TriggerDeath();
            return;
        }

        IsDying = true;
        Time.timeScale = 0f;
        FreezePlayer(false);
        FreezeAllEnemies();
        new GameObject("Death Jumpscare").AddComponent<EnemyJumpscare>().Play(enemy, camera);
    }

    public static void TriggerDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        IsDying = false;
        Debug.Log("Death");

        Time.timeScale = 0f;
        FreezePlayer();
        FreezeAllEnemies();
    }

    public static void KeepViewAfterJumpscare()
    {
        IsDead = true;
        IsDying = false;
        EnemiesReleased = false;
        Debug.Log("Death");
        Time.timeScale = 1f;
        FreezePlayer();
        EnableMouseLook();
        SnapEnemiesToPatrol(true);
    }

    public static void ReleaseEnemiesToPatrol()
    {
        if (!IsDead)
            return;

        EnemiesReleased = true;
        Time.timeScale = 1f;
        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase enemy = enemies[i];
            if (enemy == null)
                continue;

            enemy.enabled = true;
            if (enemy.Anim != null)
                enemy.Anim.enabled = true;

            NavMeshAgent agent = enemy.Agent;
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;
                }
            }

            enemy.ReturnToPatrolAfterDeath();
        }
    }

    static void SnapEnemiesToPatrol(bool hold)
    {
        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                enemies[i].SnapToPatrolAnimation(hold);
        }
    }

    static void FreezePlayer(bool markDead = true)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        PlayerConditions conditions = player.GetComponent<PlayerConditions>();
        if (markDead && conditions != null)
            conditions.Die();

        PlayerHidingController hiding = player.GetComponent<PlayerHidingController>();
        if (hiding != null) hiding.StopAllCoroutines();

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
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            enemy.StopAllCoroutines();
            enemy.enabled = false;
        }
    }

    static void EnableMouseLook()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        MouseLook[] looks = player.GetComponentsInChildren<MouseLook>(true);
        for (int i = 0; i < looks.Length; i++)
        {
            if (looks[i] != null)
                looks[i].enabled = true;
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
