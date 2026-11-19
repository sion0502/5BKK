using System;
using UnityEngine;

/// <summary>
/// 시연 씬 길 안내 루트 (2026-11-19, 시연 전용 기 '길잡이 혼불'). 연출을 모두 지나도록 짠 순서:
/// 열쇠 → 부적1 → ④ 구역 → 부적4(③) → 침대 열쇠 → 부적7 → ⑤ 구역 → 출구.
/// 단계마다 앞 단계 목표에서 이 단계 목표까지의 경로(꺾이는 점)를 에디터가 문을 전부 연 임시 NavMesh로 미리 구워 둠
/// (실행 중엔 닫힌 문이 NavMesh를 깎아 경로가 엉뚱한 곳에서 끊기므로) — Editor/FixedDemoGuide.cs.
/// 혼불(DemoGuideWisp)은 기를 착용했을 때만 생김.
/// </summary>
public class DemoGuideRoute : MonoBehaviour
{
    public enum StepKind
    {
        Key,
        Talisman,
        Zone,
        Gate,
    }

    [Serializable]
    public class Step
    {
        public string label;
        public StepKind kind;
        public Transform target;
        [Tooltip("부적 단계: 이 부적 / 열쇠 단계: 이 열쇠로 여는 방의 부적(이미 탔으면 건너뜀) / 구역 단계: 이 구역 연출을 켜는 부적")]
        public DemoTalisman talisman;
        public StoryZone zone;
        public Vector3[] path = { };
    }

    [SerializeField] private Step[] steps = { };
    [Tooltip("출구 봉인 대문 문짝 — 봉인이 남아 있으면 혼불이 이 앞에서 기다림")]
    [SerializeField] private Transform[] gateDoors = { };

    public bool IsGateDoor(Transform t)
    {
        if (DemoSealProgress.Count >= DemoSealProgress.Total)
        {
            return false;
        }

        foreach (Transform d in gateDoors)
        {
            if (d != null && t.IsChildOf(d))
            {
                return true;
            }
        }

        return false;
    }

    [Tooltip("혼불이 처음 나타날 때 안내 문구")]
    [SerializeField] private string introMessage = "길잡이 혼불이 앞장선다";

    private bool[] zoneVisited;
    private InventoryManager inventory;

    public int Current { get; private set; }

    private static DemoGuideRoute active;

    /// <summary>p가 아직 남은 안내 경로(현재 단계부터 끝까지) radius 안이면 true — 연출 뒤 배회 괴물을 길에서 먼 곳에 두려고 (DemoRoamingSpawner).</summary>
    /// <summary>
    /// 남은 길잡이 루트에서 수평 거리 radius 안인지 (층 무시) — 소리는 바닥·천장을 넘어 들리므로
    /// 위·아래층이라도 루트 바로 위면 가까운 것으로 봄. 괴물 울음이 갈 길에서 들리지 않게 고를 때 씀.
    /// </summary>
    public static bool IsNearRemainingRouteFlat(Vector3 p, float radius)
    {
        if (active == null)
        {
            return false;
        }

        float r2 = radius * radius;
        for (int i = Mathf.Max(0, active.Current); i < active.steps.Length; i++)
        {
            foreach (Vector3 q in active.steps[i].path)
            {
                float dx = q.x - p.x;
                float dz = q.z - p.z;
                if (dx * dx + dz * dz < r2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsNearRemainingRoute(Vector3 p, float radius)
    {
        if (active == null)
        {
            return false;
        }

        float r2 = radius * radius;
        for (int i = Mathf.Max(0, active.Current); i < active.steps.Length; i++)
        {
            foreach (Vector3 q in active.steps[i].path)
            {
                Vector3 d = q - p;
                d.y *= 3f; // 다른 층은 멀게
                if (d.sqrMagnitude < r2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 캠코더 부적 감지가 가리킬 목표 (2026-11-19): 안내 순서상 아직 안 태운 첫 부적, 부적이 다 모였으면 출구(마지막 단계 목표).
    /// 연출 루트 순서(④→③→⑤)를 따르게 — 루트 밖 부적은 가리키지 않음.
    /// </summary>
    public static Transform NextCamcorderTarget()
    {
        if (active == null || active.steps.Length == 0)
        {
            return null;
        }

        if (DemoSealProgress.Count < DemoSealProgress.Total)
        {
            foreach (Step s in active.steps)
            {
                if (s.kind == StepKind.Talisman && s.talisman != null && !DemoSealProgress.Has(s.talisman.Index))
                {
                    return s.talisman.transform;
                }
            }

            return null;
        }

        return active.steps[active.steps.Length - 1].target;
    }

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
        {
            active = null;
        }
    }

    public Step CurrentStep => steps.Length > 0 ? steps[Mathf.Clamp(Current, 0, steps.Length - 1)] : null;

    private void Start()
    {
        zoneVisited = new bool[steps.Length];
        if (TraitLoadout.IsEquipped(TraitId.GuideWisp))
        {
            var go = new GameObject("GuideWisp");
            go.transform.SetParent(transform, false);
            go.AddComponent<DemoGuideWisp>().Init(this);
            if (!string.IsNullOrEmpty(introMessage))
            {
                ScreenMessageUI.Show(introMessage, 2.5f);
            }
        }
    }

    private void Update()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            Step s = steps[i];
            if (s.kind == StepKind.Zone && s.zone != null && !zoneVisited[i] && (s.zone.IsPlayerInside || s.zone.HasFired))
            {
                zoneVisited[i] = true;
            }
        }

        // 순서대로 — 부적 3장이 모이면 남은 열쇠·부적 단계는 건너뛰지만 연출 구역(⑤)은 지나가게 (예전엔 바로 출구로 건너뛰어
        // 2층 부적7 자리에서 1층 ⑤→출구 경로로 바뀌어 혼불이 안 보였음)
        int c = 0;
        while (c < steps.Length - 1 && IsDone(c))
        {
            c++;
        }

        if (c != Current)
        {
            Debug.Log("[Demo] 길 안내 → " + steps[c].label);
        }

        Current = c;
    }

    private bool IsDone(int i)
    {
        Step s = steps[i];
        if (DemoSealProgress.Count >= DemoSealProgress.Total && (s.kind == StepKind.Key || s.kind == StepKind.Talisman))
        {
            return true;
        }

        switch (s.kind)
        {
            case StepKind.Key:
                if (s.talisman != null && DemoSealProgress.Has(s.talisman.Index))
                {
                    return true;
                }

                if (s.target == null || !s.target.gameObject.activeInHierarchy)
                {
                    return true;
                }

                if (inventory == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    inventory = player != null ? player.GetComponentInChildren<InventoryManager>() : null;
                }

                return inventory != null && inventory.HasKey();
            case StepKind.Talisman:
                return s.talisman == null || DemoSealProgress.Has(s.talisman.Index);
            case StepKind.Zone:
                // 부적 3장이 모였는데 이 구역 연출을 켜는 부적(talisman)을 안 태웠으면 가 봐야 아무 일 없음 → 건너뜀
                if (DemoSealProgress.Count >= DemoSealProgress.Total && s.talisman != null && !DemoSealProgress.Has(s.talisman.Index))
                {
                    return true;
                }

                return s.zone == null || zoneVisited[i];
            default:
                return false;
        }
    }
}
