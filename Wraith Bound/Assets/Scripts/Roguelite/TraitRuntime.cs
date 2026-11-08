using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기 효과 적용 + 해금 진행 추적. 씬이 열릴 때마다 플레이어(Player 태그)에 자동으로 붙음.
/// 팀원 스크립트(PlayerController·PlayerConditions·RandomItemSpawner 등)는 수정하지 않고 공개 값을 실행 중에 바꾸거나 읽기만 함.
/// 효과: 무리한 질주·무거운 배낭·가시날개(속도·소모·칸), 아드레날린(발각 3초 무소모), 스타터팩(런 시작 시 패시브),
///       락픽 툴(열쇠 20% 유지), 타고난 신체(잠긴 문 파괴), 구사일생(1회 부활), 런닝맨(음식→스태미나는 ActiveItem, 획득 확률은 여기).
/// 진행: 숨기·추격 30초·인벤 가득·잠긴 문 열기·"열쇠가 필요합니다"·사망·체력 20% 이하 생존 귀환.
/// 운영자 초기화: Ctrl+Shift+F12 (부스 반복 시연).
/// </summary>
public class TraitRuntime : MonoBehaviour
{
    private static readonly HashSet<string> NonChapterScenes = new HashSet<string> { "Hub", "Prologue", "Intro", "MainMenu" };
    private static readonly string[] FoodPrefabHints = { "Can", "Water", "Jerky", "MRE", "Energy", "energy" };

    // 런(허브에서 나가 죽거나 돌아올 때까지) 상태 — 씬을 넘어가도 유지
    private static bool runStarted;
    private static bool reviveUsed;
    private static bool lowHpThisRun;

    private PlayerController controller;
    private PlayerConditions conditions;
    private InventoryManager inventory;
    private PlayerHidingController hiding;
    private PlayerChaseEffectController chase;

    private float baseMove, baseRun, baseCrouch, baseDrain;
    private int capacityBonusApplied;
    private float lastHealth;
    private bool wasHiding;
    private bool wasChased;
    private float chaseTime;
    private bool chaseCounted;
    private bool inventoryFullCounted;
    private float adrenalineUntil;
    private float adrenalineStamina;
    private float nextDoorPoll;
    private readonly Dictionary<LockedDoor, bool> doorLocked = new Dictionary<LockedDoor, bool>();
    private int lastKeyCount;
    private Items keyItem;

    public static bool IsChapterScene(string sceneName)
    {
        return !NonChapterScenes.Contains(sceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        runStarted = reviveUsed = lowHpThisRun = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerDeathDebug.ReviveHandler = TryRevive;
        PlayerDeathDebug.Died -= OnDied;
        PlayerDeathDebug.Died += OnDied;
        ScreenMessageUI.Shown -= OnMessage;
        ScreenMessageUI.Shown += OnMessage;
    }

    // 에디터에서 Play를 누를 때 처음 열린 씬은 sceneLoaded가 오지 않을 수 있어 한 번 더 붙임
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToFirstScene()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<TraitRuntime>() == null)
        {
            player.AddComponent<TraitRuntime>();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
        {
            return;
        }

        if (scene.name == "Hub")
        {
            // 챕터를 클리어하고 돌아왔으면(팀원 ExitEventCube가 켜는 표시) — 체력 20% 이하를 겪고도 살아 돌아온 것
            if (lowHpThisRun && ScreenFader.ShouldWakeUpInHub)
            {
                TraitProgress.Add(TraitCatalog.LowHpReturn);
            }

            runStarted = reviveUsed = lowHpThisRun = false;
        }

        if (TraitLoadout.IsEquipped(TraitId.RunningMan))
        {
            BoostFoodSpawns(); // 아이템 생성기의 Start보다 먼저
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.GetComponent<TraitRuntime>() == null)
        {
            player.AddComponent<TraitRuntime>();
        }
    }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        conditions = GetComponent<PlayerConditions>();
        inventory = GetComponent<InventoryManager>();
        hiding = GetComponent<PlayerHidingController>();
        chase = GetComponent<PlayerChaseEffectController>();
        keyItem = Resources.Load<Items>("ItemDatas/Active/Key");
        if (controller != null)
        {
            baseMove = controller.moveSpeed;
            baseRun = controller.runSpeed;
            baseCrouch = controller.crouchSpeed;
            baseDrain = controller.staminaDrainRate;
        }
    }

    private void OnEnable()
    {
        TraitLoadout.Changed += ApplyStats;
    }

    private void OnDisable()
    {
        TraitLoadout.Changed -= ApplyStats;
    }

    private void Start()
    {
        ApplyStats();
        lastHealth = conditions != null ? conditions.currentHealth : 0f;
        lastKeyCount = KeyCount();
        if (IsChapterScene(SceneManager.GetActiveScene().name) && !runStarted)
        {
            runStarted = true;
            if (TraitLoadout.IsEquipped(TraitId.StarterPack))
            {
                GrantStarterPassive();
            }
        }
    }

    // 속도·스태미나 소모·인벤 칸 — 착용이 바뀔 때마다 기본값에서 다시 계산
    private void ApplyStats()
    {
        if (controller == null)
        {
            return;
        }

        bool reckless = TraitLoadout.IsEquipped(TraitId.RecklessSprint);
        bool heavy = TraitLoadout.IsEquipped(TraitId.HeavyPack);
        bool thorn = TraitLoadout.IsEquipped(TraitId.ThornWings);
        float all = (reckless ? 1.5f : 1f) * (heavy ? 0.75f : 1f);
        controller.moveSpeed = baseMove * all * (thorn ? 0.5f : 1f);
        controller.runSpeed = baseRun * all;
        controller.crouchSpeed = baseCrouch * all * (thorn ? 1.3f : 1f);
        controller.staminaDrainRate = baseDrain * (reckless ? 1.5f : 1f);

        int wantBonus = heavy ? 2 : 0;
        if (inventory != null && wantBonus != capacityBonusApplied)
        {
            if (capacityBonusApplied > 0)
            {
                inventory.RemoveCapacityBonus(capacityBonusApplied);
            }

            if (wantBonus > 0)
            {
                inventory.AddCapacityBonus(wantBonus);
            }

            capacityBonusApplied = wantBonus;
        }
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F12))
        {
            TraitProgress.ResetAll();
            ScreenMessageUI.Show("기 해금 진행을 초기화했습니다 (운영자)", 2.5f);
        }

        if (conditions == null || PlayerDeathDebug.IsDead || PlayerDeathDebug.IsDying)
        {
            return;
        }

        TrackHealth();
        TrackHiding();
        TrackChase();
        TrackInventory();
        TrackDoors();
        Adrenaline();
    }

    private void TrackHealth()
    {
        float hp = conditions.currentHealth;
        // 무리한 질주: 받는 피해(소모 체력) +50%
        if (TraitLoadout.IsEquipped(TraitId.RecklessSprint) && hp < lastHealth)
        {
            hp = Mathf.Max(0f, hp - (lastHealth - hp) * 0.5f);
            conditions.currentHealth = hp;
        }

        if (hp > 0f && hp <= conditions.maxHealth * 0.2f && IsChapterScene(SceneManager.GetActiveScene().name))
        {
            lowHpThisRun = true;
        }

        lastHealth = hp;
    }

    private void TrackHiding()
    {
        bool now = hiding != null && hiding.isHiding;
        if (now && !wasHiding)
        {
            TraitProgress.Add(TraitCatalog.Hides);
        }

        wasHiding = now;
    }

    private void TrackChase()
    {
        bool now = chase != null && chase.IsBeingChased;
        if (now && !wasChased)
        {
            chaseTime = 0f;
            chaseCounted = false;
            // 아드레날린: 체력 20% 이하에서 발각되면 3초간 달리기 스태미나 무소모
            if (TraitLoadout.IsEquipped(TraitId.Adrenaline) && conditions.currentHealth <= conditions.maxHealth * 0.2f)
            {
                adrenalineUntil = Time.time + 3f;
                adrenalineStamina = conditions.currentStamina;
                ScreenMessageUI.Show("아드레날린!", 1.2f);
            }
        }

        if (now)
        {
            chaseTime += Time.deltaTime;
            if (!chaseCounted && chaseTime >= 30f)
            {
                chaseCounted = true;
                TraitProgress.Add(TraitCatalog.Chase30);
            }
        }

        wasChased = now;
    }

    private void Adrenaline()
    {
        if (Time.time < adrenalineUntil && controller != null && controller.isRun)
        {
            conditions.currentStamina = Mathf.Max(conditions.currentStamina, adrenalineStamina);
        }
    }

    private void TrackInventory()
    {
        if (inventory == null || inventoryFullCounted)
        {
            return;
        }

        int filled = 0;
        foreach (InventorySlot slot in inventory.slots)
        {
            if (slot != null && slot.item != null && slot.amount > 0)
            {
                filled++;
            }
        }

        if (filled >= inventory.capacity && inventory.capacity > 0)
        {
            inventoryFullCounted = true;
            TraitProgress.Add(TraitCatalog.InventoryFull);
        }
    }

    // 잠긴 문이 열렸는지 — 열릴 때 열쇠가 줄었으면 락픽 툴 20% 확률로 돌려줌
    private void TrackDoors()
    {
        if (Time.time < nextDoorPoll)
        {
            return;
        }

        nextDoorPoll = Time.time + 0.2f;
        int keys = KeyCount();
        foreach (LockedDoor door in FindObjectsByType<LockedDoor>(FindObjectsSortMode.None))
        {
            bool locked = door.IsLocked;
            if (doorLocked.TryGetValue(door, out bool wasLocked) && wasLocked && !locked)
            {
                TraitProgress.Add(TraitCatalog.LockedDoorsOpened);
                if (keys < lastKeyCount && TraitLoadout.IsEquipped(TraitId.Lockpick) && Random.value < 0.2f && keyItem != null)
                {
                    inventory.AddItem(keyItem, 1);
                    keys = KeyCount();
                    ScreenMessageUI.Show("락픽 툴 — 열쇠를 아꼈다", 2f);
                }
            }

            doorLocked[door] = locked;
        }

        lastKeyCount = keys;
    }

    private int KeyCount()
    {
        return inventory != null && keyItem != null ? inventory.CountItem(keyItem) : 0;
    }

    private void GrantStarterPassive()
    {
        PassiveItem[] passives = Resources.LoadAll<PassiveItem>("ItemDatas/Passive");
        if (passives.Length == 0 || inventory == null)
        {
            return;
        }

        PassiveItem pick = passives[Random.Range(0, passives.Length)];
        if (inventory.AddPassiveItem(pick))
        {
            ScreenMessageUI.Show("스타터팩 — " + pick.itemName, 2.5f);
        }
    }

    private static void BoostFoodSpawns()
    {
        foreach (RandomItemSpawner spawner in FindObjectsByType<RandomItemSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (spawner.items == null)
            {
                continue;
            }

            foreach (RandomItemSpawner.SpawnItem item in spawner.items)
            {
                if (item?.prefab == null)
                {
                    continue;
                }

                foreach (string hint in FoodPrefabHints)
                {
                    if (item.prefab.name.Contains(hint))
                    {
                        item.chance *= 2f; // 런닝맨: 음식·에너지 드링크 획득 확률 증가
                        break;
                    }
                }
            }
        }
    }

    // ── 정적 훅 ─────────────────────────────────────────

    private static void OnDied()
    {
        TraitProgress.Add(TraitCatalog.Deaths);
        if (IsChapterScene(SceneManager.GetActiveScene().name))
        {
            TraitProgress.Add(TraitCatalog.ChapterDeaths);
        }

        runStarted = false;
    }

    private const float ReviveStunRadius = 6f;
    private const float ReviveStunSeconds = 5f;

    // 구사일생: 런마다 한 번, 죽는 대신 체력 30으로 부활 — 금빛 이펙트, 붙잡은 괴물은 그 자리에서 5초 기절
    private static bool TryRevive()
    {
        if (reviveUsed || !TraitLoadout.IsEquipped(TraitId.NarrowEscape))
        {
            return false;
        }

        reviveUsed = true;
        PlayerDeathDebug.ReviveFromJumpscare();
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            return true;
        }

        PlayerConditions c = player.GetComponent<PlayerConditions>();
        if (c != null)
        {
            c.dead = false;
            c.currentHealth = Mathf.Min(30f, c.maxHealth);
        }

        // 붙잡았던 괴물(가까이 있는 적)은 그 자리에서 기절
        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            Vector3 d = enemy.transform.position - player.transform.position;
            d.y = 0f;
            if (d.magnitude <= ReviveStunRadius)
            {
                EnemyStun.Apply(enemy, ReviveStunSeconds);
            }
        }

        ReviveEffect.Play(player);
        ScreenMessageUI.Show("구사일생 — 아직 끝나지 않았다", 2.5f);
        Debug.Log("[Trait] 구사일생 부활");
        return true;
    }

    // "열쇠가 필요합니다" — 해금 카운터 + 타고난 신체(최대 체력의 80%를 써서 조준한 잠긴 문을 부숨, 모자라면 못 부숨)
    private static void OnMessage(string message)
    {
        if (message != "열쇠가 필요합니다")
        {
            return;
        }

        TraitProgress.Add(TraitCatalog.KeyNeeded);
        if (!TraitLoadout.IsEquipped(TraitId.BornStrong))
        {
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        Camera cam = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (cam == null || !Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 3.5f, ~0, QueryTriggerInteraction.Collide))
        {
            return;
        }

        LockedDoor door = hit.collider.GetComponentInParent<LockedDoor>();
        PlayerConditions c = player.GetComponent<PlayerConditions>();
        if (door == null || !door.IsLocked || c == null)
        {
            return;
        }

        float cost = c.maxHealth * 0.8f; // 최대 체력의 80% 소모 (사용자 확정)
        if (c.currentHealth <= cost)
        {
            ScreenMessageUI.Show("문을 부수기엔 체력이 부족하다…", 2f);
            return;
        }

        c.currentHealth -= cost;
        door.UnlockDoor();
        ScreenMessageUI.Show("몸으로 문을 부쉈다…", 2f);
    }
}
