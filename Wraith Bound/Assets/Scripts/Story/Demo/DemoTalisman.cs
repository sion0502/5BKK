using UnityEngine;

/// <summary>
/// 시연 맵 부적 (3장 중 하나 — 예전 봉인 열쇠 자리). E로 태우면 불타 사라지고(TalismanBurnEffect)
/// DemoSealProgress에 기록 → 출구 대문의 봉인 부적도 한 장 타며 봉인이 약해짐(DemoSealGate), 수술실 부적이면 연출 ③.
/// </summary>
[RequireComponent(typeof(TalismanBurnEffect))]
public class DemoTalisman : MonoBehaviour, IInteractable
{
    [SerializeField] private int index;
    [SerializeField] private string prompt = "[E] 부적 태우기";

    private TalismanBurnEffect burn;
    private bool used;

    private void Awake()
    {
        burn = GetComponent<TalismanBurnEffect>();
    }

    private void Start()
    {
        if (DemoSealProgress.Has(index))
        {
            used = true;
            burn.BurnInstant();
            DisableColliders();
        }
    }

    public string GetInteractPrompt()
    {
        return used ? null : prompt;
    }

    public void Interact(GameObject interactor)
    {
        if (used)
        {
            return;
        }

        used = true;
        DisableColliders();
        burn.Burn();
        DemoSealProgress.Collect(index);
        int left = DemoSealProgress.Total - DemoSealProgress.Count;
        if (left > 0)
        {
            ScreenMessageUI.Show("부적이 타오른다 — 출구의 봉인이 약해졌다 (" + DemoSealProgress.Count + "/" + DemoSealProgress.Total + ")", 2.8f);
        }
        else if (left == 0)
        {
            ScreenMessageUI.Show("부적이 타오른다 — 출구의 봉인이 풀린다", 2.8f);
        }
        else
        {
            ScreenMessageUI.Show("부적이 타오른다", 2f); // 이미 봉인이 풀린 뒤 (부적 7장 중 아무 3장)
        }
    }

    private void DisableColliders()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }
    }
}
