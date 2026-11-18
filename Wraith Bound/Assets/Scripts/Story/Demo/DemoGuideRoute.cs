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
