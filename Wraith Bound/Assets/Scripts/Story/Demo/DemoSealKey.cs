using System;
using UnityEngine;

/// <summary>
/// 시연 맵 봉인 열쇠 (3개 중 하나). E로 주우면 DemoSealProgress에 기록 + 안내 문구 + PickedUp 이벤트(각 연출이 받아 씀).
/// 천천히 돌며 위아래로 떠 있고 금빛 점광을 냄.
/// </summary>
public class DemoSealKey : MonoBehaviour, IInteractable
{
    [SerializeField] private int index;
    [SerializeField] private string displayName = "봉인 열쇠";
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed = 35f;
    [SerializeField] private float bobHeight = 0.04f;

    public event Action PickedUp;

    public int Index => index;

    private bool taken;
    private Vector3 visualBase;

    private void Start()
    {
        if (DemoSealProgress.Has(index))
        {
            gameObject.SetActive(false);
            return;
        }

        if (visual != null)
        {
            visualBase = visual.localPosition;
        }
    }

    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * 1.6f) * bobHeight);
    }

    public string GetInteractPrompt()
    {
        return taken ? null : "[E] " + displayName + " 줍기";
    }

    public void Interact(GameObject interactor)
    {
        if (taken)
        {
            return;
        }

        taken = true;
        DemoSealProgress.Collect(index);
        InventoryManager inventory = interactor != null ? interactor.GetComponentInChildren<InventoryManager>() : null;
        if (inventory != null)
        {
            inventory.PlayItemPickupSound();
        }

        ScreenMessageUI.Show(displayName + " (" + DemoSealProgress.Count + "/" + DemoSealProgress.Total + ")", 2.2f);
        PickedUp?.Invoke();
        gameObject.SetActive(false);
    }
}
